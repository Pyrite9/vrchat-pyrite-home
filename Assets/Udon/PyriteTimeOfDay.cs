// 시간대 전환 — 노을 / 밤 / 새벽
//
// 라이트맵은 한 번만 굽고 고정한다. LightmapSettings는 Udon에 노출돼 있지 않다(실측).
// 그래서 "구워진 빛"은 못 바꾸고, 아래 것들만 바꾼다. 전부 Udon 노출 확인함.
//   RenderSettings (skybox / ambient / fog / reflectionIntensity)
//   Light.color·intensity        — 태양 방위는 고정. 돌리면 구워진 그림자와 어긋난다
//   Renderer.sharedMaterial      — 절벽·물·꽃을 밤용 복사본으로 교체
//   ParticleSystem.main          — 반딧불 색·수
//   Material.SetFloat            — 수면 시머 강도
//   AudioSource.volume           — 환경음
//
// 배열은 전부 프리셋 수만큼. 인덱스 0=노을 1=밤 2=새벽.
// 병렬 배열이 보기 싫지만 Udon에서는 구조체 배열을 못 쓴다.
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteTimeOfDay : UdonSharpBehaviour
{
    [UdonSynced] public int index = 0;

    public string[] presetName;

    [Header("환경")]
    public Material[] skybox;
    public Color[] ambSky;
    public Color[] ambEquator;
    public Color[] ambGround;
    public float[] ambIntensity;
    public int[] ambMode;              // 0 = Skybox(구운 SH 유지) / 1 = Trilight(직접 지정)
    public bool[] fogOn;
    public Color[] fogColor;
    public float[] fogDensity;
    public float[] reflIntensity;

    [Header("태양 — 방위는 고정, 색·강도만")]
    public Light sun;
    public Color[] sunColor;
    public float[] sunIntensity;

    [Header("머티리얼 교체")]
    public Renderer[] cliffRenderers;
    public Material[] cliffMat;
    public Renderer[] waterRenderers;
    public Material[] waterMat;
    public Renderer[] flowerRenderers;
    public Material[] flowerMat;

    [Header("수면 시머")]
    public Material shimmerMat;
    public float[] shimmerGain;

    [Header("반딧불")]
    public ParticleSystem[] fireflies;
    public Color[] ffColorA;
    public Color[] ffColorB;
    public float[] ffRateMul;
    public float[] ffSizeMul;

    [Header("황철석 발광")]
    public Material[] crystalMats;        // 결정 머티리얼들
    public Color[] crystalBaseEmission;   // 머티리얼별 기준 이미션 (길이 = crystalMats)
    public float[] crystalEmissionMul;    // 프리셋별 배수 (길이 = presetName)
    public Light[] crystalLights;         // 밤에만 켜는 실시간 포인트 라이트
    public float[] crystalLightIntensity; // 프리셋별 절대 강도
    public float[] crystalMatcap;         // 프리셋별 MatCap 세기 (Pyrite/PyriteMetal 의 _MatCapStrength)
    public Color[] crystalMatcapTint;     // 프리셋별 MatCap 환경색 (_MatCapTint) — 밤엔 하늘색
    public float[] crystalBaseGloss;      // 머티리얼별 기준 smoothness (길이 = crystalMats)
    public float[] crystalGlossMul;       // 프리셋별 배수 — 밤엔 낮춰서 반딧불 빛이 면에 번지게

    [Header("캠프 조명 — 기준값에 배수")]
    public Light[] campLights;
    public float[] campLightMul;

    [Header("환경음 — 기준 볼륨에 배수")]
    public AudioSource[] ambience;
    public float[] ambienceMul;
    [Header("환경음 — 야행성(풀벌레)")]
    public AudioSource[] nightAmbience;
    public float[] nightAmbienceMul;

    [Header("밤 어둠 — 구운 조명 색조")]
    // Pyrite/TerrainNight · Pyrite/StandardNight 머티리얼(지형·부두)의 색조를 프리셋마다 바꾼다.
    // 🔴 terrain.materialTemplate 대입은 Udon 차단(읽기만 열림). 그래서 교체 대신 Material.SetColor.
    public Material[] nightMats;
    public Color[] nightTint;     // 프리셋별 — 캠프 밖
    public Color[] campTint;      // 프리셋별 — 캠프 안
    public Color[] nightAmbSky;   // 프리셋별 — 밤 하늘빛(반구 위). 노을·새벽 = 검정
    public Color[] nightAmbGround;// 프리셋별 — 밤 하늘빛(반구 아래)

    [Header("리플렉션 프로브 — 프리셋별 큐브맵")]
    // 물 반사는 전부 프로브에서 온다. 라이트맵은 런타임 교체가 막혀 있지만
    // ReflectionProbe.customBakedTexture 는 열려 있어서 프리셋마다 갈아 끼운다.
    // probeCubes 는 [프리셋 * probes.Length + 프로브] 로 펼친 1차원 배열 (U# 는 2차원 배열 불가)
    public ReflectionProbe[] probes;
    public Texture[] probeCubes;

    [Header("패널 / 다이얼")]
    public GameObject panel;              // 켜고 끄는 부모
    public GameObject[] panelCards;       // 프리셋별 카드(쿼드). 해당 인덱스만 켠다
    public float panelSeconds = 8f;
    public Transform dialPointer;         // 다이얼 바늘
    public float[] dialYaw;               // 프리셋별 바늘 각도

    // 기준값 — Start에서 한 번만 읽어둔다
    private float[] baseFfRate;
    private float[] baseFfSize;
    private float[] baseCampLight;
    private float[] baseAmbience;
    private float[] baseNightAmbience;
    private bool ready = false;
    private float panelHideAt = -1f;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        Apply();
    }

    // 기준값 캡처 — 프리셋을 적용하기 전의 값이어야 하므로 딱 한 번만 잡는다.
    // 에디터에서 Apply()를 직접 불러도 되도록 Start가 아니라 여기서 처리한다.
    private void CaptureBase()
    {
        baseFfRate = new float[fireflies.Length];
        baseFfSize = new float[fireflies.Length];
        for (int i = 0; i < fireflies.Length; i++)
        {
            ParticleSystem.EmissionModule em = fireflies[i].emission;
            baseFfRate[i] = em.rateOverTime.constant;
            ParticleSystem.MainModule mm = fireflies[i].main;
            baseFfSize[i] = mm.startSize.constantMax;
        }
        baseCampLight = new float[campLights.Length];
        for (int i = 0; i < campLights.Length; i++) baseCampLight[i] = campLights[i].intensity;

        baseAmbience = new float[ambience.Length];
        for (int i = 0; i < ambience.Length; i++) baseAmbience[i] = ambience[i].volume;
        baseNightAmbience = new float[nightAmbience.Length];
        for (int i = 0; i < nightAmbience.Length; i++) baseNightAmbience[i] = nightAmbience[i].volume;

        ready = true;
    }

    void Update()
    {
        if (panel == null) return;

        if (panelHideAt > 0f && Time.time > panelHideAt)
        {
            panelHideAt = -1f;
            panel.SetActive(false);
            return;
        }

        // 패널이 켜져 있는 동안 보는 사람 쪽을 향한다.
        // 트랜스폼은 동기화되지 않으므로 이 회전은 각자 클라이언트에서만 일어난다 — 남의 화면을 안 건드린다.
        if (panel.activeSelf && Networking.LocalPlayer != null)
        {
            Vector3 d = Networking.LocalPlayer.GetPosition() - panel.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.04f)
            {
                // Unity 기본 Quad의 법선은 -Z다. 보는 사람 쪽을 -Z가 향하게 한다.
                Quaternion look = Quaternion.LookRotation(-d.normalized, Vector3.up);
                panel.transform.rotation = look * Quaternion.Euler(-20f, 0f, 0f);
            }
        }
    }

    // 다이얼에서 부른다
    public override void Interact()
    {
        Next();
    }

    public void Next()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        index = index + 1;
        if (index >= presetName.Length) index = 0;
        RequestSerialization();
        Apply();
        ShowPanel();
    }

    public override void OnDeserialization()
    {
        Apply();
        ShowPanel();
    }

    private void ShowPanel()
    {
        if (panel == null) return;
        panel.SetActive(true);
        panelHideAt = Time.time + panelSeconds;
        for (int k = 0; k < panelCards.Length; k++)
            if (panelCards[k] != null) panelCards[k].SetActive(k == index);
    }

    public void Apply()
    {
        if (!ready) CaptureBase();
        int i = index;
        if (i < 0 || i >= presetName.Length) return;

        // 노을(0)은 구운 스카이박스 SH를 그대로 쓴다. DynamicGI.UpdateEnvironment가
        // Udon에 없어서, 스카이박스를 바꾼 프리셋은 Trilight로 직접 지정해야 한다.
        if (ambMode[i] == 0) RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
        else                 RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = ambSky[i];
        RenderSettings.ambientEquatorColor = ambEquator[i];
        RenderSettings.ambientGroundColor = ambGround[i];
        RenderSettings.ambientIntensity = ambIntensity[i];
        RenderSettings.skybox = skybox[i];
        RenderSettings.fog = fogOn[i];
        RenderSettings.fogColor = fogColor[i];
        RenderSettings.fogDensity = fogDensity[i];
        RenderSettings.reflectionIntensity = reflIntensity[i];

        if (sun != null)
        {
            sun.color = sunColor[i];
            sun.intensity = sunIntensity[i];
        }

        for (int k = 0; k < cliffRenderers.Length; k++)
            if (cliffRenderers[k] != null) cliffRenderers[k].sharedMaterial = cliffMat[i];
        for (int k = 0; k < waterRenderers.Length; k++)
            if (waterRenderers[k] != null) waterRenderers[k].sharedMaterial = waterMat[i];
        for (int k = 0; k < flowerRenderers.Length; k++)
            if (flowerRenderers[k] != null) flowerRenderers[k].sharedMaterial = flowerMat[i];

        if (shimmerMat != null) shimmerMat.SetFloat("_Gain", shimmerGain[i]);

        for (int k = 0; k < fireflies.Length; k++)
        {
            ParticleSystem ps = fireflies[k];
            if (ps == null) continue;
            ParticleSystem.MainModule mm = ps.main;
            mm.startColor = new ParticleSystem.MinMaxGradient(ffColorA[i], ffColorB[i]);
            mm.startSize = new ParticleSystem.MinMaxCurve(baseFfSize[k] * 0.45f * ffSizeMul[i],
                                                         baseFfSize[k] * ffSizeMul[i]);
            ParticleSystem.EmissionModule em = ps.emission;
            em.rateOverTime = baseFfRate[k] * ffRateMul[i];
            if (ffRateMul[i] <= 0.001f) ps.Stop(); else if (!ps.isPlaying) ps.Play();
        }

        for (int k = 0; k < crystalMats.Length; k++)
        {
            if (crystalMats[k] == null) continue;
            Color e = crystalBaseEmission[k] * crystalEmissionMul[i];
            crystalMats[k].SetColor("_EmissionColor", e);
            if (crystalMatcap != null && i < crystalMatcap.Length)
                crystalMats[k].SetFloat("_MatCapStrength", crystalMatcap[i]);
            if (crystalMatcapTint != null && i < crystalMatcapTint.Length)
                crystalMats[k].SetColor("_MatCapTint", crystalMatcapTint[i]);
            if (crystalBaseGloss != null && crystalGlossMul != null && k < crystalBaseGloss.Length && i < crystalGlossMul.Length)
                crystalMats[k].SetFloat("_Glossiness", crystalBaseGloss[k] * crystalGlossMul[i]);
        }
        for (int k = 0; k < crystalLights.Length; k++)
        {
            if (crystalLights[k] == null) continue;
            float v = crystalLightIntensity[i];
            crystalLights[k].intensity = v;
            crystalLights[k].enabled = v > 0.001f;
        }

        for (int k = 0; k < campLights.Length; k++)
            if (campLights[k] != null) campLights[k].intensity = baseCampLight[k] * campLightMul[i];

        if (dialPointer != null && dialYaw.Length > i)
            dialPointer.localRotation = Quaternion.Euler(0f, dialYaw[i], 0f);

        for (int k = 0; k < ambience.Length; k++)
            if (ambience[k] != null) ambience[k].volume = baseAmbience[k] * ambienceMul[i];
        for (int k = 0; k < nightAmbience.Length; k++)
            if (nightAmbience[k] != null) nightAmbience[k].volume = baseNightAmbience[k] * nightAmbienceMul[i];

        if (nightMats != null && nightTint != null && campTint != null
            && i < nightTint.Length && i < campTint.Length)
        {
            for (int k = 0; k < nightMats.Length; k++)
            {
                if (nightMats[k] == null) continue;
                nightMats[k].SetColor("_NightTint", nightTint[i]);
                nightMats[k].SetColor("_CampTint",  campTint[i]);
                if (nightAmbSky != null && i < nightAmbSky.Length) nightMats[k].SetColor("_NightAmbSky", nightAmbSky[i]);
                if (nightAmbGround != null && i < nightAmbGround.Length) nightMats[k].SetColor("_NightAmbGround", nightAmbGround[i]);
            }
        }

        if (probes != null && probeCubes != null)
        {
            int np = probes.Length;
            for (int k = 0; k < np; k++)
            {
                int idx = i * np + k;
                if (probes[k] == null || idx >= probeCubes.Length || probeCubes[idx] == null) continue;
                probes[k].customBakedTexture = probeCubes[idx];
            }
        }
    }
}
