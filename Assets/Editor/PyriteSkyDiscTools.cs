// Tools ▸ Pyrite ▸ Z3. Sun + Moon (sky discs)
//  태양·달이 절벽에 가려 안 보이던 문제 (측정: Z1 — 캠프에서 V자 골짜기 벽 약 4°, 호수 가운데서 약 7°)
//   [S1] 스카이박스 태양 고도 — 노을 0.10 → 0.17 (5.7° → 9.8°), 새벽 0.045 → 0.14 (2.6° → 8.0°). 방위는 둘 다 184 (Shadowmask — 정적 그림자가 구운 각도에 고정)
//   [S2] 하늘 원반 3개 (Pyrite/SkyDisc — 카메라 기준 방향 빌보드, 시차 없음, 지형 뒤로 가려짐)
//        노을 해 빛무리 / 달(지름 3°) / 새벽 해 빛무리. ToD 가 프리셋마다 하나만 켠다
//   [S3] 방향광을 프리셋마다 보이는 해·달 쪽으로 (ToD.sunEuler). 씬에는 노을 각도로 둔다(라이트맵 굽는 각도)
//   [S4] 그림자: 빛 고도 22°(보이는 해와 분리), 노을 하늘빛 1.6→1.2, 지형 라이트맵 배율 0.128
//  재실행 안전. 텍스처는 코드로 만든다(저장소에서 재현 가능). ⚠ J(Setup Time Of Day)를 다시 돌리면 ambIntensity 가 1.6 으로 돌아간다 → Z3 다시
//  라이트맵을 다시 구워야 그림자 방향·해상도가 반영된다.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteSkyDiscTools
{
    // 프리셋: 0 노을 / 1 밤 / 2 새벽
    public const float DUSK_E = 0.17f, DUSK_AZ = 184f;     // Sorafield _SunElevation 은 sin(고도)
    public const float DAWN_E = 0.14f, DAWN_AZ = 184f;     // 방위는 노을과 같게 — 혼합조명이 Shadowmask 라 정적 그림자는 구운 각도에 고정된다
    public const float MOON_EL = 22f,  MOON_AZ = 205f;     // 캠프에서 호수 정면(180°) 조금 오른쪽
    // [S4] 그림자 가독성 — 빛은 보이는 해보다 높은 각도에서 (방위는 같게).
    //      9.8° 로 비추면 바닥이 받는 햇빛이 17% 뿐이라 그림자가 하늘빛에 묻혔다. 22° → 37%.
    public const float LIGHT_EL = 22f;
    public const float DUSK_AMB = 1.2f;                    // 노을 하늘빛 세기 (전 1.6) — 그림자 대비
    // 지형 라이트맵 배율: 0.0256 = 1텍셀/m 라 의자·테이블 그림자가 번져 사라졌다. 200m 를 1024 한 장에 = 0.128 (5텍셀/m)
    public const float TERRAIN_LM = 0.128f;

    const string TEX_SUN = "Assets/Textures/T_SkySunGlow.png";
    const string TEX_MOON = "Assets/Textures/T_SkyMoon.png";
    const string MESH = "Assets/Meshes/SkyDiscQuad.asset";
    const float SUN_HALF = 5.0f, MOON_HALF = 3.6f;          // 쿼드 반각(도)

    public static Vector3 Dir(float elDeg, float azDeg)
    {
        float e = elDeg * Mathf.Deg2Rad, a = azDeg * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
    }
    static float ElOfSin(float s) => Mathf.Asin(s) * Mathf.Rad2Deg;

    [MenuItem("Tools/Pyrite/Z3. Sun + Moon (sky discs)", false, 7)]
    public static void Run()
    {
        var log = new StringBuilder("[Z3] ");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var sh = Shader.Find("Pyrite/SkyDisc");
        if (tod == null || tod.sun == null || sh == null || tod.skybox == null || tod.skybox.Length < 3) { Debug.LogError("[Z3] ToD/sun/skybox/Pyrite/SkyDisc 없음"); return; }

        // ── 진단 로그: 혼합 조명 모드·그림자·카메라 far
        try { var ls = Lightmapping.lightingSettings; log.Append("mixed=").Append(ls.mixedBakeMode).Append(" | "); } catch { log.Append("mixed=? | "); }
        log.Append("sunShadows=").Append(tod.sun.shadows).Append(" mode=").Append(tod.sun.lightmapBakeType).Append(" | ");
        if (Camera.main != null) log.Append("camFar=").Append(Camera.main.farClipPlane).Append(" | ");

        // ── [S1] 스카이박스
        var dusk = tod.skybox[0]; var dawn = tod.skybox[2];
        Undo.RecordObjects(new Object[] { dusk, dawn }, "sky sun");
        dusk.SetFloat("_SunElevation", DUSK_E); dusk.SetFloat("_SunAzimuth", DUSK_AZ);
        dawn.SetFloat("_SunElevation", DAWN_E); dawn.SetFloat("_SunAzimuth", DAWN_AZ);
        EditorUtility.SetDirty(dusk); EditorUtility.SetDirty(dawn);

        // ── [S2] 텍스처·메시·머티리얼·오브젝트
        Directory.CreateDirectory("Assets/Textures"); Directory.CreateDirectory("Assets/Meshes");
        MakeTex(TEX_SUN, SunGlow);
        MakeTex(TEX_MOON, Moon);
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH);
        if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, MESH); }
        mesh.Clear(); mesh.name = "SkyDiscQuad";
        mesh.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20000f);     // 셰이더가 위치를 옮기므로 컬링 안 되게
        EditorUtility.SetDirty(mesh);

        var root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "SkyDiscs");
        if (root == null) { root = new GameObject("SkyDiscs"); Undo.RegisterCreatedObjectUndo(root, "sky discs"); }
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        GameObjectUtility.SetStaticEditorFlags(root, 0);

        var sunTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_SUN);
        var moonTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_MOON);
        var gDusk = Disc(root, mesh, sh, "Sky_SunDusk", sunTex, new Color(1.00f, 0.56f, 0.26f) * 3.0f, Dir(ElOfSin(DUSK_E), DUSK_AZ), SUN_HALF);
        var gMoon = Disc(root, mesh, sh, "Sky_Moon", moonTex, new Color(0.80f, 0.86f, 1.00f) * 1.35f, Dir(MOON_EL, MOON_AZ), MOON_HALF);
        var gDawn = Disc(root, mesh, sh, "Sky_SunDawn", sunTex, new Color(1.00f, 0.66f, 0.60f) * 2.6f, Dir(ElOfSin(DAWN_E), DAWN_AZ), SUN_HALF);

        // ── [S3] 방향광
        tod.sunEuler = new[]
        {
            new Vector3(LIGHT_EL, DUSK_AZ - 180f, 0f),
            new Vector3(MOON_EL,  MOON_AZ - 180f, 0f),
            new Vector3(LIGHT_EL, DAWN_AZ - 180f, 0f),
        };
        tod.skyObjects = new[] { gDusk, gMoon, gDawn };
        Undo.RecordObject(tod.sun.transform, "sun rot");
        tod.sun.transform.rotation = Quaternion.Euler(tod.sunEuler[0]);
        if (tod.ambIntensity != null && tod.ambIntensity.Length > 0) { log.Append("ambIntensity[0] ").Append(tod.ambIntensity[0]).Append("→").Append(DUSK_AMB).Append(" | "); tod.ambIntensity[0] = DUSK_AMB; }
        var terr = Terrain.activeTerrain;
        if (terr != null)
        {
            var so = new SerializedObject(terr); var p = so.FindProperty("m_ScaleInLightmap");
            log.Append("terrainLM ").Append(p.floatValue).Append("→").Append(TERRAIN_LM).Append(" | ");
            p.floatValue = TERRAIN_LM; so.ApplyModifiedProperties();
        }
        log.Append("sunEuler ").Append(string.Join(" ", tod.sunEuler.Select(v => v.ToString("F2")))).Append(" | ");

        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
        tod.index = 0; tod.Apply();
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
        Directory.CreateDirectory("Assets/_preview/");
        File.WriteAllText("Assets/_preview/skydisc.txt", log.ToString());
        PyriteViews.CaptureSet("camp_lake shore", new[] { 0, 1, 2 });
    }

    static GameObject Disc(GameObject root, Mesh mesh, Shader sh, string name, Texture2D tex, Color col, Vector3 dir, float half)
    {
        var t = root.transform.Find(name);
        GameObject go;
        if (t == null) { go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "disc"); go.transform.SetParent(root.transform, false); }
        else go = t.gameObject;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        var mf = go.GetComponent<MeshFilter>(); if (mf == null) mf = go.AddComponent<MeshFilter>();
        var mr = go.GetComponent<MeshRenderer>(); if (mr == null) mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = mesh;
        string mp = "Assets/Materials/M_" + name.Replace("Sky_", "SkyDisc_") + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, mp); }
        m.shader = sh;
        m.SetTexture("_MainTex", tex);
        m.SetColor("_Color", col);
        m.SetVector("_Dir", new Vector4(dir.x, dir.y, dir.z, 0));
        m.SetFloat("_AngSize", half);
        EditorUtility.SetDirty(m);
        mr.sharedMaterial = m;
        mr.shadowCastingMode = ShadowCastingMode.Off; mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off; mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        mr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        mr.allowOcclusionWhenDynamic = false;
        return go;
    }

    // ── 텍스처 (선형, 가산용 RGB) ────────────────────────────────
    delegate Color Px(float thetaDeg, float u, float v);

    static void MakeTex(string path, Px fn)
    {
        const int N = 256;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false, true);
        var px = new Color[N * N];
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                float u = (i + 0.5f) / N * 2f - 1f, v = (j + 0.5f) / N * 2f - 1f;   // -1..1
                px[j * N + i] = fn(Mathf.Sqrt(u * u + v * v), u, v);
            }
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.sRGBTexture = false; ti.mipmapEnabled = true; ti.wrapMode = TextureWrapMode.Clamp;
        ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaSource = TextureImporterAlphaSource.None;
        ti.SaveAndReimport();
    }

    static float SS(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

    // 해: 지름 1.6° 원반 + 두 겹 빛무리. 쿼드 가장자리에서 0
    static Color SunGlow(float rho, float u, float v)
    {
        float th = rho * SUN_HALF;
        float core = 1f - SS(0.68f, 0.92f, th);
        float glow = 0.38f * Mathf.Exp(-th / 0.9f) + 0.07f * Mathf.Exp(-th / 2.6f);
        float k = (core + glow) * (1f - SS(0.80f, 1.0f, rho));
        return new Color(k, k, k, 1);
    }

    // 달: 지름 3° — 바다(어두운 얼룩)·크레이터·가장자리 어둡게 + 옅은 달무리
    static readonly Vector3[] CRATERS = MakeCraters();
    static Vector3[] MakeCraters()
    {
        var r = new System.Random(20260923); var c = new Vector3[28];
        for (int k = 0; k < c.Length; k++)
        {
            float a = (float)r.NextDouble() * 6.2832f, d = Mathf.Sqrt((float)r.NextDouble()) * 0.85f;
            c[k] = new Vector3(Mathf.Cos(a) * d, Mathf.Sin(a) * d, 0.03f + 0.09f * (float)r.NextDouble() * (float)r.NextDouble());
        }
        return c;
    }
    static Color Moon(float rho, float u, float v)
    {
        const float R = 1.5f;
        float th = rho * MOON_HALF;
        float q = th / R;                                     // 원반 안 0..1
        float disc = 1f - SS(0.97f, 1.02f, q);
        float mu = u * MOON_HALF / R, mv = v * MOON_HALF / R;  // 원반 좌표 -1..1
        float maria = 0f;
        maria += Mathf.PerlinNoise(mu * 1.6f + 3.1f, mv * 1.6f + 7.3f) * 0.65f;
        maria += Mathf.PerlinNoise(mu * 4.0f + 11.0f, mv * 4.0f + 2.0f) * 0.35f;
        maria = SS(0.50f, 0.66f, maria);
        float alb = 0.92f - 0.30f * maria;
        foreach (var c in CRATERS)
        {
            float d = Mathf.Sqrt((mu - c.x) * (mu - c.x) + (mv - c.y) * (mv - c.y)) / c.z;
            if (d < 1.25f) alb *= d < 0.85f ? 0.86f : (d < 1.05f ? 1.10f : 1f);
        }
        float limb = 1f - 0.28f * (1f - Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(q, 1f) * Mathf.Min(q, 1f))));
        float moon = disc * alb * limb;
        float halo = 0f;
        if (th > R) halo = 0.10f * Mathf.Exp(-(th - R) / 0.55f) + 0.035f * Mathf.Exp(-(th - R) / 1.6f);
        float k = (moon + halo * (1f - disc)) * (1f - SS(0.85f, 1.0f, rho));
        return new Color(k, k, k, 1);
    }
}
#endif
