// Tools ▸ Pyrite ▸ Z18b. Day Cycle Setup / Z18c. Day Cycle Revert / Z18d. Day Cycle Sweep Render
//  연속 시간대: PyriteTimeOfDay(3단계 교체) → PyriteDayCycle(24시간, 키프레임 보간)
//   선행: H(UdonSharp 프로그램 에셋 생성) 로 PyriteDayCycle 프로그램 에셋이 있어야 한다
//   Z18b — 기존 ToD 값에서 노을·밤·새벽 키프레임을 옮기고, 아침·정오·오후·푸른 시간을 새로 넣는다.
//          하늘은 Sorafield 머티리얼 1개(M_Sky_PyriteCycle, 유료 폴더 안), 절벽·물·꽃은 기준 머티리얼 색만.
//          노을 앰비언트는 구운 하늘 SH 를 Trilight 로 역산(런타임엔 SH 를 못 바꾼다).
//          기존 ToD 는 비활성(에디터 도구 호환 때문에 지우지 않음). 에디터 저장 상태는 18:20 노을(베이크 기준).
//   Z18c — 되돌림: DayCycle 제거, ToD 다시 켜고 노을 적용
//   Z18d — 12개 시각 × 2 시점 렌더 → Assets/_preview/cycle/, 로그 Logs/pyrite_cycle.txt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteDayCycleSetup
{
    const string LOG = "Logs/pyrite_cycle.txt";
    const string SKY = "Assets/Sorafield Atmosphere Sky/Materials/M_Sky_PyriteCycle.mat";
    const string DISC_SUN = "Assets/Materials/M_SkyDisc_SunDusk.mat";
    const string DISC_MOON = "Assets/Materials/M_SkyDisc_Moon.mat";
    public const float EDITOR_HOUR = 18f + 20f / 60f;       // 에디터에 저장해 두는 시각 = 노을 키프레임

    class K
    {
        public string name; public float hour; public int cube;
        public float skyExp, skyHaze, skyMie, skyMieG, skyCloud;
        public Color ambSky, ambEq, ambGr, fogCol; public float fogDen, refl;
        public Color lightCol; public float lightInt; public Color sunDisc;
        public Color cliff, waterBase, waterDeep, flower;
        public float shimmer; public Color ffA, ffB; public float ffRate, ffSize;
        public float crEm, crMatcap, crGloss, crLight; public Color crTint;
        public float camp, amb, nightAmb;
        public Color nightTint, campTint, nAmbSky, nAmbGr;
        public float ppDay, ppNight;
        public float crRim, crSpk, crGlint, crMcN = 1f;
        public K Clone(string n, float h, int c) { var k = (K)MemberwiseClone(); k.name = n; k.hour = h; k.cube = c; return k; }
    }

    static Color G(float v) => new Color(v, v, v, 1f);
    static Color RGB(float r, float g, float b) => new Color(r, g, b, 1f);

    static K FromPreset(PyriteTimeOfDay t, int p, string name, float hour, int cube, Material[] cliffM, Material[] waterM, Material[] flowerM)
    {
        var sky = t.skybox[p];
        var k = new K
        {
            name = name, hour = hour, cube = cube,
            skyExp = sky.GetFloat("_Exposure"), skyHaze = sky.GetFloat("_HorizonHaze"), skyMie = sky.GetFloat("_MieStrength"),
            skyMieG = sky.GetFloat("_MieG"), skyCloud = sky.GetFloat("_CloudCoverage"),
            ambSky = t.ambSky[p], ambEq = t.ambEquator[p], ambGr = t.ambGround[p],
            fogCol = t.fogColor[p], fogDen = t.fogOn[p] ? t.fogDensity[p] : 0f, refl = t.reflIntensity[p],
            lightCol = t.sunColor[p], lightInt = t.sunIntensity[p],
            cliff = cliffM[p].GetColor("_Color"), waterBase = waterM[p].GetColor("_BaseColor"), waterDeep = waterM[p].GetColor("_DeepColor"),
            // 🔴 꽃 기준색은 상수 — Day 머티리얼은 사이클이 보정값을 써 넣으므로 읽으면 재실행마다 곱해진다 (Z18a 원래 값)
            flower = p == 1 ? new Color(0.145f, 0.165f, 0.24f, 1f) : new Color(0.66f, 0.66f, 0.66f, 1f),
            shimmer = t.shimmerGain[p], ffA = t.ffColorA[p], ffB = t.ffColorB[p], ffRate = t.ffRateMul[p], ffSize = t.ffSizeMul[p],
            crEm = t.crystalEmissionMul[p], crMatcap = t.crystalMatcap[p], crGloss = t.crystalGlossMul[p], crLight = t.crystalLightIntensity[p], crTint = t.crystalMatcapTint[p],
            camp = t.campLightMul[p], amb = t.ambienceMul[p], nightAmb = t.nightAmbienceMul[p],
            nightTint = t.nightTint[p], campTint = t.campTint[p], nAmbSky = t.nightAmbSky[p], nAmbGr = t.nightAmbGround[p],
        };
        return k;
    }

    [MenuItem("Tools/Pyrite/Z18b. Day Cycle Setup", false, 41)]
    public static void Setup()
    {
        var sb = new StringBuilder("[Z18b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (tod == null) { sb.AppendLine("PyriteTimeOfDay 없음"); Flush(sb, false); return; }
        var go = tod.gameObject;

        // 기존 ToD 를 노을로 돌려 둔 상태에서 값을 읽는다
        var fr = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fr != null) fr.SetValue(tod, false);
        tod.index = 0; tod.Apply();

        // 노을 앰비언트: 구운 하늘 SH → Trilight
        var shp = RenderSettings.ambientProbe;
        var dirs = new[] { Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                           new Vector3(1,0,1).normalized, new Vector3(-1,0,1).normalized, new Vector3(1,0,-1).normalized, new Vector3(-1,0,-1).normalized };
        var cols = new Color[dirs.Length];
        shp.Evaluate(dirs, cols);
        Color up = cols[0], down = cols[1], eq = Color.black;
        for (int i = 2; i < dirs.Length; i++) eq += cols[i];
        eq /= (dirs.Length - 2); up.a = down.a = eq.a = 1f;
        sb.AppendLine(string.Format("dusk SH → trilight  sky {0}  equator {1}  ground {2}  (ambientMode {3})", Fmt(up), Fmt(eq), Fmt(down), RenderSettings.ambientMode));

        // ── 키프레임 ──
        var dusk = FromPreset(tod, 0, "dusk", EDITOR_HOUR, 0, tod.cliffMat, tod.waterMat, tod.flowerMat);
        dusk.ambSky = up; dusk.ambEq = eq; dusk.ambGr = down;
        dusk.fogCol = RGB(0.55f, 0.45f, 0.42f);
        dusk.sunDisc = new Color(1.00f, 0.56f, 0.26f) * 3.0f;
        var night = FromPreset(tod, 1, "night", 0f, 1, tod.cliffMat, tod.waterMat, tod.flowerMat);
        night.sunDisc = dusk.sunDisc;
        var dawn = FromPreset(tod, 2, "dawn", 6.2f, 2, tod.cliffMat, tod.waterMat, tod.flowerMat);
        dawn.sunDisc = new Color(1.00f, 0.66f, 0.60f) * 2.6f;

        var noon = dusk.Clone("noon", 12f, 0);
        noon.skyExp = 1.0f; noon.skyHaze = 1.0f; noon.skyMie = 1.2f; noon.skyMieG = 0.80f; noon.skyCloud = 0.40f;
        noon.ambSky = RGB(0.62f, 0.68f, 0.78f); noon.ambEq = RGB(0.48f, 0.49f, 0.50f); noon.ambGr = RGB(0.24f, 0.23f, 0.21f);
        noon.fogCol = RGB(0.62f, 0.70f, 0.80f); noon.fogDen = 0.0015f; noon.refl = 1f;
        noon.lightCol = RGB(1f, 0.96f, 0.90f); noon.lightInt = 1.35f; noon.sunDisc = new Color(1f, 0.95f, 0.85f) * 1.8f;
        noon.waterBase = RGB(0.28f, 0.39f, 0.42f); noon.waterDeep = RGB(0.07f, 0.14f, 0.18f);
        noon.shimmer = 0.8f; noon.ffRate = 0f; noon.ffSize = 1f;
        noon.crMatcap = 1f; noon.crTint = G(1f); noon.crGloss = 1f; noon.crLight = 0f;
        noon.camp = 0.25f; noon.amb = 1f; noon.nightAmb = 0.05f;
        noon.nightTint = G(1f); noon.campTint = G(1f); noon.nAmbSky = Color.black; noon.nAmbGr = Color.black;

        var morning = noon.Clone("morning", 7.5f, 2);
        morning.skyExp = 1.05f; morning.skyHaze = 1.8f; morning.skyMie = 1.8f; morning.skyMieG = 0.83f; morning.skyCloud = 0.35f;
        morning.ambSky = RGB(0.50f, 0.50f, 0.58f); morning.ambEq = RGB(0.42f, 0.40f, 0.42f); morning.ambGr = RGB(0.20f, 0.19f, 0.19f);
        morning.fogCol = RGB(0.62f, 0.60f, 0.62f); morning.fogDen = 0.003f; morning.refl = 0.85f;
        morning.lightCol = RGB(1f, 0.84f, 0.70f); morning.lightInt = 1.1f; morning.sunDisc = new Color(1f, 0.80f, 0.60f) * 2.4f;
        morning.cliff = RGB(0.215f, 0.205f, 0.200f);
        morning.waterBase = Color.Lerp(dawn.waterBase, noon.waterBase, 0.5f); morning.waterDeep = Color.Lerp(dawn.waterDeep, noon.waterDeep, 0.5f);
        morning.camp = 0.35f;

        var afternoon = noon.Clone("afternoon", 16f, 0);
        afternoon.skyExp = 1.05f; afternoon.skyHaze = 1.2f; afternoon.skyMie = 1.6f; afternoon.skyMieG = 0.82f;
        afternoon.ambSky = RGB(0.62f, 0.62f, 0.66f); afternoon.ambEq = RGB(0.48f, 0.46f, 0.44f); afternoon.ambGr = RGB(0.24f, 0.22f, 0.20f);
        afternoon.fogCol = RGB(0.70f, 0.68f, 0.66f); afternoon.fogDen = 0.002f;
        afternoon.lightCol = RGB(1f, 0.90f, 0.76f); afternoon.lightInt = 1.3f; afternoon.sunDisc = new Color(1f, 0.85f, 0.65f) * 2.2f;
        afternoon.ffRate = 0.15f; afternoon.camp = 0.45f; afternoon.nightAmb = 0.15f;

        var blue = dusk.Clone("blue", 19.2f, 1);
        blue.skyExp = 2.0f; blue.skyHaze = 0.8f; blue.skyMie = 1.2f; blue.skyMieG = 0.82f; blue.skyCloud = 0.5f;
        blue.ambSky = RGB(0.16f, 0.18f, 0.30f); blue.ambEq = RGB(0.12f, 0.13f, 0.20f); blue.ambGr = RGB(0.05f, 0.05f, 0.07f);
        blue.fogCol = RGB(0.16f, 0.17f, 0.30f); blue.fogDen = 0.007f; blue.refl = 0.6f;
        blue.lightCol = RGB(0.45f, 0.50f, 0.75f); blue.lightInt = 0.1f;
        blue.cliff = RGB(0.13f, 0.135f, 0.16f);
        blue.waterBase = Color.Lerp(dusk.waterBase, night.waterBase, 0.5f); blue.waterDeep = Color.Lerp(dusk.waterDeep, night.waterDeep, 0.5f);
        blue.flower = RGB(0.36f, 0.38f, 0.47f);
        blue.shimmer = 1.3f; blue.ffRate = 1.4f; blue.ffSize = 1.15f;
        blue.crMatcap = 0.6f; blue.crTint = RGB(0.72f, 0.78f, 1f); blue.crGloss = 0.85f; blue.crLight = 0.6f;
        blue.camp = 1.25f; blue.amb = 0.9f; blue.nightAmb = 0.7f;
        blue.nightTint = RGB(0.45f, 0.45f, 0.55f); blue.campTint = RGB(0.80f, 0.72f, 0.66f);
        blue.nAmbSky = RGB(0.015f, 0.022f, 0.05f); blue.nAmbGr = RGB(0.005f, 0.006f, 0.011f);

        blue.hour = 19.4f; blue.skyExp = 2.6f;
        // 박명 — 1차에서 18:20→19:12 하늘 밝기 136→41 (26초 만에)로 급했다
        var sunset = dusk.Clone("sunset", 19.0f, 0);
        sunset.skyExp = 1.3f; sunset.skyHaze = 1.3f; sunset.skyMie = 2.2f; sunset.skyCloud = 0.45f;
        sunset.ambSky = Color.Lerp(dusk.ambSky, blue.ambSky, 0.35f); sunset.ambEq = Color.Lerp(dusk.ambEq, blue.ambEq, 0.35f); sunset.ambGr = Color.Lerp(dusk.ambGr, blue.ambGr, 0.35f);
        sunset.fogCol = RGB(0.40f, 0.32f, 0.36f); sunset.fogDen = 0.003f;
        sunset.cliff = Color.Lerp(dusk.cliff, blue.cliff, 0.3f); sunset.flower = Color.Lerp(dusk.flower, blue.flower, 0.3f);
        sunset.ffRate = 1.2f; sunset.camp = 1.1f; sunset.nightAmb = 0.5f; sunset.crLight = 0.2f;
        sunset.nightTint = Color.Lerp(dusk.nightTint, blue.nightTint, 0.3f); sunset.campTint = Color.Lerp(dusk.campTint, blue.campTint, 0.3f);
        var predawn = blue.Clone("predawn", 5.5f, 2);
        predawn.skyHaze = 1.5f; predawn.ambSky = RGB(0.20f, 0.18f, 0.28f); predawn.fogCol = RGB(0.24f, 0.20f, 0.28f);
        predawn.ffRate = 0.3f; predawn.camp = 0.9f; predawn.nightAmb = 0.4f;
        // 꽃 보정 — ACES 가 짙은 파랑을 크게 누른다(텐트 옆 꽃 밝기: 노을 56→39, 밤 25→18, 정오 70→68). 후처리 전 밝기로. 인게임 정오 꽃 RGB 5/37/88 (밝기 44) → 정오도 1.45
        var flowerMul = new System.Collections.Generic.Dictionary<K, float> {
            { night, 1.8f }, { predawn, 1.8f }, { dawn, 2.05f }, { morning, 2.3f }, { noon, 2.3f },
            { afternoon, 2.3f }, { dusk, 2.05f }, { sunset, 2.05f }, { blue, 1.8f } };   // 관리자: "약간만 더" (+15%, 밤 1.4→1.8)   // 기준 0.66 → 낮 1.32 / 노을 1.19 (0.66×2.1 에서 정오 80·노을 65 측정)
        foreach (var kv in flowerMul) { var c = kv.Key.flower * kv.Value; c.a = 1f; kv.Key.flower = c; }
        sb.AppendLine("flower: dusk " + Fmt(dusk.flower) + " noon " + Fmt(noon.flower) + " night " + Fmt(night.flower));

        // 결정 밤 보강 (Z21a 비교 → 관리자: 림 + 미세 반짝이). 밤 MatCap 색조를 파랑→따뜻한 금색 ("색칠한 깍두기" 해소)
        var warm = RGB(0.62f, 0.52f, 0.38f);
        foreach (var k in new[] { night, predawn }) { k.crRim = 0.35f; k.crSpk = 4f; k.crGlint = 2.5f; k.crMcN = 0.45f; k.crTint = warm; k.crMatcap = 0.30f; }
        blue.crRim = 0.25f; blue.crSpk = 3f; blue.crGlint = 1.8f; blue.crMcN = 0.6f; blue.crTint = RGB(0.75f, 0.68f, 0.55f);
        sunset.crRim = 0.1f; sunset.crSpk = 1f; sunset.crGlint = 0.8f; sunset.crMcN = 0.85f;
        dawn.crRim = 0.1f; dawn.crSpk = 1f; dawn.crGlint = 0.5f; dawn.crMcN = 0.85f;

        // 후처리 볼륨 weight (노을 프로필 위에 섞음)
        night.ppDay = 0f; night.ppNight = 1f;
        predawn.ppDay = 0f; predawn.ppNight = 0.7f;
        dawn.ppDay = 0.2f; dawn.ppNight = 0.2f;
        morning.ppDay = 0.8f; morning.ppNight = 0f;
        noon.ppDay = 1f; noon.ppNight = 0f;
        afternoon.ppDay = 1f; afternoon.ppNight = 0f;
        dusk.ppDay = 0f; dusk.ppNight = 0f;
        sunset.ppDay = 0f; sunset.ppNight = 0.2f;
        blue.ppDay = 0f; blue.ppNight = 0.6f;
        // 낮 다듬기 (Z23b 격자 → 관리자: 절벽 0.30, 구름 0.5). Sorafield _CloudCoverage 는 낮출수록 흐려진다(0.2 = 회색 하늘)
        { var c = dusk.cliff * (0.30f / 0.227f); c.a = 1f; noon.cliff = c; afternoon.cliff = c; var m = dusk.cliff * (0.28f / 0.227f); m.a = 1f; morning.cliff = m; }
        noon.skyCloud = 0.5f; afternoon.skyCloud = 0.5f; morning.skyCloud = 0.45f;
        predawn.skyCloud = 0.55f; dawn.skyCloud = 0.55f;   // 관리자: 여명·새벽 먹구름(새벽 프리셋 0.30) → 0.55
        sb.AppendLine("day: cliff " + Fmt(noon.cliff) + " cloud " + noon.skyCloud);

        // 낮 반사 세트(Z23a)가 있으면 아침 3 / 정오 4 / 오후 5
        if (tod.probes != null && tod.probeCubes != null && tod.probeCubes.Length >= 6 * tod.probes.Length) { morning.cube = 3; noon.cube = 4; afternoon.cube = 5; }
        var keys = new List<K> { night, night.Clone("night", 4.3f, 1), predawn, dawn, morning, noon, afternoon, dusk, sunset, blue, night.Clone("night", 20.3f, 1) };
        keys = keys.OrderBy(k => k.hour).ToList();
        sb.AppendLine("keys: " + string.Join(" | ", keys.Select(k => string.Format("{0:00.00} {1} cube{2}", k.hour, k.name, k.cube))));

        // ── 하늘 머티리얼 1개 (유료 폴더 안) ──
        var skyM = AssetDatabase.LoadAssetAtPath<Material>(SKY);
        if (skyM == null) { skyM = new Material(tod.skybox[0]); AssetDatabase.CreateAsset(skyM, SKY); }
        skyM.shader = tod.skybox[0].shader;
        skyM.CopyPropertiesFromMaterial(tod.skybox[0]);
        skyM.SetFloat("_CloudSpeed", 0.03f);
        EditorUtility.SetDirty(skyM);

        // 원반 머티리얼은 렌더러에 실제로 붙은 것을 쓴다 (경로 추정 금지 — 1차에서 해 원반이 안 움직였음)
        Material sunDisc = null, moonDisc = null;
        foreach (var o in tod.skyObjects)
        {
            if (o == null) continue;
            var r = o.GetComponent<Renderer>(); if (r == null) continue;
            sb.AppendLine("  disc " + o.name + " mat " + AssetDatabase.GetAssetPath(r.sharedMaterial) + " _Dir " + r.sharedMaterial.GetVector("_Dir"));
            if (o.name == "Sky_SunDusk") sunDisc = r.sharedMaterial;
            if (o.name == "Sky_Moon") moonDisc = r.sharedMaterial;
        }
        sb.AppendLine("discs: sun " + (sunDisc != null) + " moon " + (moonDisc != null) + "  moon _Color " + (moonDisc != null ? Fmt(moonDisc.GetColor("_Color")) : "-"));

        // ── 기존 ToD 끄기 (지우지 않음) ──
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tod);
        Undo.RecordObject(tod, "tod off"); tod.enabled = false;
        if (ub != null) { Undo.RecordObject(ub, "tod off"); ub.enabled = false; }
        if (tod.panel != null) tod.panel.SetActive(false);
        foreach (var o in tod.skyObjects) if (o != null) o.SetActive(o.name != "Sky_SunDawn");
        sb.AppendLine("old ToD disabled (proxy + UdonBehaviour). components on " + go.name + ": " + string.Join(", ", go.GetComponents<Component>().Select(c => c.GetType().Name)));
        if (tod.dialPointer != null)
            for (var tr = tod.dialPointer; tr != null; tr = tr.parent)
                sb.AppendLine("  dial chain " + tr.name + ": " + string.Join(", ", tr.GetComponents<Component>().Select(c => c.GetType().Name)));

        // ── DayCycle — 자기 루트 오브젝트에 ──
        // 🔴 TimeOfDay 에 같이 붙이면 네트워크 ID 기록(UdonBehaviour 1개)과 어긋나 빌드가 AssignSceneNetworkIDs 에서 멈춘다(2026-09-23 실측)
        var old = go.GetComponent<PyriteDayCycle>();
        if (old != null)
        {
            var ob = UdonSharpEditorUtility.GetBackingUdonBehaviour(old);
            Object.DestroyImmediate(old);
            if (ob != null) Object.DestroyImmediate(ob);
            sb.AppendLine("TimeOfDay 에서 DayCycle 제거 → 원래 구성 " + string.Join(", ", go.GetComponents<Component>().Select(c => c.GetType().Name)));
        }
        var host = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "DayCycle");
        if (host == null) { host = new GameObject("DayCycle"); Undo.RegisterCreatedObjectUndo(host, "day cycle"); }
        var cyc = host.GetComponent<PyriteDayCycle>();
        if (cyc == null)
        {
            try { cyc = UdonSharpUndo.AddComponent<PyriteDayCycle>(host); }
            catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb, false); return; }
        }
        sb.AppendLine("DayCycle host: " + string.Join(", ", host.GetComponents<Component>().Select(c => c.GetType().Name)));
        cyc.startHour = 21f; cyc.hourAtSync = 21f; cyc.dayMinutes = 12f; cyc.autoFlow = true;   // 2026-09-24 관리자: 자동 흐름 기본 cyc.syncStamp = 0;
        cyc.keyHour = keys.Select(k => k.hour).ToArray();
        cyc.keyCubeSet = keys.Select(k => k.cube).ToArray();
        cyc.sky = skyM;
        cyc.skyExposure = keys.Select(k => k.skyExp).ToArray();
        cyc.skyHaze = keys.Select(k => k.skyHaze).ToArray();
        cyc.skyMie = keys.Select(k => k.skyMie).ToArray();
        cyc.skyMieG = keys.Select(k => k.skyMieG).ToArray();
        cyc.skyCloud = keys.Select(k => k.skyCloud).ToArray();
        cyc.ambSky = keys.Select(k => k.ambSky).ToArray();
        cyc.ambEquator = keys.Select(k => k.ambEq).ToArray();
        cyc.ambGround = keys.Select(k => k.ambGr).ToArray();
        cyc.fogColor = keys.Select(k => k.fogCol).ToArray();
        cyc.fogDensity = keys.Select(k => k.fogDen).ToArray();
        cyc.reflIntensity = keys.Select(k => k.refl).ToArray();
        cyc.sun = tod.sun;
        cyc.lightColor = keys.Select(k => k.lightCol).ToArray();
        cyc.lightIntensity = keys.Select(k => k.lightInt).ToArray();
        cyc.sunDisc = sunDisc; cyc.moonDisc = moonDisc;
        cyc.sunDiscColor = keys.Select(k => k.sunDisc).ToArray();
        if (moonDisc != null) cyc.moonDiscColor = new Color(0.80f, 0.86f, 1.00f) * 1.35f;
        cyc.hideObjects = tod.skyObjects.Where(o => o != null && o.name == "Sky_SunDawn").ToArray();
        cyc.cliffRenderers = tod.cliffRenderers; cyc.cliffMat = tod.cliffMat[0];
        cyc.cliffColor = keys.Select(k => k.cliff).ToArray();
        cyc.waterRenderers = tod.waterRenderers; cyc.waterMat = tod.waterMat[0];
        cyc.waterBase = keys.Select(k => k.waterBase).ToArray();
        cyc.waterDeep = keys.Select(k => k.waterDeep).ToArray();
        cyc.flowerRenderers = tod.flowerRenderers; cyc.flowerMat = tod.flowerMat[0];
        cyc.flowerColor = keys.Select(k => k.flower).ToArray();
        cyc.shimmerMat = tod.shimmerMat; cyc.shimmerGain = keys.Select(k => k.shimmer).ToArray();
        // 반딧불은 황철석 결정 주변만 (2026-09-23 관리자). 꽃밭 반딧불(FF_P_TS_*)은 오브젝트를 끈다
        cyc.fireflies = tod.fireflies.Where(p => p != null && p.name.StartsWith("FF_Crystal_")).ToArray();
        foreach (var p in tod.fireflies.Where(p => p != null && !p.name.StartsWith("FF_Crystal_")))
        {
            if (p.transform.parent != null && p.transform.parent.name == "Fireflies") { p.transform.parent.gameObject.SetActive(false); }
            else p.gameObject.SetActive(false);
        }
        sb.AppendLine("fireflies: " + string.Join(", ", cyc.fireflies.Select(p => p.name)) + "  (꽃밭 반딧불 끔)");
        // 🔴 기준값은 상수 — 씬 값은 도구가 배수를 누적시켜 무너졌다(캠프 조명 0.01, 반딧불 0, 풀벌레 1e-33)
        cyc.ffBaseRate = cyc.fireflies.Select(p => 25.06f).ToArray();       // push 된 씬(노을 배수 1) 값
        cyc.ffBaseSize = cyc.fireflies.Select(p => 0.40f).ToArray();
        cyc.campBase = new[] { 7.86f, 10.13f, 7.59f, 0.70f };   // 2026-09-24 화로 9.82 → 7.86 (Z28b, 범위 4.2 m)                // Fire_Light, Fire_Light, 랜턴 Light, 부두 뿌리 (19:45 로그)
        cyc.ambBase = tod.ambience.Select(a => a == null ? 0f : a.name == "AMB_Water_Center" ? 0.30f : a.name == "AMB_Water_ShoreN" ? 0.42f : a.name == "AMB_Water_Dock" ? 0.40f : a.name == "AMB_Campfire" ? 0.55f : a.volume).ToArray();
        cyc.nightAmbBase = tod.nightAmbience.Select(a => a == null ? 0f : a.name == "AMB_N_Crickets_A" ? 0.38f : a.name == "AMB_N_Crickets_B" ? 0.34f : a.volume).ToArray();   // PyriteAmbienceTools 스펙
        sb.AppendLine("bases camp " + string.Join("/", cyc.campBase) + "  amb " + string.Join("/", cyc.ambBase) + "  night " + string.Join("/", cyc.nightAmbBase) + "  (lights " + string.Join(",", tod.campLights.Select(l => l ? l.name : "-")) + ")");
        cyc.ffColorA = keys.Select(k => k.ffA).ToArray(); cyc.ffColorB = keys.Select(k => k.ffB).ToArray();
        cyc.ffRateMul = keys.Select(k => k.ffRate).ToArray(); cyc.ffSizeMul = keys.Select(k => k.ffSize).ToArray();
        cyc.crystalMats = tod.crystalMats; cyc.crystalBaseEmission = tod.crystalBaseEmission; cyc.crystalBaseGloss = tod.crystalBaseGloss;
        cyc.crystalEmissionMul = keys.Select(k => k.crEm).ToArray();
        cyc.crystalMatcap = keys.Select(k => k.crMatcap).ToArray();
        cyc.crystalGlossMul = keys.Select(k => k.crGloss).ToArray();
        cyc.crystalMatcapTint = keys.Select(k => k.crTint).ToArray();
        cyc.crystalLights = tod.crystalLights; cyc.crystalLightIntensity = keys.Select(k => k.crLight).ToArray();
        cyc.crystalRim = keys.Select(k => k.crRim).ToArray();
        cyc.crystalSparkle = keys.Select(k => k.crSpk).ToArray();
        cyc.crystalGlint = keys.Select(k => k.crGlint).ToArray();
        cyc.crystalMcNormal = keys.Select(k => k.crMcN).ToArray();
        cyc.campLights = tod.campLights; cyc.campLightMul = keys.Select(k => k.camp).ToArray();
        cyc.ambience = tod.ambience; cyc.ambienceMul = keys.Select(k => k.amb).ToArray();
        cyc.nightAmbience = tod.nightAmbience; cyc.nightAmbienceMul = keys.Select(k => k.nightAmb).ToArray();
        cyc.nightMats = tod.nightMats;
        cyc.nightTint = keys.Select(k => k.nightTint).ToArray();
        cyc.campTint = keys.Select(k => k.campTint).ToArray();
        cyc.nightAmbSky = keys.Select(k => k.nAmbSky).ToArray();
        cyc.nightAmbGround = keys.Select(k => k.nAmbGr).ToArray();
        cyc.probes = tod.probes; cyc.probeCubes = tod.probeCubes;
        cyc.dialPointer = tod.dialPointer;
        var vols = Object.FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>(true);
        cyc.ppDay = vols.FirstOrDefault(v => v.name == "PostProcess_Day");
        cyc.ppNight = vols.FirstOrDefault(v => v.name == "PostProcess_Night");
        cyc.ppDayW = keys.Select(k => k.ppDay).ToArray();
        cyc.ppNightW = keys.Select(k => k.ppNight).ToArray();
        sb.AppendLine("pp volumes: day " + (cyc.ppDay != null) + " night " + (cyc.ppNight != null));

        // 기준값(캠프 조명·볼륨)은 노을 상태에서 잡혀야 한다 — 위에서 tod.Apply(0) 했지만 배수 1 이 아닌 값이 있을 수 있어 기록
        sb.AppendLine("campLights base: " + string.Join(", ", tod.campLights.Select(l => l == null ? "-" : l.intensity.ToString("0.00"))) + "  (노을 배수 " + tod.campLightMul[0] + ")");

        cyc.ResetCache();
        cyc.EvaluateAt(EDITOR_HOUR);
        UdonSharpEditorUtility.CopyProxyToUdon(cyc);
        EditorUtility.SetDirty(cyc);

        // 임시 다이얼 → 새 사이클
        foreach (var d in Object.FindObjectsOfType<PyriteDial>(true))
        {
            Undo.RecordObject(d, "dial"); d.cycle = cyc;
            UdonSharpEditorUtility.CopyProxyToUdon(d); EditorUtility.SetDirty(d);
            sb.AppendLine("dial " + d.name + " → DayCycle (+" + cyc.stepHours + "h)");
        }

        // 궤도 표
        sb.AppendLine("orbit (hour: sunEl/sunAz  moonEl/moonAz):");
        for (float h = 0; h < 24f; h += 1f)
            sb.AppendLine(string.Format("  {0:00}:00  sun {1,6:0.0}/{2,5:0}  moon {3,6:0.0}/{4,5:0}", h, cyc.SunEl(h), cyc.SunAz(h), cyc.MoonEl(h), cyc.MoonAz(h)));
        sb.AppendLine(string.Format("  18:20  sun {0:0.0}/{1:0}  (노을 원반 9.8/184 목표)", cyc.SunEl(EDITOR_HOUR), cyc.SunAz(EDITOR_HOUR)));
        sb.AppendLine(string.Format("  light at 18:20 euler {0}  intensity {1:0.00}", tod.sun.transform.eulerAngles, tod.sun.intensity));

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb, false);
    }

    [MenuItem("Tools/Pyrite/Z18c. Day Cycle Revert", false, 42)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z18c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (tod == null) { sb.AppendLine("ToD 없음"); Flush(sb, true); return; }
        foreach (var cyc in Object.FindObjectsOfType<PyriteDayCycle>(true))
        {
            var cb = UdonSharpEditorUtility.GetBackingUdonBehaviour(cyc);
            var hostGo = cyc.gameObject;
            Object.DestroyImmediate(cyc);
            if (cb != null) Object.DestroyImmediate(cb);
            if (hostGo != tod.gameObject && hostGo.name == "DayCycle") Object.DestroyImmediate(hostGo);
        }
        foreach (var d in Object.FindObjectsOfType<PyriteDial>(true)) { d.cycle = null; UdonSharpEditorUtility.CopyProxyToUdon(d); EditorUtility.SetDirty(d); }
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(tod);
        tod.enabled = true; if (ub != null) ub.enabled = true;
        var fr = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (fr != null) fr.SetValue(tod, false);
        tod.index = 0; tod.Apply();
        // 원반 머티리얼 원래 값 (Z3 기준)
        foreach (var o in tod.skyObjects)
        {
            if (o == null) continue; var r = o.GetComponent<Renderer>(); if (r == null) continue; var m = r.sharedMaterial;
            if (o.name == "Sky_SunDusk") { m.SetColor("_Color", new Color(1.00f, 0.56f, 0.26f) * 3.0f); var d = PyriteSkyDiscTools.Dir(9.77f, 184f); m.SetVector("_Dir", new Vector4(d.x, d.y, d.z, 0)); }
            if (o.name == "Sky_Moon") { m.SetColor("_Color", new Color(0.80f, 0.86f, 1.00f) * 1.35f); var d = PyriteSkyDiscTools.Dir(22f, 205f); m.SetVector("_Dir", new Vector4(d.x, d.y, d.z, 0)); }
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("DayCycle 제거, ToD 복구(노을), 원반 복구. RESULT: DONE");
        Flush(sb, true);
    }

    // ── 24시간 렌더 ─────────────────────────────────────────
    static readonly float[] Hours = { 0f, 5f, 5.5f, 5.833f, 6.2f, 7.5f, 9.5f, 12f, 15f, 17f, EDITOR_HOUR, 18.667f, 19f, 19.333f, 19.667f, 20f, 20.5f, 21f };
    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="camp_lake",   eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f },
        new V{ n="far_to_camp", eye=new Vector3(  5.0f, 1.7f, -40.0f), look=new Vector3( -5.0f, 6.0f, 78.0f), fov=70f },
        new V{ n="tent_side",   eye=new Vector3( -4.0f, 1.7f, 58.0f), look=new Vector3(-16.0f, 1.5f, 56.0f), fov=70f },
        new V{ n="crystal_r",   eye=new Vector3(-40.0f, 1.7f, 42.0f), look=new Vector3(-52.0f, 3.0f, 26.0f), fov=50f },
    };

    [MenuItem("Tools/Pyrite/Z18d. Day Cycle Sweep Render", false, 43)]
    public static void Sweep()
    {
        var sb = new StringBuilder("[Z18d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc == null) { sb.AppendLine("DayCycle 없음"); Flush(sb, true); return; }
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/cycle/");
        var t = Terrain.activeTerrain;
        // 에디터에선 파티클이 멈춰 있어 반딧불 하나가 하늘의 고정 빛점처럼 찍힌다(Z18e 확인) → 렌더 동안 끈다
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        try
        {
            cyc.ResetCache();
            foreach (var h in Hours)
            {
                cyc.EvaluateAt(h);
                string hs = string.Format("{0:00}{1:00}", (int)h, Mathf.RoundToInt((h - (int)h) * 60f));
                sb.AppendLine(string.Format("  {0}  sun {1,6:0.0}/{2,4:0}  moon {3,6:0.0}  light int {4:0.000} euler {5}  fog {6} {7:0.0000}  sky exp {8:0.00}",
                    hs, cyc.sunElNow, cyc.SunAz(h), cyc.moonElNow, cyc.sun.intensity, cyc.sun.transform.eulerAngles, RenderSettings.fog, RenderSettings.fogDensity, cyc.sky.GetFloat("_Exposure")));
                foreach (var v in Views)
                {
                    var eye = v.eye;
                    if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
                    Shot(cam, eye, v.look, v.fov, string.Format("Assets/_preview/cycle/h{0}_{1}.png", hs, v.n));
                }
            }
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.EvaluateAt(EDITOR_HOUR);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb, true);
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static string Fmt(Color c) => string.Format("({0:0.###},{1:0.###},{2:0.###})", c.r, c.g, c.b);
    static void Flush(StringBuilder sb, bool append) { Directory.CreateDirectory("Logs"); if (append) File.AppendAllText(LOG, sb.ToString()); else File.WriteAllText(LOG, sb.ToString()); }
}
#endif
