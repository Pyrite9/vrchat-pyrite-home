using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// 밤 어둠 + 황철석 금속
//
//  V1 : 황철석 머티리얼 — 창백한 황동 알베도 / metallic 1 / 높은 smoothness / 조선(줄무늬) 노멀맵 / emission 거의 제거
//  V2 : 밤 어둠 — 지형·부두를 Pyrite/*Night 셰이더로 바꾸고 프리셋별 색조 머티리얼 생성, 꽃 밤 머티리얼 교체,
//       결정 포인트 라이트 끄기, 노을 태양 값 복구, 월드 전체 리플렉션 프로브 추가 → TimeOfDay 배선
//  V3 : 확인 렌더 (노을/밤 전경, 황철석 근접)
//
//  J. Setup Time Of Day 도 끝에서 Wire() 를 부른다 — J 를 다시 돌려도 이 값들이 유지된다.
public static class PyriteNightTools
{
    const string MAT = "Assets/Materials/";
    const string TEX = "Assets/TerrainAssets/";
    // 면 노멀 — 모서리 베벨 + 볼록한 면 + 가는 조선. 결정 메시는 전부 면마다 UV 0~1 이라 타일링 1 로 모서리가 정확히 맞는다
    const string STRIATION = TEX + "T_PyriteFace_N.png";
    const string MATCAP    = TEX + "T_PyriteMatCap.png";

    // 캠프 불빛 웅덩이 — 이 반경 안쪽은 구운 조명을 덜 깎는다
    static readonly Vector4 CAMP_C = new Vector4(-10.5f, 0f, 54.5f, 0f);
    const float CAMP_R0 = 8f, CAMP_R1 = 16f;

    // 프리셋별 색조 [노을, 밤, 새벽] — 구운 간접광(라이트맵·프로브)에만 곱해진다
    static readonly Color[] NIGHT_T = { Color.white, new Color(0.13f, 0.15f, 0.22f), new Color(0.86f, 0.84f, 0.94f) };
    static readonly Color[] CAMP_T  = { Color.white, new Color(0.62f, 0.50f, 0.40f), new Color(0.92f, 0.90f, 0.97f) };
    // [E1] 밤 하늘빛 (선형값) — 라이트맵엔 간접광만 있어서 색조만 곱하면 물가 모래가 새까매진다
    public static Color[] NIGHT_AMB_SKY    = { Color.black, new Color(0.030f, 0.044f, 0.095f), Color.black };
    public static Color[] NIGHT_AMB_GROUND = { Color.black, new Color(0.009f, 0.012f, 0.022f), Color.black };
    
    // 원래 노을 태양 (buildspec: Directional FFB070 / 1.25)
    static readonly Color SUN_DUSK_C = new Color(1.000f, 0.690f, 0.439f);
    const float SUN_DUSK_I = 1.25f;

    public static readonly float[] CRYSTAL_EMISSION_MUL = { 0.00f, 0.00f, 0.00f };   // [A] 밤 노란 자체발광 제거
    public static readonly float[] CRYSTAL_LIGHT        = { 0f, 0f, 0f };
    public static readonly float[] CRYSTAL_MATCAP       = { 1.00f, 0.35f, 0.75f };   // 밤엔 달빛 받은 정도로만
    // [A] MatCap 환경색 — 밤엔 하늘색을 곱해 노랑×청 = 차분한 청동
    public static readonly Color[] CRYSTAL_MATCAP_TINT  = { Color.white, new Color(0.50f, 0.62f, 1.00f), Color.white };
    // [B] 밤엔 smoothness 를 낮춰 반딧불 점광원 하이라이트가 면에 넓게 번지게 (0.86 → 0.62)
    public static readonly float[] CRYSTAL_GLOSS_MUL    = { 1.00f, 0.72f, 1.00f };

    // 꽃 밤 — 에셋 팩의 _Night 는 emission 이 켜진 "빛나는 꽃"이라 쓰지 않는다. Day 복사본을 어둡게.
    static readonly Color FLOWER_NIGHT = new Color(0.145f, 0.165f, 0.240f, 1f);

    struct PM
    {
        public string name; public Color albedo; public float metal, smooth, bump;
        public PM(string n, Color a, float m, float s, float b) { name = n; albedo = a; metal = m; smooth = s; bump = b; }
    }
    // 실제 황철석은 금보다 채도가 낮은 "창백한 황동". 채도 높은 노랑은 금속이 아니라 단무지로 읽힌다.
    static readonly PM[] PYRITE =
    {
        new PM("M_Pyrite",         new Color(0.80f, 0.71f, 0.46f), 1.0f, 0.86f, 0.9f),
        new PM("M_Pyrite_Cliff",   new Color(0.66f, 0.58f, 0.38f), 1.0f, 0.78f, 0.9f),
        new PM("M_Pyrite_Tarnish", new Color(0.66f, 0.46f, 0.30f), 1.0f, 0.62f, 0.9f),
        new PM("M_Pyrite_Iris",    new Color(0.66f, 0.60f, 0.70f), 1.0f, 0.88f, 0.8f),
    };

    // ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Pyrite/V1. Pyrite Metal", false, 300)]
    public static void PyriteMetal()
    {
        ApplyPyriteMetal();
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod != null)
        {
            tod.crystalMatcap         = (float[])CRYSTAL_MATCAP.Clone();
        tod.crystalMatcapTint     = (Color[])CRYSTAL_MATCAP_TINT.Clone();
            tod.crystalEmissionMul    = (float[])CRYSTAL_EMISSION_MUL.Clone();
            tod.crystalLightIntensity = (float[])CRYSTAL_LIGHT.Clone();
            tod.index = 0; tod.Apply();
            UdonSharpEditorUtility.CopyProxyToUdon(tod);
            EditorUtility.SetDirty(tod);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void ApplyPyriteMetal()
    {
        var ti = AssetImporter.GetAtPath(STRIATION) as TextureImporter;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
        { ti.textureType = TextureImporterType.NormalMap; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
        var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(STRIATION);

        // MatCap — 밉맵 끄고 Clamp. 밉맵이 있으면 멀리서 원 가장자리가 번져 테두리가 생긴다.
        var mi = AssetImporter.GetAtPath(MATCAP) as TextureImporter;
        if (mi != null && (mi.mipmapEnabled || mi.wrapMode != TextureWrapMode.Clamp))
        { mi.mipmapEnabled = false; mi.wrapMode = TextureWrapMode.Clamp; mi.sRGBTexture = true; mi.SaveAndReimport(); }
        var mcap = AssetDatabase.LoadAssetAtPath<Texture2D>(MATCAP);

        var sh = Shader.Find("Pyrite/PyriteMetal");
        if (sh == null) { Debug.LogError("[METAL] Pyrite/PyriteMetal 셰이더 없음 — 컴파일 에러 확인"); return; }

        int n = 0;
        foreach (var p in PYRITE)
        {
            var m = FindMat(p.name);
            if (m == null) { Debug.LogWarning("[METAL] " + p.name + " 없음"); continue; }
            Undo.RecordObject(m, "pyrite metal");
            var emis = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            if (m.shader != sh) m.shader = sh;
            m.SetColor("_Color", p.albedo);
            m.SetFloat("_Metallic", p.metal);
            m.SetFloat("_Glossiness", p.smooth);
            if (nrm != null) m.SetTexture("_BumpMap", nrm);
            m.SetFloat("_BumpScale", p.bump);
            m.SetFloat("_MatCapNormal", 1.0f);
            m.SetFloat("_StriationTile", 1f);
            if (mcap != null) m.SetTexture("_MatCap", mcap);
            m.SetFloat("_MatCapStrength", CRYSTAL_MATCAP[0]);
            m.SetFloat("_MatCapBoost", 1.5f);
            m.SetColor("_MatCapTint", Color.white);
            m.SetFloat("_SpecNoTint", 1f);   // [A] 반사는 프리셋별 큐브맵 그대로
            m.SetColor("_EmissionColor", emis);
            m.SetColor("_NightTint", Color.white);
            m.SetColor("_CampTint",  Color.white);
            m.SetVector("_CampCenter", CAMP_C);
            m.SetFloat("_CampR0", CAMP_R0);
            m.SetFloat("_CampR1", CAMP_R1);
            // 라이트맵에 굽지 않는다 — MatCap 은 보는 방향에 따라 변하는 값이라 구우면 안 된다
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            n++;
        }
        Debug.Log("[METAL] 황철석 " + n + "개 → Pyrite/PyriteMetal (MatCap 노을 1.0 · 밤 0.35 하늘색 · 새벽 0.75 / 반사는 색조 제외)");
    }

    // ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Pyrite/V2. Night Darkness", false, 301)]
    public static void NightDarkness()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) { Debug.LogError("[NIGHT] PyriteTimeOfDay 없음"); return; }
        EnsureWorldProbe();
        Wire(tod);
        tod.index = 0;
        tod.Apply();
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[NIGHT] 완료 — 다음: T. Bake Reflection Sets (새 프로브 RP_World 포함, 밤 지형이 반사에 들어가게)");
    }

    /// J 와 V2 가 공유하는 배선
    public static void Wire(PyriteTimeOfDay tod)
    {
        ApplyPyriteMetal();

        // 지형 · 부두 — 셰이더를 Pyrite/*Night 로 바꾸고 머티리얼 하나를 Udon 이 SetColor 로 조절한다
        //   (terrain.materialTemplate 대입은 Udon 차단 — 교체 방식은 못 쓴다)
        var nm = new System.Collections.Generic.List<Material>();
        var terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.materialTemplate != null)
        {
            var m = PrepNight(terrain.materialTemplate, "Pyrite/TerrainNight");
            if (m != null) nm.Add(m);
            terrain.basemapDistance = Mathf.Max(terrain.basemapDistance, 1000f);   // 베이스맵 셰이더엔 색조가 없다
        }
        else Debug.LogWarning("[NIGHT] 지형 머티리얼 없음");

        var dock = FindAnywhere("DockMesh");
        var dmr = dock ? dock.GetComponentInChildren<MeshRenderer>() : null;
        if (dmr != null && dmr.sharedMaterial != null)
        {
            var m = PrepNight(dmr.sharedMaterial, "Pyrite/StandardNight");
            if (m != null) nm.Add(m);
        }
        // 황철석도 정적(라이트맵)이라 같은 색조를 받는다
        foreach (var p in PYRITE) { var pm = FindMat(p.name); if (pm != null && pm.HasProperty("_NightTint")) nm.Add(pm); }
        tod.nightMats = nm.ToArray();
        tod.nightTint = (Color[])NIGHT_T.Clone();
        tod.campTint  = (Color[])CAMP_T.Clone();
        tod.nightAmbSky    = (Color[])NIGHT_AMB_SKY.Clone();
        tod.nightAmbGround = (Color[])NIGHT_AMB_GROUND.Clone();

        // 꽃 밤 머티리얼 — Day 복사본을 어둡게
        var fDay = FindMat("M_TS_Nemophila_Day");
        if (fDay != null && tod.flowerMat != null && tod.flowerMat.Length >= 2)
        {
            var fN = CopyMat(fDay, "M_TS_Nemophila_NightDark");
            if (fN.HasProperty("_MainColor")) fN.SetColor("_MainColor", FLOWER_NIGHT);
            fN.DisableKeyword("ENABLE_EMISSION");
            EditorUtility.SetDirty(fN);
            tod.flowerMat[1] = fN;
        }

        // 결정
        tod.crystalMatcap         = (float[])CRYSTAL_MATCAP.Clone();
        tod.crystalMatcapTint     = (Color[])CRYSTAL_MATCAP_TINT.Clone();
        tod.crystalGlossMul       = (float[])CRYSTAL_GLOSS_MUL.Clone();
        if (tod.crystalMats != null)
            tod.crystalBaseGloss = tod.crystalMats.Select(cm =>
            {
                if (cm == null) return 0.8f;
                foreach (var p in PYRITE) if (p.name == cm.name) return p.smooth;
                return cm.HasProperty("_Glossiness") ? cm.GetFloat("_Glossiness") : 0.8f;
            }).ToArray();
        tod.crystalEmissionMul    = (float[])CRYSTAL_EMISSION_MUL.Clone();
        tod.crystalLightIntensity = (float[])CRYSTAL_LIGHT.Clone();

        // 노을 태양 복구
        if (tod.sunIntensity != null && tod.sunIntensity.Length > 0) tod.sunIntensity[0] = SUN_DUSK_I;
        if (tod.sunColor     != null && tod.sunColor.Length     > 0) tod.sunColor[0]     = SUN_DUSK_C;

        Debug.Log(string.Format("[NIGHT] 배선 — 색조 머티리얼 {0}개({1}) / 꽃 밤 {2} / 결정 라이트 0 / 노을 태양 {3} {4}",
            tod.nightMats.Length, string.Join(", ", tod.nightMats.Select(x => x.name)),
            tod.flowerMat != null && tod.flowerMat.Length > 1 && tod.flowerMat[1] ? tod.flowerMat[1].name : "X",
            SUN_DUSK_I, ColorUtility.ToHtmlStringRGB(SUN_DUSK_C)));
    }

    /// 머티리얼 셰이더를 바꾸고 캠프 반경·기본 색조(노을 = 흰색)를 넣는다
    static Material PrepNight(Material m, string shaderName)
    {
        var sh = Shader.Find(shaderName);
        if (sh == null) { Debug.LogError("[NIGHT] 셰이더 없음: " + shaderName + " — 컴파일 에러 확인"); return null; }
        Undo.RecordObject(m, "night shader");
        if (m.shader != sh) m.shader = sh;
        m.SetColor("_NightTint", Color.white);
        m.SetColor("_CampTint",  Color.white);
        m.SetVector("_CampCenter", CAMP_C);
        m.SetFloat("_CampR0", CAMP_R0);
        m.SetFloat("_CampR1", CAMP_R1);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material CopyMat(Material src, string name)
    {
        var path = MAT + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        else { m.shader = src.shader; m.CopyPropertiesFromMaterial(src); m.shaderKeywords = src.shaderKeywords; }
        m.name = name;
        return m;
    }

    static void EnsureWorldProbe()
    {
        if (FindAnywhere("RP_World") != null) return;
        var go = new GameObject("RP_World");
        Undo.RegisterCreatedObjectUndo(go, "world probe");
        go.transform.position = new Vector3(0f, 20f, -14f);
        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Baked;
        p.size = new Vector3(240f, 90f, 240f);
        p.center = Vector3.zero;
        p.importance = 0;           // 기존 프로브가 있는 곳에선 그쪽이 이긴다
        p.boxProjection = false;
        p.resolution = 128;
        p.hdr = true;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ReflectionProbeStatic);
        Debug.Log("[NIGHT] RP_World 추가 — 절벽 쪽 결정이 기본(노을) 스카이박스 반사로 떨어지지 않게");
    }

    // ─────────────────────────────────────────────────────────────
    [MenuItem("Tools/Pyrite/V3. Capture Night Check", false, 302)]
    public static void Capture()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) return;
        Directory.CreateDirectory("Assets/_preview/");
        var t = Terrain.activeTerrain;
        float G(float x, float z) => t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 1.8f;

        // 관리자 스크린샷과 비슷한 시점 — 타프 뒤에서 호수 쪽
        var eyeO  = new Vector3(-8.0f, G(-8f, 66f) + 6.0f, 66.0f);
        var lookO = new Vector3(-8.0f, 0.0f, 32.0f);

        // 가장 큰 황철석 덩어리
        Bounds best = new Bounds(); float bv = 0f;
        var crystals = FindAnywhere("Crystals");
        if (crystals != null)
            foreach (Transform c in crystals.transform)
            {
                // PyriteCliff 는 절벽 전체에 박힌 결정 묶음이라 bounds 가 월드만 하다 — 랜드마크만 본다
                if (!c.name.StartsWith("Landmark")) continue;
                Bounds b = new Bounds(); bool has = false;
                foreach (var r in c.GetComponentsInChildren<Renderer>(true))
                { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
                if (!has) continue;
                float v = b.size.x * b.size.y * b.size.z;
                if (v > bv) { bv = v; best = b; }
            }
        Vector3 toLake = new Vector3(0f - best.center.x, 0f, -14f - best.center.z);
        if (toLake.sqrMagnitude < 1e-3f) toLake = Vector3.forward;
        toLake.Normalize();
        var eyeP  = best.center + toLake * (best.extents.magnitude * 2.2f) + Vector3.up * best.extents.y * 0.25f;
        var lookP = best.center;

        // 캠프에서 가장 가까운 절벽 결정 — 관리자가 실제로 보게 되는 것
        Vector3 camp = new Vector3(-10.5f, G(-10.5f, 51.5f) + 1.6f, 51.5f);
        Renderer near = null; float nd = float.MaxValue;
        var cliffC = crystals ? crystals.transform.Find("PyriteCliff") : null;
        if (cliffC != null)
            foreach (var r in cliffC.GetComponentsInChildren<Renderer>(true))
            {
                float d = (r.bounds.center - camp).sqrMagnitude;
                if (d < nd) { nd = d; near = r; }
            }
        Vector3 eyeC = camp, lookC = camp + Vector3.forward;
        if (near != null)
        {
            Vector3 dir = (camp - near.bounds.center); dir.y = 0f; dir.Normalize();
            eyeC  = near.bounds.center + dir * (near.bounds.extents.magnitude * 3.0f + 2.0f) + Vector3.up * 0.5f;
            lookC = near.bounds.center;
        }

        for (int i = 0; i < 2; i++)
        {
            tod.index = i; tod.Apply();
            Shot(eyeO, lookO, "Assets/_preview/overview_" + i + ".png", 62f);
            Shot(eyeP, lookP, "Assets/_preview/pyrite_" + i + ".png", 40f);
            Shot(eyeC, lookC, "Assets/_preview/pyriteCliff_" + i + ".png", 45f);
        }
        tod.index = 0; tod.Apply();
        AssetDatabase.Refresh();
        Debug.Log("[NIGHT] 확인 렌더 — overview_0/1, pyrite_0/1   (황철석 " + best.center.ToString("F1") + ")");
    }

    // ── 도우미 ────────────────────────────────────────────────────
    static Material FindMat(string name)
    {
        foreach (var g in AssetDatabase.FindAssets("t:Material " + name))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            if (Path.GetFileNameWithoutExtension(p) == name) return AssetDatabase.LoadAssetAtPath<Material>(p);
        }
        return null;
    }

    static GameObject FindAnywhere(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var tr in r.GetComponentsInChildren<Transform>(true))
                if (tr.name == name) return tr.gameObject;
        return null;
    }

    static void Shot(Vector3 eye, Vector3 look, string path, float fov)
    {
        var go = new GameObject("__shot");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 900f;
        cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = true;
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
