// Tools ▸ Pyrite ▸ Z9. Sky Motion (clouds + night Atmosphere)  /  Z9b. Revert
//  인게임에서 하늘이 멈춰 보였다.
//   · 노을·새벽: Sorafield 구름 속도 0.012 → 0.03 (구름 한 덩이가 제 크기만큼 지나가는 데 ~75초 → ~30초)
//   · 밤: 코드로 만든 정지 파노라마(T_Sky_PyriteNight, 달이 하나 구워져 있음) 대신
//         Sorafield Atmosphere 밤(해 −0.25) — 절차적 별 3겹이 _Time 으로 반짝이고 어두운 구름이 흐른다.
//         값은 패키지 프리셋 Atmosphere_Night 복사, 방위는 달(205), Exposure 3.4(Z9c 스윕). 셰이더는 원본 그대로(수정 없음)
//   · 새 머티리얼 Assets/Materials/M_Sky_PyriteNightAtmos.mat — 셰이더 참조 + 값만 있는 파일(에셋 파일 아님)
//   · 끝나면 T(반사 큐브맵) → 전후 렌더 Assets/_preview/sky/ + Logs/pyrite_sky.txt
//  되돌리기 Z9b — 밤 스카이박스를 M_Sky_PyriteNight 로, 구름 속도 0.012 로, T.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteSkyMotion
{
    const string OUT = "Assets/_preview/sky/";
    const string LOG = "Logs/pyrite_sky.txt";
    const string NEW_NIGHT = "Assets/Materials/M_Sky_PyriteNightAtmos.mat";
    const string OLD_NIGHT = "Assets/Materials/M_Sky_PyriteNight.mat";
    const string PRESET_NIGHT = "Assets/Sorafield Atmosphere Sky/Materials/Atmosphere_Night.mat";
    const string SHADER = "Sorafield/Skybox/01_Atmosphere";
    const float CLOUD_SPEED = 0.03f, CLOUD_SPEED_OLD = 0.012f, MOON_AZ = 205f;
    // 원본 프리셋(Exposure 1)은 하늘 평균 8.6 으로 절벽(5~7)과 붙어 실루엣이 사라졌다.
    // Z9c 스윕: 3.4 에서 하늘 31~37 = 이전 파노라마(34~38)와 같은 밝기, 밝은 별 점 ~6500
    const float NIGHT_EXPOSURE = 3.4f;
    static readonly StringBuilder log = new StringBuilder();

    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="camp_lake", eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f },
        new V{ n="camp_up",   eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-20.0f, 40.0f, -10.0f), fov=80f },
        new V{ n="moon",      eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f - 34.0f, 36.0f, 47.0f - 74.0f), fov=70f },
    };

    [MenuItem("Tools/Pyrite/Z9. Sky Motion (clouds + night Atmosphere)", false, 14)]
    public static void Run()
    {
        log.Clear(); log.AppendLine("[Z9] " + System.DateTime.Now.ToString("HH:mm:ss"));
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null || tod.skybox == null || tod.skybox.Length < 3) { log.AppendLine("ToD/skybox 없음 — 중단"); Flush(); return; }
        var sh = Shader.Find(SHADER);
        if (sh == null) { log.AppendLine("Sorafield 셰이더 없음 — 중단"); Flush(); return; }

        Capture("before");

        foreach (int i in new[] { 0, 2 })
        {
            var m = tod.skybox[i];
            if (m != null && m.HasProperty("_CloudSpeed"))
            {
                log.AppendLine(string.Format("  {0} _CloudSpeed {1} -> {2}", m.name, m.GetFloat("_CloudSpeed"), CLOUD_SPEED));
                Undo.RecordObject(m, "cloud speed"); m.SetFloat("_CloudSpeed", CLOUD_SPEED); EditorUtility.SetDirty(m);
            }
        }

        var night = AssetDatabase.LoadAssetAtPath<Material>(NEW_NIGHT);
        if (night == null)
        {
            var preset = AssetDatabase.LoadAssetAtPath<Material>(PRESET_NIGHT);
            night = preset != null ? new Material(preset) : new Material(sh);
            night.shader = sh;
            night.name = "M_Sky_PyriteNightAtmos";
            AssetDatabase.CreateAsset(night, NEW_NIGHT);
            log.AppendLine("  생성 " + NEW_NIGHT + (preset != null ? " (Atmosphere_Night 복사)" : " (기본값)"));
        }
        night.SetFloat("_SunAzimuth", MOON_AZ);
        night.SetFloat("_Exposure", NIGHT_EXPOSURE);
        EditorUtility.SetDirty(night);
        foreach (var p in new[] { "_SunElevation", "_SunAzimuth", "_CloudCoverage", "_CloudSpeed", "_MieStrength", "_HorizonHaze", "_Exposure" })
            log.AppendLine(string.Format("    {0} {1}", p, night.GetFloat(p)));

        Undo.RecordObject(tod, "night sky");
        log.AppendLine("  skybox[1] " + (tod.skybox[1] ? tod.skybox[1].name : "null") + " -> " + night.name);
        tod.skybox[1] = night;
        Finish(tod);
        Capture("after");
        log.AppendLine("RESULT: DONE"); Flush();
    }

    [MenuItem("Tools/Pyrite/Z9b. Sky Motion Revert", false, 15)]
    public static void Revert()
    {
        log.Clear(); log.AppendLine("[Z9b] " + System.DateTime.Now.ToString("HH:mm:ss"));
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) return;
        foreach (int i in new[] { 0, 2 })
        {
            var m = tod.skybox[i];
            if (m != null && m.HasProperty("_CloudSpeed")) { m.SetFloat("_CloudSpeed", CLOUD_SPEED_OLD); EditorUtility.SetDirty(m); }
        }
        var old = AssetDatabase.LoadAssetAtPath<Material>(OLD_NIGHT);
        if (old != null) { Undo.RecordObject(tod, "night sky revert"); tod.skybox[1] = old; }
        Finish(tod);
        Capture("revert");
        log.AppendLine("RESULT: REVERTED"); Flush();
    }

    static void Finish(PyriteTimeOfDay tod)
    {
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        ResetReady(tod);
        PyriteReflectionSets.Bake();             // 밤 반사 큐브맵에 새 하늘을 굽는다
        EditorSceneManager.SaveOpenScenes();
        log.AppendLine("  T 완료");
    }

    static void ResetReady(PyriteTimeOfDay tod)
    {
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
    }

    static void Capture(string tag)
    {
        Directory.CreateDirectory(OUT);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        if (cam == null || tod == null) return;
        var t = Terrain.activeTerrain;
        ResetReady(tod);
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        float n0 = cam.nearClipPlane, fa0 = cam.farClipPlane;
        try
        {
            foreach (int i in new[] { 0, 1, 2 })
            {
                tod.index = i; tod.Apply();
                foreach (var v in Views)
                {
                    if (i != 1 && v.n == "moon") continue;
                    var eye = v.eye;
                    if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0f, eye.z)) + t.transform.position.y);
                    string p = OUT + tag + "_" + v.n + "_" + i + ".png";
                    log.AppendLine("  " + p + "  " + Render(cam, eye, v.look, v.fov, p));
                }
            }
        }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0);
            cam.fieldOfView = f0; cam.targetTexture = null; cam.nearClipPlane = n0; cam.farClipPlane = fa0;
            tod.index = 0; tod.Apply();
        }
        AssetDatabase.Refresh();
    }

    // 렌더 + 위쪽 1/3(하늘) 밝기 통계 — 평균·표준편차·밝은 점(>150) 개수
    static string Render(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        var px = tex.GetPixels32();
        double s = 0, s2 = 0; int n = 0, bright = 0;
        for (int y = 2 * H / 3; y < H; y++)          // Texture2D 는 아래부터 — 위쪽 1/3
            for (int x = 0; x < W; x++)
            { var c = px[y * W + x]; float v = (c.r + c.g + c.b) / 3f; s += v; s2 += v * v; n++; if (v > 150) bright++; }
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        double m = s / n;
        return string.Format("sky mean {0:0.0} sd {1:0.0} bright {2}", m, System.Math.Sqrt(s2 / n - m * m), bright);
    }

    static void Flush() { Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, log.ToString()); }
}
#endif
