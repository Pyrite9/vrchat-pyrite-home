// 시간대 스위처 구축 — 에디터 전용
//  머티리얼 변형본 생성 → 씬 오브젝트 수집 → PyriteTimeOfDay 컴포넌트에 전부 꽂는다.
//  다시 눌러도 같은 결과가 나오도록 만들었다.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteTimeOfDaySetup
{
    const string MAT = "Assets/Materials/";
    const string TEX = "Assets/TerrainAssets/";
    const string TS  = "Assets/つきのすとあ/FlowersGrassland/";

    // ── 머티리얼 복사 + 색 곱 ──────────────────────────────────────────
    static Material Variant(Material src, string outName, Color mul, float smoothMul)
    {
        if (src == null) { Debug.LogError("[ToD] 원본 머티리얼 없음: " + outName); return null; }
        var path = MAT + outName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        m.shader = src.shader;
        m.CopyPropertiesFromMaterial(src);

        var sh = src.shader;
        int n = ShaderUtil.GetPropertyCount(sh);
        var touched = new List<string>();
        for (int i = 0; i < n; i++)
        {
            var pn = ShaderUtil.GetPropertyName(sh, i);
            var pt = ShaderUtil.GetPropertyType(sh, i);
            if (pt == ShaderUtil.ShaderPropertyType.Color && pn.ToLower().Contains("color"))
            {
                var c = src.GetColor(pn);
                m.SetColor(pn, new Color(c.r * mul.r, c.g * mul.g, c.b * mul.b, c.a));
                touched.Add(pn);
            }
            else if (pt == ShaderUtil.ShaderPropertyType.Range && pn == "_Glossiness")
                m.SetFloat(pn, Mathf.Clamp01(src.GetFloat(pn) * smoothMul));
        }
        EditorUtility.SetDirty(m);
        Debug.Log("[ToD] 변형 생성 " + outName + " ← " + src.name + "  색 속성 " + touched.Count + "개: "
                  + string.Join(", ", touched));
        return m;
    }

    static Material MakePanoSkybox(string outName, string texName, float exposure, Color tint)
    {
        var path = MAT + outName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Skybox/Panoramic");
        if (sh == null) { Debug.LogError("[ToD] Skybox/Panoramic 없음"); return null; }
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        var tp = TEX + texName + ".png";
        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
        if (ti != null && (ti.mipmapEnabled || ti.wrapMode != TextureWrapMode.Repeat))
        { ti.mipmapEnabled = false; ti.wrapMode = TextureWrapMode.Repeat; ti.maxTextureSize = 4096; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        if (tex == null) Debug.LogError("[ToD] 텍스처 없음: " + tp);
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Tint", tint);
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_Rotation", 0f);
        m.SetFloat("_Mapping", 1f);          // Latitude Longitude
        m.SetFloat("_ImageType", 0f);        // 360
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SkyVariant(Material src, string outName, float elevation, float exposure,
                               float mie, float haze, float cloud)
    {
        var path = MAT + outName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(src); AssetDatabase.CreateAsset(m, path); }
        m.shader = src.shader;
        m.CopyPropertiesFromMaterial(src);
        m.SetFloat("_SunElevation", elevation);
        m.SetFloat("_Exposure", exposure);
        m.SetFloat("_MieStrength", mie);
        m.SetFloat("_HorizonHaze", haze);
        m.SetFloat("_CloudCoverage", cloud);
        EditorUtility.SetDirty(m);
        return m;
    }

    // 🔴 "현재 씬 상태"를 노을 원본으로 읽으면 안 된다.
    //    밤 프리셋이 적용된 상태에서 Setup을 돌리면 노을 원본이 밤 머티리얼로 덮인다(실제로 당했다).
    //    원본은 에셋 이름으로 고정한다. 못 찾을 때만 씬 값을 쓰되, _Night/_Dawn 변형이면 거부한다.
    static Material BaseMat(string assetName, Material sceneFallback)
    {
        foreach (var g in AssetDatabase.FindAssets("t:Material " + assetName))
        {
            var path = AssetDatabase.GUIDToAssetPath(g);
            if (System.IO.Path.GetFileNameWithoutExtension(path) != assetName) continue;
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m != null) { Debug.Log("[ToD] 원본 " + assetName + " ← " + path); return m; }
        }
        if (sceneFallback != null &&
            (sceneFallback.name.EndsWith("_Night") || sceneFallback.name.EndsWith("_Dawn")))
        {
            Debug.LogError("[ToD] 원본 " + assetName + " 을 못 찾았고 씬 값이 변형본(" + sceneFallback.name
                           + ")이다. 프리셋 0으로 되돌린 뒤 다시 실행할 것");
            return null;
        }
        Debug.LogWarning("[ToD] 원본 " + assetName + " 못 찾음 — 씬 값 사용");
        return sceneFallback;
    }


    static Material UnlitPanel(string name, string texName)
    {
        var path = MAT + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find("Unlit/Transparent");
        if (sh == null) { Debug.LogError("[ToD] Unlit/Transparent 없음"); return null; }
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
        m.shader = sh;
        var tp = TEX + texName + ".png";
        var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
        if (ti != null && (ti.alphaIsTransparency == false || ti.wrapMode != TextureWrapMode.Clamp))
        { ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport(); }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
        if (tex == null) Debug.LogError("[ToD] 패널 텍스처 없음: " + tp);
        m.mainTexture = tex;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material BrassMat()
    {
        var path = MAT + "M_Brass.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", new Color(0.62f, 0.50f, 0.22f));
        m.SetFloat("_Metallic", 0.90f);
        m.SetFloat("_Glossiness", 0.62f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name,
                           Vector3 localPos, Vector3 localScale, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        Undo.RegisterCreatedObjectUndo(go, "prim");
        go.name = name;
        var c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = localScale;
        var r = go.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    [MenuItem("Tools/Pyrite/J. Setup Time Of Day &#1")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();

        // ── 씬에서 대상 모으기 ──
        GameObject flora = null, lightFx = null, camp = null, water = null, cliffs = null;
        foreach (var r in scene.GetRootGameObjects())
        {
            if (r.name == "FlowerField") flora = r;
            else if (r.name == "LightFX") lightFx = r;
            else if (r.name == "Camp") camp = r;
            else if (r.name == "Water") water = r;
            else if (r.name == "PyriteCliffs_Visual") cliffs = r;
        }
        if (flora == null || lightFx == null || water == null || cliffs == null)
        { Debug.LogError("[ToD] 씬 오브젝트 부족 — FlowerField/LightFX/Water/PyriteCliffs_Visual 확인"); return; }

        var cliffR = cliffs.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
        var waterR = water.GetComponents<MeshRenderer>().Cast<Renderer>()
                     .Concat(water.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>()).Distinct().ToArray();
        var floraR = flora.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
        var ff = lightFx.GetComponentsInChildren<ParticleSystem>(true)
                 .Where(p => p.name.StartsWith("FF_")).ToArray();
        var campL = camp != null ? camp.GetComponentsInChildren<Light>(true) : new Light[0];

        Light sun = null;
        foreach (var l in Object.FindObjectsOfType<Light>(true))
            if (l.type == LightType.Directional) sun = l;
        if (sun == null) { Debug.LogError("[ToD] Directional Light 없음"); return; }

        var srcSky   = BaseMat("M_Sky_PyriteDusk", RenderSettings.skybox);
        var srcCliff = BaseMat("M_Bedrock",  cliffR.Length > 0 ? cliffR[0].sharedMaterial : null);
        var srcWater = BaseMat("M_LakeWater", waterR.Length > 0 ? waterR[0].sharedMaterial : null);
        var srcFlower= BaseMat("M_TS_Nemophila_Day", floraR.Length > 0 ? floraR[0].sharedMaterial : null);
        if (srcSky == null || srcCliff == null || srcWater == null || srcFlower == null) return;
        var shimmer = AssetDatabase.LoadAssetAtPath<Material>(MAT + "M_WaterShimmer.mat");

        // ── 변형 머티리얼 ──
        var skyNight = MakePanoSkybox("M_Sky_PyriteNight", "T_Sky_PyriteNight", 0.50f, new Color(0.88f,0.92f,1.0f));
        var skyDawn  = SkyVariant(srcSky, "M_Sky_PyriteDawn", 0.045f, 1.05f, 1.9f, 2.7f, 0.30f);

        var cliffNight = Variant(srcCliff, "M_Bedrock_Night", new Color(0.30f, 0.34f, 0.52f), 1.0f);
        var cliffDawn  = Variant(srcCliff, "M_Bedrock_Dawn",  new Color(0.88f, 0.84f, 0.96f), 1.0f);
        var waterNight = Variant(srcWater, "M_LakeWater_Night", new Color(0.28f, 0.34f, 0.58f), 1.0f);
        var waterDawn  = Variant(srcWater, "M_LakeWater_Dawn",  new Color(0.90f, 0.86f, 1.00f), 1.0f);

        // 꽃은 벤더가 제공하는 Night 머티리얼을 쓴다 (Flower 05 = Nemophila)
        var vendorNight = AssetDatabase.LoadAssetAtPath<Material>(TS + "Materials/T_Flower_05_Night.mat");
        Material flowerNight = null;
        if (vendorNight != null)
        {
            var path = MAT + "M_TS_Nemophila_Night.mat";
            flowerNight = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (flowerNight == null) { flowerNight = new Material(vendorNight); AssetDatabase.CreateAsset(flowerNight, path); }
            flowerNight.shader = vendorNight.shader;
            flowerNight.CopyPropertiesFromMaterial(vendorNight);
            // 낮 머티리얼에 했던 것과 같은 보정 — 벤더 기본값이 라이트프로브를 꺼둔다
            flowerNight.SetFloat("_EnaLgtPrb", 1f); flowerNight.EnableKeyword("ENABLE_LIGHTPROBE");
            flowerNight.SetFloat("_EnaSha", 1f);    flowerNight.EnableKeyword("ENABLE_SHADOW");
            flowerNight.enableInstancing = true;
            EditorUtility.SetDirty(flowerNight);
            Debug.Log("[ToD] 꽃 밤 머티리얼 ← " + vendorNight.name);
        }
        else Debug.LogWarning("[ToD] 벤더 Night 머티리얼 없음 — 낮 것을 그대로 쓴다");
        AssetDatabase.SaveAssets();


        // ── 황철석 발광 ────────────────────────────────────────────────
        // 이미션을 켜고 GI를 Baked로 둔다. 베이크해야 실제로 주변을 밝힌다.
        // (베이크 후에는 구워진 기여분이 고정되므로, 밤의 추가 밝기는 실시간 포인트 라이트가 맡는다)
        var cNames = new[] { "M_Pyrite", "M_Pyrite_Cliff", "M_Pyrite_Tarnish", "M_Pyrite_Iris" };
        var cBase  = new[] { new Color(0.85f,0.70f,0.30f), new Color(0.80f,0.66f,0.28f),
                             new Color(0.62f,0.40f,0.22f), new Color(0.58f,0.52f,0.72f) };
        var cMats = new List<Material>(); var cEm = new List<Color>();
        for (int k = 0; k < cNames.Length; k++)
        {
            var m = BaseMat(cNames[k], null);
            if (m == null) continue;
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            m.SetColor("_EmissionColor", cBase[k] * 0.16f);   // 베이크 기준 = 노을값.
            // 0.55는 알베도와 맞먹어서 결정이 형태 없는 노란 덩어리가 됐다(실측). 속에서 은은히 비치는 정도로.
            EditorUtility.SetDirty(m);
            cMats.Add(m); cEm.Add(cBase[k]);
            Debug.Log("[ToD] 발광 켬 " + m.name);
        }

        // 결정 군집 위치에 실시간 포인트 라이트 (밤에만 켠다)
        GameObject lightRoot = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "CrystalLights") lightRoot = r;
        if (lightRoot != null) Undo.DestroyObjectImmediate(lightRoot);
        lightRoot = new GameObject("CrystalLights");
        Undo.RegisterCreatedObjectUndo(lightRoot, "crystal lights");
        GameObjectUtility.SetStaticEditorFlags(lightRoot, 0);

        var spots = new List<Vector3>();
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t.name.StartsWith("Landmark_"))
                spots.Add(t.position + Vector3.up * 3.0f);
        // 절벽 광맥 쪽에도 몇 개 — 링 반경 79 위 네 방향
        float[] ang = { 35f, 120f, 215f, 300f };
        foreach (var a in ang)
        {
            float r = 77f, rad = a * Mathf.Deg2Rad;
            spots.Add(new Vector3(Mathf.Sin(rad) * r, 11f, Mathf.Cos(rad) * r));
        }

        var cLights = new List<Light>();
        for (int k = 0; k < spots.Count; k++)
        {
            var lg = new GameObject("CrystalLight_" + (k + 1));
            lg.transform.SetParent(lightRoot.transform, false);
            lg.transform.position = spots[k];
            GameObjectUtility.SetStaticEditorFlags(lg, 0);
            var L = lg.AddComponent<Light>();
            L.type = LightType.Point;
            L.color = new Color(1.00f, 0.82f, 0.42f);
            L.range = k < 3 ? 18f : 26f;
            L.intensity = 0f;
            L.shadows = LightShadows.None;
            L.lightmapBakeType = LightmapBakeType.Realtime;
            L.renderMode = LightRenderMode.ForceVertex;   // 픽셀 라이트 예산을 안 먹게
            cLights.Add(L);
        }
        Debug.Log("[ToD] 결정 포인트 라이트 " + cLights.Count + "개 (랜드마크 3 + 절벽 4)");


        // ── 다이얼 + 팝업 패널 ────────────────────────────────────────
        Transform table = null;
        foreach (var t in Object.FindObjectsOfType<Transform>(true))
            if (t.name.StartsWith("camp03_table")) table = t;
        if (table == null) Debug.LogWarning("[ToD] 테이블 못 찾음 — 다이얼 생략");

        GameObject dialGo = null, panelGo = null;
        var cards = new List<GameObject>();
        Transform pointer = null;
        if (table != null)
        {
            var tr = table.GetComponentInChildren<Renderer>();
            float topY = tr != null ? tr.bounds.max.y : table.position.y + 0.4f;
            Vector3 tableTop = new Vector3(table.position.x, topY, table.position.z);

            foreach (var r in scene.GetRootGameObjects()) { }
            var old = GameObject.Find("TimeDial"); if (old != null) Undo.DestroyObjectImmediate(old);
            var oldP = GameObject.Find("TimePanel"); if (oldP != null) Undo.DestroyObjectImmediate(oldP);

            dialGo = new GameObject("TimeDial");
            Undo.RegisterCreatedObjectUndo(dialGo, "dial");
            dialGo.transform.SetParent(camp != null ? camp.transform : null, true);
            dialGo.transform.position = tableTop + Vector3.up * 0.015f;
            dialGo.transform.rotation = Quaternion.identity;
            GameObjectUtility.SetStaticEditorFlags(dialGo, 0);

            var brass = BrassMat();
            Prim(PrimitiveType.Cylinder, dialGo.transform, "DialBase",
                 new Vector3(0f, 0.012f, 0f), new Vector3(0.17f, 0.012f, 0.17f), brass);
            var ring = Prim(PrimitiveType.Cylinder, dialGo.transform, "DialRing",
                 new Vector3(0f, 0.026f, 0f), new Vector3(0.12f, 0.004f, 0.12f), brass);
            var pv = new GameObject("DialPointer");
            Undo.RegisterCreatedObjectUndo(pv, "pointer");
            pv.transform.SetParent(dialGo.transform, false);
            pv.transform.localPosition = new Vector3(0f, 0.030f, 0f);
            GameObjectUtility.SetStaticEditorFlags(pv, 0);
            Prim(PrimitiveType.Cube, pv.transform, "PointerArm",
                 new Vector3(0f, 0f, 0.045f), new Vector3(0.016f, 0.010f, 0.085f), brass);
            pointer = pv.transform;

            var bc = dialGo.AddComponent<BoxCollider>();
            bc.center = new Vector3(0f, 0.03f, 0f);
            bc.size = new Vector3(0.24f, 0.09f, 0.24f);

            // 패널 — 테이블 위 0.9m
            panelGo = new GameObject("TimePanel");
            Undo.RegisterCreatedObjectUndo(panelGo, "panel");
            panelGo.transform.SetParent(camp != null ? camp.transform : null, true);
            panelGo.transform.position = tableTop + Vector3.up * 0.90f;
            panelGo.transform.rotation = Quaternion.Euler(-20f, 0f, 0f);
            GameObjectUtility.SetStaticEditorFlags(panelGo, 0);

            for (int k = 0; k < 3; k++)
            {
                var pm = UnlitPanel("M_Panel_" + k, "T_Panel_" + k);
                var q = Prim(PrimitiveType.Quad, panelGo.transform, "Card_" + k,
                             Vector3.zero, new Vector3(1.20f, 0.50f, 1f), pm);
                q.SetActive(k == 0);
                cards.Add(q);
            }
            panelGo.SetActive(false);
            Debug.Log("[ToD] 다이얼 " + dialGo.transform.position + " / 패널 " + panelGo.transform.position);
        }

        // ── 컴포넌트 ──
        GameObject go = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "TimeOfDay") go = r;
        if (go == null) { go = new GameObject("TimeOfDay"); Undo.RegisterCreatedObjectUndo(go, "tod"); }
        var tod = go.GetComponent<PyriteTimeOfDay>();
        if (tod == null) tod = UdonSharpUndo.AddComponent<PyriteTimeOfDay>(go);

        Undo.RecordObject(tod, "tod setup");
        tod.presetName   = new[] { "노을", "밤", "새벽" };
        tod.skybox       = new[] { srcSky, skyNight ?? srcSky, skyDawn ?? srcSky };
        tod.ambMode      = new[] { 0, 1, 1 };
        tod.ambSky       = new[] { new Color(0.604f,0.604f,0.604f), new Color(0.028f,0.040f,0.088f), new Color(0.30f,0.27f,0.38f) };
        tod.ambEquator   = new[] { new Color(0.354f,0.366f,0.377f), new Color(0.022f,0.030f,0.062f), new Color(0.27f,0.23f,0.30f) };
        tod.ambGround    = new[] { new Color(0.198f,0.193f,0.184f), new Color(0.008f,0.011f,0.022f), new Color(0.12f,0.11f,0.13f) };
        tod.ambIntensity = new[] { 1.6f, 0.55f, 1.25f };
        tod.fogOn        = new[] { false, true, true };
        tod.fogColor     = new[] { Color.gray, new Color(0.045f,0.065f,0.125f), new Color(0.36f,0.27f,0.33f) };
        tod.fogDensity   = new[] { 0.002f, 0.0130f, 0.0050f };
        tod.reflIntensity= new[] { 1.0f, 0.35f, 0.70f };

        tod.sun          = sun;
        // 🔴 노을 태양은 씬 값에서 읽으면 안 된다 — 밤 상태에서 Setup 하면 밤 태양(0.04, 청색)이 노을로 박힌다(실제로 당했다).
        //    원래 값은 buildspec 문서: Directional FFB070 / 1.25
        var SUN_DUSK_C = new Color(1.000f, 0.690f, 0.439f);
        const float SUN_DUSK_I = 1.25f;
        tod.sunColor     = new[] { SUN_DUSK_C, new Color(0.28f,0.36f,0.62f), new Color(1.00f,0.66f,0.70f) };
        tod.sunIntensity = new[] { SUN_DUSK_I, 0.04f, 0.80f };

        tod.cliffRenderers = cliffR;
        tod.cliffMat       = new[] { srcCliff, cliffNight ?? srcCliff, cliffDawn ?? srcCliff };
        tod.waterRenderers = waterR;
        tod.waterMat       = new[] { srcWater, waterNight ?? srcWater, waterDawn ?? srcWater };
        tod.flowerRenderers= floraR;
        tod.flowerMat      = new[] { srcFlower, flowerNight ?? srcFlower, srcFlower };

        tod.shimmerMat   = shimmer;
        tod.shimmerGain  = new[] { 1.0f, 1.7f, 0.7f };

        tod.fireflies    = ff;
        var gA = new Color(1.00f,0.74f,0.24f); var gB = new Color(1.00f,0.93f,0.58f);
        tod.ffColorA     = new[] { gA, gA, gA };
        tod.ffColorB     = new[] { gB, gB, gB };
        tod.ffRateMul    = new[] { 1.0f, 1.7f, 0.0f };
        tod.ffSizeMul    = new[] { 1.0f, 1.3f, 1.0f };

        tod.crystalMats          = cMats.ToArray();
        tod.crystalBaseEmission  = cEm.ToArray();
        tod.crystalEmissionMul   = new[] { 0.16f, 0.80f, 0.10f };
        tod.crystalLights        = cLights.ToArray();
        tod.crystalLightIntensity= new[] { 0.0f, 1.15f, 0.0f };

        tod.campLights   = campL;
        tod.campLightMul = new[] { 1.0f, 1.5f, 0.55f };

        tod.panel       = panelGo;
        tod.panelCards  = cards.ToArray();
        tod.dialPointer = pointer;
        tod.dialYaw     = new[] { 0f, 120f, 240f };

        // 환경음은 K. Setup Ambience 가 만든다. 여기서는 이미 있는 걸 다시 물려주기만 한다.
        PyriteAmbienceTools.Wire(tod);

        // 밤 어둠(지형·부두 구운 조명 색조), 꽃 밤 머티리얼, 황철석 금속 값 — V 툴과 같은 값으로 덮어쓴다
        PyriteNightTools.Wire(tod);

        // 구축 직후엔 항상 프리셋 0(노을)으로 되돌려 둔다.
        // 씬이 밤 상태로 저장돼 있으면 다음 Setup이 또 잘못된 원본을 읽게 된다.
        tod.index = 0;
        tod.Apply();

        // 다이얼에 상호작용 스크립트
        if (dialGo != null)
        {
            var dial = dialGo.GetComponent<PyriteDial>();
            if (dial == null) dial = UdonSharpUndo.AddComponent<PyriteDial>(dialGo);
            dial.tod = tod;
            EditorUtility.SetDirty(dial);
            UdonSharpEditorUtility.CopyProxyToUdon(dial);
        }

        EditorUtility.SetDirty(tod);
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(string.Format(
            "[ToD] 구축 완료 — 프리셋 3 / 절벽 렌더러 {0} / 물 {1} / 꽃 {2} / 반딧불 {3} / 캠프 조명 {4}\n"
            + "  스카이박스: {5} / {6} / {7}",
            cliffR.Length, waterR.Length, floraR.Length, ff.Length, campL.Length,
            srcSky != null ? srcSky.name : "?",
            skyNight != null ? skyNight.name : "?",
            skyDawn != null ? skyDawn.name : "?"));
    }

    [MenuItem("Tools/Pyrite/J2. Preview Preset 0 1 2")]
    public static void CyclePreview()
    {
        GameObject go = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == "TimeOfDay") go = r;
        if (go == null) { Debug.LogError("[ToD] TimeOfDay 없음"); return; }
        var tod = go.GetComponent<PyriteTimeOfDay>();
        Undo.RecordObject(tod, "preview");
        tod.index = (tod.index + 1) % tod.presetName.Length;
        tod.Apply();                       // 기준값 캡처는 Apply 안에서 알아서 한다
        EditorUtility.SetDirty(tod);
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        SceneView.RepaintAll();
        Debug.Log("[ToD] 프리뷰 인덱스 " + tod.index + " = " + tod.presetName[tod.index]);
    }
}
#endif
