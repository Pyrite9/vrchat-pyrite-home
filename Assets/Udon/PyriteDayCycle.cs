// 연속 시간대 (24시간) — PyriteTimeOfDay(3단계 교체)를 대신한다
//
// 시각 hour(0..24)에서
//   해·달 위치  : 궤도 공식 (아래 SunEl/SunAz/MoonEl/MoonAz)
//   나머지 전부  : 키프레임 표(keyHour + 값 배열)를 smoothstep 으로 섞는다
// 머티리얼 교체는 없다. 절벽·물·꽃은 한 머티리얼의 색만 바꾼다(시간대별 머티리얼 차이가 색 1~2개뿐이었음, Z18a 실측).
// 반사 큐브맵만 못 섞는다 → 가까운 키프레임의 세트로 갈아 끼운다(keyCubeSet).
//
// 동기화: hourAtSync + syncStamp(서버 시각) + autoFlow + dayMinutes. 자동 흐름이면 각 클라이언트가 서버 시각으로 계산.
// 에디터 미리보기는 EvaluateAt(hour) 만 부른다(Networking 안 씀).
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using UnityEngine.Rendering.PostProcessing;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteDayCycle : UdonSharpBehaviour
{
    [UdonSynced] public float hourAtSync = 21f;
    [UdonSynced] public double syncStamp = 0;
    [UdonSynced] public bool autoFlow = false;
    [UdonSynced] public float dayMinutes = 12f;

    [Header("시작")]
    public float startHour = 21f;
    public float stepHours = 1.5f;          // 임시: 다이얼 누를 때마다 +1.5시간 (설정 UI 전까지)

    [Header("해 궤도")]
    public float riseHour = 6f;
    public float setHour = 19f;
    public float sunMaxEl = 62f;
    public float sunRiseAz = 6f;
    public float sunSetAz = 194f;
    public float nightDepth = 18f;          // 해가 진 뒤 내려가는 최대 깊이(도)
    public float lightLift = 18f;           // 낮은 해일 때 방향광을 들어 올리는 각(도). 9.8° → 22° (기존 노을 그대로)
    [Header("달 궤도")]
    public float moonRiseHour = 19f;
    public float moonSetHour = 7f;
    public float moonMaxEl = 45f;
    public float moonRiseAz = 150f;
    public float moonSetAz = 330f;

    [Header("키프레임 (시각 오름차순)")]
    public float[] keyHour;
    public int[] keyCubeSet;                // 반사 큐브맵 세트 번호 (probeCubes[set * probes.Length + k])

    [Header("하늘 (Sorafield 한 머티리얼)")]
    public Material sky;
    public float[] skyExposure;
    public float[] skyHaze;
    public float[] skyMie;
    public float[] skyMieG;
    public float[] skyCloud;
    public float skySinMin = -0.25f;        // 밤 노출 3.4 를 맞춘 깊이

    [Header("환경")]
    public Color[] ambSky;
    public Color[] ambEquator;
    public Color[] ambGround;
    public Color[] fogColor;
    public float[] fogDensity;              // 0 = 안개 끔
    public float[] reflIntensity;

    [Header("방향광 (해/달 공용)")]
    public Light sun;
    public Color[] lightColor;
    public float[] lightIntensity;

    [Header("하늘 원반")]
    public Material sunDisc;
    public Material moonDisc;
    public Color[] sunDiscColor;
    public Color moonDiscColor = new Color(1.08f, 1.16f, 1.35f, 1f);
    public GameObject[] hideObjects;        // 예전 원반(새벽 해) 등

    [Header("절벽·물·꽃 — 색만")]
    public Renderer[] cliffRenderers;
    public Material cliffMat;
    public Color[] cliffColor;
    public Renderer[] waterRenderers;
    public Material waterMat;
    public Color[] waterBase;
    public Color[] waterDeep;
    public Renderer[] flowerRenderers;
    public Material flowerMat;
    public Color[] flowerColor;

    [Header("수면 시머")]
    public Material shimmerMat;
    public float[] shimmerGain;

    [Header("반딧불")]
    public ParticleSystem[] fireflies;
    public Color[] ffColorA;
    public Color[] ffColorB;
    public float[] ffRateMul;
    public float[] ffSizeMul;

    [Header("황철석")]
    public Material[] crystalMats;
    public Color[] crystalBaseEmission;     // 머티리얼별
    public float[] crystalBaseGloss;        // 머티리얼별
    public float[] crystalEmissionMul;
    public float[] crystalMatcap;
    public float[] crystalGlossMul;
    public Color[] crystalMatcapTint;
    public Light[] crystalLights;
    public float[] crystalLightIntensity;
    // 밤 보강 (Pyrite/PyriteMetal 림·달 반짝임·미세 반짝이) — 키프레임별
    public float[] crystalRim;
    public float[] crystalSparkle;
    public float[] crystalGlint;
    public float[] crystalMcNormal;

    [Header("캠프 조명·환경음")]
    public Light[] campLights;
    public float[] campLightMul;
    public AudioSource[] ambience;
    public float[] ambienceMul;
    public AudioSource[] nightAmbience;
    public float[] nightAmbienceMul;

    [Header("밤 색조 (Pyrite/TerrainNight·StandardNight·PyriteMetal)")]
    public Material[] nightMats;
    public Color[] nightTint;
    public Color[] campTint;
    public Color[] nightAmbSky;
    public Color[] nightAmbGround;

    [Header("반사 프로브")]
    public ReflectionProbe[] probes;
    public Texture[] probeCubes;

    [Header("후처리 — 노을 볼륨(weight 1 고정) 위에 낮·밤 볼륨을 섞는다")]
    public PostProcessVolume ppDay;
    public PostProcessVolume ppNight;
    public float[] ppDayW;
    public float[] ppNightW;

    [Header("다이얼 (임시)")]
    public Transform dialPointer;

    [Header("기준값 (상수, 셋업이 넣는다) — 씬 값을 읽으면 에디터 도구를 돌릴 때마다 배수가 누적된다")]
    // 🔴 2026-09-23: 씬에서 읽던 방식 때문에 캠프 조명 9.8→0.01, 반딧불 발생량 0, 풀벌레 볼륨 1e-33 까지 무너졌다
    public float[] ffBaseRate;
    public float[] ffBaseSize;
    public float[] campBase;
    public float[] ambBase;
    public float[] nightAmbBase;

    // 기준값
    private float[] baseFfRate;
    private float[] baseFfSize;
    private float[] baseCampLight;
    private float[] baseAmbience;
    private float[] baseNightAmbience;
    private bool ready = false;
    private int ka;
    private int kb;
    private float kt;
    private int lastCubeSet = -1;
    private float nextEval = 0f;

    // 읽기 전용 상태 (설정 UI 용)
    [HideInInspector] public float currentHour;
    [HideInInspector] public float soundScale = 1f;   // 설정 UI 의 자연 소리 배율 (로컬, 동기화 안 함)
    [HideInInspector] public float sunElNow;
    [HideInInspector] public float moonElNow;

    void Start()
    {
        if (Networking.IsOwner(Networking.LocalPlayer, gameObject) && syncStamp == 0)
        {
            hourAtSync = startHour;
            syncStamp = Networking.GetServerTimeInSeconds();
            RequestSerialization();
        }
        EvaluateAt(CurrentHour());
    }

    void Update()
    {
        if (!autoFlow) return;
        if (Time.time < nextEval) return;
        nextEval = Time.time + 0.1f;
        EvaluateAt(CurrentHour());
    }

    public override void OnDeserialization() { EvaluateAt(CurrentHour()); }

    public override void Interact() { SetHour(CurrentHour() + stepHours); }

    // ── 공개 API (설정 UI 가 부른다) ─────────────────────────────
    public float CurrentHour()
    {
        if (!autoFlow) return Mathf.Repeat(hourAtSync, 24f);
        double el = Networking.GetServerTimeInSeconds() - syncStamp;
        float dm = dayMinutes < 0.5f ? 0.5f : dayMinutes;
        return Mathf.Repeat(hourAtSync + (float)(el / (dm * 60.0) * 24.0), 24f);
    }

    public void SetHour(float h)
    {
        TakeOwner();
        hourAtSync = Mathf.Repeat(h, 24f);
        syncStamp = Networking.GetServerTimeInSeconds();
        RequestSerialization();
        EvaluateAt(hourAtSync);
    }

    public void SetAutoFlow(bool on)
    {
        TakeOwner();
        float h = CurrentHour();
        hourAtSync = h; syncStamp = Networking.GetServerTimeInSeconds(); autoFlow = on;
        RequestSerialization();
        EvaluateAt(h);
    }

    public void SetDayMinutes(float m)
    {
        TakeOwner();
        float h = CurrentHour();
        hourAtSync = h; syncStamp = Networking.GetServerTimeInSeconds(); dayMinutes = m;
        RequestSerialization();
    }

    public void SetSoundScale(float v)
    {
        soundScale = Mathf.Clamp(v, 0f, 2f);
        EvaluateAt(CurrentHour());
    }

    private void TakeOwner()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
    }

    // ── 궤도 ────────────────────────────────────────────────
    public float SunEl(float h)
    {
        if (h >= riseHour && h <= setHour)
            return sunMaxEl * Mathf.Sin(Mathf.PI * (h - riseHour) / (setHour - riseHour));
        float after = Mathf.Repeat(h - setHour, 24f);
        float before = Mathf.Repeat(riseHour - h, 24f);
        float d = Mathf.Min(after, before);
        return -nightDepth * Mathf.Sin(Mathf.PI * 0.5f * Mathf.Clamp01(d / 1.6f));
    }

    public float SunAz(float h)
    {
        float dayLen = setHour - riseHour;
        if (h >= riseHour && h <= setHour)
            return sunRiseAz + (sunSetAz - sunRiseAz) * (h - riseHour) / dayLen;
        float f = Mathf.Repeat(h - setHour, 24f) / (24f - dayLen);   // 밤: 지는 곳 → 뜨는 곳(+360)
        return Mathf.Repeat(sunSetAz + (sunRiseAz + 360f - sunSetAz) * f, 360f);
    }

    public float MoonEl(float h)
    {
        float len = Mathf.Repeat(moonSetHour - moonRiseHour, 24f);
        float x = Mathf.Repeat(h - moonRiseHour, 24f);
        if (x <= len) return moonMaxEl * Mathf.Sin(Mathf.PI * x / len);
        return -10f;
    }

    public float MoonAz(float h)
    {
        float len = Mathf.Repeat(moonSetHour - moonRiseHour, 24f);
        float x = Mathf.Clamp(Mathf.Repeat(h - moonRiseHour, 24f), 0f, len);
        return moonRiseAz + (moonSetAz - moonRiseAz) * x / len;
    }

    // 하늘 셰이더에 넘기는 해 고도 — 박명 늘이기.
    // Sorafield 는 해가 -5° 쯤만 내려가도 별 하늘이 된다(1차: 19:00 하늘 91 → 19:20 37).
    // 지평선 아래는 0.35 배로 눌러 -12° 까지 -4.2°, 그 뒤 -18° 에서 -14.5°(밤 노출 3.4 를 맞춘 깊이)로.
    // 노을 (2026-09-24 관리자): 저녁(12시 이후)에만 하늘에 넘기는 해 고도를 낮춘다 — 조명·그림자 방향은 그대로.
    //  물리 하늘은 해가 ~3° 아래여야 붉어지는데 '노을' 키 18:20 의 해가 10° 였다(Z36d: 채도 0.01~0.08, 붉은 구간 실제 9초)
    //  s × (skyDuskMin → 1), skyDuskFrom~skyDuskTo° 사이에서 부드럽게 원래 고도로 돌아간다 (10° → 2°, 14.8° → 4.6°, 22° → 20.6°)
    public float skyDuskMin = 0.2f;
    public float skyDuskFrom = 12f, skyDuskTo = 24f;

    public float SkyElAt(float s, float h)
    {
        if (h >= 12f && s > 0f)
        {
            float t = Mathf.Clamp01((s - skyDuskFrom) / (skyDuskTo - skyDuskFrom));
            t = t * t * (3f - 2f * t);
            return s * (skyDuskMin + (1f - skyDuskMin) * t);
        }
        return SkyEl(s);
    }

    public float SkyEl(float s)
    {
        if (s >= 0f) return s;
        if (s > -12f) return s * 0.35f;
        return Mathf.Lerp(-4.2f, -14.5f, Mathf.Clamp01((-12f - s) / 6f));
    }

    private Vector3 Dir(float el, float az)
    {
        float e = el * Mathf.Deg2Rad;
        float a = az * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
    }

    // ── 키프레임 ───────────────────────────────────────────────
    private void FindKeys(float h)
    {
        int n = keyHour.Length;
        ka = n - 1; kb = 0;
        for (int i = 0; i < n - 1; i++)
            if (h >= keyHour[i] && h < keyHour[i + 1]) { ka = i; kb = i + 1; break; }
        float a = keyHour[ka];
        float b = keyHour[kb];
        float span = b - a; if (span <= 0f) span += 24f;
        float x = h - a; if (x < 0f) x += 24f;
        float t = Mathf.Clamp01(x / span);
        kt = t * t * (3f - 2f * t);
    }

    private float F(float[] v) { return Mathf.Lerp(v[ka], v[kb], kt); }
    private Color C(Color[] v) { return Color.Lerp(v[ka], v[kb], kt); }

    private void CaptureBase()
    {
        if (ffBaseRate != null && ffBaseRate.Length == fireflies.Length && ffBaseSize != null && ffBaseSize.Length == fireflies.Length
            && campBase != null && campBase.Length == campLights.Length
            && ambBase != null && ambBase.Length == ambience.Length
            && nightAmbBase != null && nightAmbBase.Length == nightAmbience.Length)
        {
            baseFfRate = ffBaseRate; baseFfSize = ffBaseSize; baseCampLight = campBase;
            baseAmbience = ambBase; baseNightAmbience = nightAmbBase;
            FixOnce();
            return;
        }
        ReadBaseFromScene();
        FixOnce();
    }

    private void ReadBaseFromScene()
    {
        baseFfRate = new float[fireflies.Length];
        baseFfSize = new float[fireflies.Length];
        for (int i = 0; i < fireflies.Length; i++)
        {
            if (fireflies[i] == null) continue;
            ParticleSystem.EmissionModule em = fireflies[i].emission; baseFfRate[i] = em.rateOverTime.constant;
            ParticleSystem.MainModule mm = fireflies[i].main; baseFfSize[i] = mm.startSize.constantMax;
        }
        baseCampLight = new float[campLights.Length];
        for (int i = 0; i < campLights.Length; i++) if (campLights[i] != null) baseCampLight[i] = campLights[i].intensity;
        baseAmbience = new float[ambience.Length];
        for (int i = 0; i < ambience.Length; i++) if (ambience[i] != null) baseAmbience[i] = ambience[i].volume;
        baseNightAmbience = new float[nightAmbience.Length];
        for (int i = 0; i < nightAmbience.Length; i++) if (nightAmbience[i] != null) baseNightAmbience[i] = nightAmbience[i].volume;

    }

    private void FixOnce()
    {
        // 한 번만: 교체하던 렌더러를 기준 머티리얼로 고정, 하늘 지정
        for (int k = 0; k < cliffRenderers.Length; k++) if (cliffRenderers[k] != null) cliffRenderers[k].sharedMaterial = cliffMat;
        for (int k = 0; k < waterRenderers.Length; k++) if (waterRenderers[k] != null) waterRenderers[k].sharedMaterial = waterMat;
        for (int k = 0; k < flowerRenderers.Length; k++) if (flowerRenderers[k] != null) flowerRenderers[k].sharedMaterial = flowerMat;
        if (sky != null) RenderSettings.skybox = sky;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        if (hideObjects != null) for (int k = 0; k < hideObjects.Length; k++) if (hideObjects[k] != null) hideObjects[k].SetActive(false);
        ready = true;
    }

    public void EvaluateAt(float hour)
    {
        if (!ready) CaptureBase();
        float h = Mathf.Repeat(hour, 24f);
        currentHour = h;
        FindKeys(h);

        // 해·달
        float sEl = SunEl(h);
        float sAz = SunAz(h);
        float mEl = MoonEl(h);
        float mAz = MoonAz(h);
        sunElNow = sEl; moonElNow = mEl;

        if (sky != null)
        {
            sky.SetFloat("_SunElevation", Mathf.Max(Mathf.Sin(SkyElAt(sEl, h) * Mathf.Deg2Rad), skySinMin));
            sky.SetFloat("_SunAzimuth", sAz);
            sky.SetFloat("_Exposure", F(skyExposure));
            sky.SetFloat("_HorizonHaze", F(skyHaze));
            sky.SetFloat("_MieStrength", F(skyMie));
            sky.SetFloat("_MieG", F(skyMieG));
            sky.SetFloat("_CloudCoverage", F(skyCloud));
        }

        if (sun != null)
        {
            float el;
            float az;
            float k;
            if (sEl > -2f) { el = sEl; az = sAz; k = Mathf.Clamp01((sEl + 2f) / 4f); }
            else { el = mEl; az = mAz; k = Mathf.Clamp01((-sEl - 2f) / 3f) * Mathf.Clamp01((mEl + 1f) / 4f); }
            float lift = lightLift * (1f - Mathf.Clamp01(el / 30f));
            float lel = Mathf.Max(el, 0f) + lift;
            sun.transform.rotation = Quaternion.Euler(lel, az - 180f, 0f);
            sun.color = C(lightColor);
            sun.intensity = F(lightIntensity) * k;
        }

        if (sunDisc != null)
        {
            Vector3 d = Dir(sEl, sAz);
            sunDisc.SetVector("_Dir", new Vector4(d.x, d.y, d.z, 0f));
            sunDisc.SetColor("_Color", C(sunDiscColor) * Mathf.Clamp01((sEl + 1.5f) / 2.5f));
        }
        if (moonDisc != null)
        {
            Vector3 d = Dir(mEl, mAz);
            moonDisc.SetVector("_Dir", new Vector4(d.x, d.y, d.z, 0f));
            float vis = Mathf.Clamp01((mEl + 1f) / 3f) * Mathf.Clamp01((6f - sEl) / 10f);
            moonDisc.SetColor("_Color", moonDiscColor * vis);
        }

        // 환경
        RenderSettings.ambientSkyColor = C(ambSky);
        RenderSettings.ambientEquatorColor = C(ambEquator);
        RenderSettings.ambientGroundColor = C(ambGround);
        float fd = F(fogDensity);
        RenderSettings.fog = fd > 0.0003f;
        RenderSettings.fogColor = C(fogColor);
        RenderSettings.fogDensity = fd;
        RenderSettings.reflectionIntensity = F(reflIntensity);

        if (cliffMat != null) cliffMat.SetColor("_Color", C(cliffColor));
        if (waterMat != null) { waterMat.SetColor("_BaseColor", C(waterBase)); waterMat.SetColor("_DeepColor", C(waterDeep)); }
        if (flowerMat != null) flowerMat.SetColor("_MainColor", C(flowerColor));
        if (shimmerMat != null) shimmerMat.SetFloat("_Gain", F(shimmerGain));

        float rate = F(ffRateMul);
        float size = F(ffSizeMul);
        Color fa = C(ffColorA);
        Color fb = C(ffColorB);
        for (int k = 0; k < fireflies.Length; k++)
        {
            ParticleSystem ps = fireflies[k];
            if (ps == null) continue;
            ParticleSystem.MainModule mm = ps.main;
            mm.startColor = new ParticleSystem.MinMaxGradient(fa, fb);
            mm.startSize = new ParticleSystem.MinMaxCurve(baseFfSize[k] * 0.45f * size, baseFfSize[k] * size);
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = baseFfRate[k] * rate;
            if (rate <= 0.02f) { if (ps.isPlaying) ps.Stop(); }
            else if (!ps.isPlaying) ps.Play();
        }

        float emul = F(crystalEmissionMul);
        float mc = F(crystalMatcap);
        float gm = F(crystalGlossMul);
        Color mct = C(crystalMatcapTint);
        for (int k = 0; k < crystalMats.Length; k++)
        {
            Material m = crystalMats[k];
            if (m == null) continue;
            m.SetColor("_EmissionColor", crystalBaseEmission[k] * emul);
            m.SetFloat("_MatCapStrength", mc);
            m.SetColor("_MatCapTint", mct);
            m.SetFloat("_Glossiness", crystalBaseGloss[k] * gm);
        }
        if (crystalRim != null && crystalRim.Length == keyHour.Length)
        {
            float rim = F(crystalRim);
            float spk = F(crystalSparkle);
            float gl = F(crystalGlint);
            float mcn = F(crystalMcNormal);
            Vector3 md = Dir(mEl, mAz);
            Vector4 md4 = new Vector4(md.x, md.y, md.z, 0f);
            Color rimC = new Color(1f, 0.78f, 0.42f, 1f) * rim;
            Color glC = new Color(0.85f, 0.9f, 1f, 1f) * gl;
            for (int k = 0; k < crystalMats.Length; k++)
            {
                Material m = crystalMats[k];
                if (m == null) continue;
                m.SetColor("_RimColor", rimC);
                m.SetFloat("_RimPower", 5f);
                m.SetColor("_GlintColor", glC);
                m.SetVector("_GlintDir", md4);
                m.SetFloat("_GlintSharp", 40f);
                m.SetFloat("_SparkleStrength", spk);
                m.SetFloat("_SparkleScale", 28f);
                m.SetFloat("_MatCapNormal", mcn);
            }
        }

        float cl = F(crystalLightIntensity);
        for (int k = 0; k < crystalLights.Length; k++)
        {
            if (crystalLights[k] == null) continue;
            crystalLights[k].intensity = cl;
            crystalLights[k].enabled = cl > 0.01f;
        }

        float cm = F(campLightMul);
        for (int k = 0; k < campLights.Length; k++) if (campLights[k] != null) campLights[k].intensity = baseCampLight[k] * cm;
        float am = F(ambienceMul);
        float nm = F(nightAmbienceMul);
        for (int k = 0; k < ambience.Length; k++) if (ambience[k] != null) ambience[k].volume = baseAmbience[k] * am * soundScale;
        for (int k = 0; k < nightAmbience.Length; k++) if (nightAmbience[k] != null) nightAmbience[k].volume = baseNightAmbience[k] * nm * soundScale;

        Color nt = C(nightTint);
        Color ct = C(campTint);
        Color nas = C(nightAmbSky);
        Color nag = C(nightAmbGround);
        for (int k = 0; k < nightMats.Length; k++)
        {
            Material m = nightMats[k];
            if (m == null) continue;
            m.SetColor("_NightTint", nt);
            m.SetColor("_CampTint", ct);
            m.SetColor("_NightAmbSky", nas);
            m.SetColor("_NightAmbGround", nag);
        }

        // 반사: 가까운 키프레임의 세트
        int set = keyCubeSet[kt < 0.5f ? ka : kb];
        if (set != lastCubeSet && probes != null && probeCubes != null)
        {
            lastCubeSet = set;
            int np = probes.Length;
            for (int k = 0; k < np; k++)
            {
                int idx = set * np + k;
                if (probes[k] == null || idx >= probeCubes.Length || probeCubes[idx] == null) continue;
                probes[k].customBakedTexture = probeCubes[idx];
            }
        }

        if (ppDay != null && ppDayW != null && ppDayW.Length == keyHour.Length) ppDay.weight = F(ppDayW);
        if (ppNight != null && ppNightW != null && ppNightW.Length == keyHour.Length) ppNight.weight = F(ppNightW);

        if (dialPointer != null) dialPointer.localRotation = Quaternion.Euler(0f, h / 24f * 360f, 0f);
    }

    // 에디터 도구가 기준값을 다시 잡게
    public void ResetCache() { ready = false; lastCubeSet = -1; }
}
