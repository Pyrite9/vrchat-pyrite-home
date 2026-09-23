// Tools ▸ Pyrite ▸ Z19c. PP Rebuild (dusk/day/night) + Exposure Sweep
//  발견(Z19b): PP_PyriteDusk 의 효과 3개가 전부 빈 참조(fileID 0) — F(Setup Post Processing)가 AddSettings 만 하고
//  하위 에셋으로 저장하지 않아 재시작 때 사라졌다. 월드는 처음부터 후처리 없이 보였다.
//  여기서:
//   1) 프로필 3개를 제대로 만든다 (효과를 AddObjectToAsset 으로 프로필 안에 저장)
//        PP_PyriteDusk  기존 의도(따뜻하게, 파란 그림자·따뜻한 하이라이트)
//        PP_PyriteDay   중립 색온도, 대비·채도 조금
//        PP_PyriteNight 푸르게, 블룸 강하게(모닥불·랜턴·결정)
//   2) 볼륨: 기존 PostProcessVolume = 노을(우선 0, weight 1) / PostProcess_Day(우선 1) / PostProcess_Night(우선 2) — 레이어 23, 전역
//   3) 노출 스윕: 12:00·18:20·21:00 × (후처리 끔, postExposure 3가지) 렌더 + 영역 밝기 → Logs/pyrite_pp.txt
//  시간대 연동(DayCycle weight)은 값이 정해진 뒤 다음 단계. 여기선 볼륨 weight 를 스윕 동안만 바꾸고 0 으로 둔다.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class PyritePPRebuild
{
    const string LOG = "Logs/pyrite_pp.txt";
    public const string DUSK = "Assets/TerrainAssets/PP_PyriteDusk.asset";
    public const string DAY = "Assets/TerrainAssets/PP_PyriteDay.asset";
    public const string NIGHT = "Assets/TerrainAssets/PP_PyriteNight.asset";
    const int PP_LAYER = 23;

    static T AddS<T>(PostProcessProfile p) where T : PostProcessEffectSettings
    {
        var s = p.AddSettings<T>();
        s.name = typeof(T).Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, p);
        return s;
    }

    static PostProcessProfile Fresh(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if (p == null) { p = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(p, path); }
        // 기존 하위 효과·빈 참조 제거
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o is PostProcessEffectSettings).ToArray()) Object.DestroyImmediate(o, true);
        p.settings.Clear();
        return p;
    }

    public static void Grading(ColorGrading cg, float postExp, float temp, float tint, float contrast, float sat, Vector4 lift, Vector4 gain)
    {
        cg.enabled.Override(true);
        cg.gradingMode.Override(GradingMode.HighDefinitionRange);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.postExposure.Override(postExp);
        cg.temperature.Override(temp); cg.tint.Override(tint);
        cg.contrast.Override(contrast); cg.saturation.Override(sat);
        cg.lift.Override(lift); cg.gamma.Override(new Vector4(1f, 1f, 1f, 0f)); cg.gain.Override(gain);
    }

    static void BloomSet(Bloom b, float intensity, float threshold, Color col)
    {
        b.enabled.Override(true);
        b.intensity.Override(intensity); b.threshold.Override(threshold); b.softKnee.Override(0.6f);
        b.diffusion.Override(7f); b.anamorphicRatio.Override(0f); b.color.Override(col); b.fastMode.Override(false);
    }

    static void Vig(Vignette v, float i)
    {
        v.enabled.Override(true); v.mode.Override(VignetteMode.Classic); v.intensity.Override(i);
        v.smoothness.Override(0.4f); v.rounded.Override(false); v.color.Override(Color.black);
    }

    // 기본 노출 (스윕으로 다시 고른다)
    static float EXP_DUSK = 1.0f, EXP_DAY = 1.0f, EXP_NIGHT = 1.0f;   // 2026-09-23 관리자 결정: 전 시간대 ACES 노출 1.0 (밤은 어두운 쪽 수용)

    [MenuItem("Tools/Pyrite/Z19c. PP Rebuild + Exposure Sweep", false, 47)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z19c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");

        var dusk = Fresh(DUSK);
        BloomSet(AddS<Bloom>(dusk), 1.0f, 1.0f, new Color(1f, 0.93f, 0.84f));
        Grading(AddS<ColorGrading>(dusk), EXP_DUSK, 8f, -3f, 3f, 4f, new Vector4(0.97f, 1.00f, 1.07f, 0.015f), new Vector4(1.05f, 1.01f, 0.95f, 0.02f));
        Vig(AddS<Vignette>(dusk), 0.24f);
        EditorUtility.SetDirty(dusk);

        var day = Fresh(DAY);
        BloomSet(AddS<Bloom>(day), 0.5f, 1.2f, Color.white);
        Grading(AddS<ColorGrading>(day), EXP_DAY, 0f, 0f, 8f, 12f, new Vector4(1f, 1f, 1.02f, 0f), new Vector4(1f, 1f, 1f, 0.02f));
        Vig(AddS<Vignette>(day), 0.18f);
        EditorUtility.SetDirty(day);

        var night = Fresh(NIGHT);
        BloomSet(AddS<Bloom>(night), 1.6f, 0.8f, new Color(1f, 0.9f, 0.75f));
        Grading(AddS<ColorGrading>(night), EXP_NIGHT, -6f, 0f, 5f, 0f, new Vector4(0.97f, 1.00f, 1.08f, 0f), new Vector4(1.03f, 1.0f, 0.97f, 0f));
        Vig(AddS<Vignette>(night), 0.28f);
        EditorUtility.SetDirty(night);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(DUSK); AssetDatabase.ImportAsset(DAY); AssetDatabase.ImportAsset(NIGHT);
        foreach (var path in new[] { DUSK, DAY, NIGHT })
            sb.AppendLine("  " + path + " sub-assets: " + string.Join(", ", AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o is PostProcessEffectSettings).Select(o => o.GetType().Name)));

        // 볼륨
        var vDusk = Object.FindObjectsOfType<PostProcessVolume>(true).FirstOrDefault(v => v.name == "PostProcessVolume");
        if (vDusk == null) { sb.AppendLine("PostProcessVolume 없음"); Flush(sb); return; }
        vDusk.sharedProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(DUSK); vDusk.priority = 0; vDusk.weight = 1f; vDusk.isGlobal = true;
        var vDay = Vol("PostProcess_Day", DAY, 1f);
        var vNight = Vol("PostProcess_Night", NIGHT, 2f);
        EditorUtility.SetDirty(vDusk);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // 스윕
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var cam = Camera.main; var ppl = cam.GetComponent<PostProcessLayer>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        Directory.CreateDirectory("Assets/_preview/pp/");
        try
        {
            var cases = new[] { (12f, "noon", day, 1f, 0f), (PyriteDayCycleSetup.EDITOR_HOUR, "dusk", dusk, 0f, 0f), (21f, "night", night, 0f, 1f) };
            // 변형: (이름, 톤매퍼, postExposure, gamma w) — ACES 는 어두운 쪽을 크게 누른다(1차: 정오 절벽 58→32)
            var vars = new (string, Tonemapper, float, float)[]
            {
                ("none",        Tonemapper.None,    0f,   0f),
                ("none_g10",    Tonemapper.None,    0f,   0.10f),
                ("neutral",     Tonemapper.Neutral, 0.3f, 0f),
                ("neutral_g10", Tonemapper.Neutral, 0.3f, 0.10f),
                ("aces_e1.0",   Tonemapper.ACES,    1.0f, 0f),
            };
            foreach (var (h, n, prof, wd, wn) in cases)
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                vDay.weight = wd; vNight.weight = wn;
                prof.TryGetSettings<ColorGrading>(out var cg);
                var tm0 = cg.tonemapper.value; float e0 = cg.postExposure.value; var g0 = cg.gamma.value;
                ppl.enabled = false;
                Views(cam, n + "_off", sb);
                ppl.enabled = true;
                foreach (var (vn, tm, e, gw) in vars)
                {
                    cg.tonemapper.Override(tm); cg.postExposure.Override(e); cg.gamma.Override(new Vector4(1f, 1f, 1f, gw));
                    Views(cam, n + "_" + vn, sb);
                }
                cg.tonemapper.Override(tm0); cg.postExposure.Override(e0); cg.gamma.Override(g0);
            }
        }
        finally
        {
            ppl.enabled = true; vDay.weight = 0f; vNight.weight = 0f;
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static PostProcessVolume Vol(string name, string profPath, float prio)
    {
        var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go == null) { go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, name); }
        go.layer = PP_LAYER;
        var v = go.GetComponent<PostProcessVolume>(); if (v == null) v = go.AddComponent<PostProcessVolume>();
        v.isGlobal = true; v.priority = prio; v.weight = 0f;
        v.sharedProfile = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(profPath);
        EditorUtility.SetDirty(v);
        return v;
    }

    struct V { public string n; public Vector3 eye, look; }
    static readonly V[] VS =
    {
        new V{ n="camp_lake", eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f) },
        new V{ n="tent_side", eye=new Vector3( -4.0f, 1.7f, 58.0f), look=new Vector3(-16.0f, 1.5f, 56.0f) },
    };

    static void Views(Camera cam, string tag, StringBuilder sb)
    {
        var t = Terrain.activeTerrain;
        sb.Append("  " + tag.PadRight(18));
        foreach (var v in VS)
        {
            var eye = v.eye; if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
            var px = Shot(cam, eye, v.look, string.Format("Assets/_preview/pp/{0}_{1}.png", tag, v.n));
            // 위 1/3(하늘) / 가운데(절벽) / 아래 1/3 평균 밝기(0..255)
            int W = 960, H = 540; float[] s = new float[3]; int[] c = new int[3];
            for (int y = 0; y < H; y += 2) for (int x = 0; x < W; x += 2)
            {
                int band = y < H / 3 ? 2 : (y < 2 * H / 3 ? 1 : 0);     // ReadPixels 는 위가 y=0 → PNG/텍스처 행 0 은 아래... 텍스처 기준으로 뒤집어 센다
                s[band] += px[y * W + x].grayscale; c[band]++;
            }
            sb.Append(string.Format(" | {0} sky {1:0} mid {2:0} low {3:0}", v.n, 255 * s[0] / c[0], 255 * s[1] / c[1], 255 * s[2] / c[2]));
        }
        sb.AppendLine();
    }

    static Color[] Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        var px = tx.GetPixels();
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        return px;
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString()); }
}
#endif
