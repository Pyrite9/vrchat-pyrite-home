// Tools ▸ Pyrite ▸ Z17a. Shadow Cost Measure / Z17b. Switch To Baked Indirect (+Bake+T) / Z17c. Sun Sweep Render / Z17d. Revert To Shadowmask (+Bake+T)
//  [B 시험] 해·달이 실제로 뜨고 지게(24시간) 하려면 그림자가 해를 따라가야 한다.
//   지금: Mixed Lighting = Shadowmask → 정적 물체 그림자는 해 22°/184° 로 구워져 있어 해를 돌리면 어긋난다.
//   시험: Baked Indirect → 라이트맵엔 반사광만, 그림자는 전부 실시간(그림자 거리 안). 해를 돌리면 절벽·계단 그림자도 따라간다.
//  대가(측정 대상): 매 프레임 그림자 맵에 정적 지오메트리 전부(절벽 1,350기둥, 계단 672칸, 지형)를 그림.
//  Z17a — Game 뷰 통계로 4 시점 삼각형·배치·그림자 캐스터 (현재 조명 방식 표기)
//  Z17c — 해를 여러 시각(고도·방위)으로 돌려 렌더. 하늘은 임시 머티리얼 복사본(에셋은 안 바뀜), 끝나면 원래대로
//  로그 Logs/pyrite_suncycle.txt, 렌더 Assets/_preview/sun/
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteSunCycleTest
{
    const string LOG = "Logs/pyrite_suncycle.txt";

    static string ModeStr()
    {
        var ls = Lightmapping.lightingSettings;
        return ls == null ? "(no LightingSettings)" : ls.mixedBakeMode.ToString();
    }

    // ── 측정 ────────────────────────────────────────────────
    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="camp_lake",  eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f },
        new V{ n="far_to_camp",eye=new Vector3(  5.0f, 1.7f, -40.0f), look=new Vector3( -5.0f, 6.0f, 78.0f), fov=70f },
        new V{ n="west_wall",  eye=new Vector3( 25.0f, 1.7f, 45.0f), look=new Vector3(-78.0f, 5.0f, 20.0f), fov=65f },
        new V{ n="tent_side",  eye=new Vector3( -4.0f, 1.7f, 58.0f), look=new Vector3(-16.0f, 1.5f, 56.0f), fov=70f },
    };
    static int step, wait; static int[,] res; static StringBuilder msb;
    static Vector3 mp0; static Quaternion mr0; static float mf0;

    [MenuItem("Tools/Pyrite/Z17a. Shadow Cost Measure", false, 35)]
    public static void Measure()
    {
        var cam = Camera.main;
        msb = new StringBuilder(string.Format("[Z17a] {0}  mixed={1}  shadowDistance={2} cascades={3}\n",
            System.DateTime.Now.ToString("HH:mm:ss"), ModeStr(), QualitySettings.shadowDistance, QualitySettings.shadowCascades));
        var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null) msb.AppendLine(string.Format("  sun {0} mode {1} shadows {2} euler {3} int {4}", sun.name, sun.lightmapBakeType, sun.shadows, sun.transform.eulerAngles, sun.intensity));
        mp0 = cam.transform.position; mr0 = cam.transform.rotation; mf0 = cam.fieldOfView;
        res = new int[Views.Length, 4]; step = 0; wait = 0;
        var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gv != null) EditorWindow.GetWindow(gv, false, null, true);
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }

    static void Tick()
    {
        var cam = Camera.main; var t = Terrain.activeTerrain;
        if (wait == 0)
        {
            if (step >= Views.Length) { Finish(); return; }
            var v = Views[step]; var eye = v.eye;
            if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((v.look - eye).normalized, Vector3.up));
            cam.fieldOfView = v.fov; cam.farClipPlane = 1000f;
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        wait++; if (wait < 6) return;
        res[step, 0] = UnityStats.triangles; res[step, 1] = UnityStats.batches; res[step, 2] = UnityStats.shadowCasters; res[step, 3] = UnityStats.setPassCalls;
        wait = 0; step++;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        var cam = Camera.main; cam.transform.SetPositionAndRotation(mp0, mr0); cam.fieldOfView = mf0;
        long st = 0, sb = 0, ss = 0;
        for (int i = 0; i < Views.Length; i++)
        {
            msb.AppendLine(string.Format("  {0,-12} tris {1,8}  batches {2,5}  shadowCasters {3,5}  setpass {4,4}", Views[i].n, res[i, 0], res[i, 1], res[i, 2], res[i, 3]));
            st += res[i, 0]; sb += res[i, 1]; ss += res[i, 2];
        }
        msb.AppendLine(string.Format("  합계 tris {0}  batches {1}  shadowCasters {2}", st, sb, ss));
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, msb.ToString());
        Debug.Log("[Z17a] 완료 — " + LOG);
    }

    // ── 조명 방식 전환 ─────────────────────────────────────────
    [MenuItem("Tools/Pyrite/Z17b. Switch To Baked Indirect (+Bake+T)", false, 36)]
    public static void ToBakedIndirect() { Switch(MixedLightingMode.IndirectOnly, "[Z17b]"); }

    [MenuItem("Tools/Pyrite/Z17d. Revert To Shadowmask (+Bake+T)", false, 38)]
    public static void ToShadowmask() { Switch(MixedLightingMode.Shadowmask, "[Z17d]"); }

    static System.Diagnostics.Stopwatch sw; static string stag;
    static void Switch(MixedLightingMode mode, string tag)
    {
        var ls = Lightmapping.lightingSettings;
        if (ls == null || Lightmapping.isRunning) { Debug.LogWarning(tag + " LightingSettings 없음 또는 베이크 중"); return; }
        var sb = new StringBuilder(tag + " " + System.DateTime.Now.ToString("HH:mm:ss") + "  " + ls.mixedBakeMode + " -> " + mode + "\n");
        Undo.RecordObject(ls, "mixed mode");
        ls.mixedBakeMode = mode;
        EditorUtility.SetDirty(ls);
        // 지형·정적 메시가 그림자를 드리우는지 기록
        foreach (var t in Object.FindObjectsOfType<Terrain>()) sb.AppendLine("  terrain shadowCastingMode " + t.shadowCastingMode);
        foreach (var n in new[] { "PyriteCliffs_Visual", "PyriteTerraces", "Crystals" })
        {
            var g = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(x => x.name == n);
            if (g == null) continue;
            var rs = g.GetComponentsInChildren<MeshRenderer>(true);
            sb.AppendLine(string.Format("  {0}: {1} renderers, cast {2}", n, rs.Length, string.Join(",", rs.Select(r => r.shadowCastingMode.ToString()).Distinct())));
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        File.AppendAllText(LOG, sb.ToString());
        stag = tag; sw = System.Diagnostics.Stopwatch.StartNew();
        Lightmapping.bakeCompleted -= AfterBake; Lightmapping.bakeCompleted += AfterBake;
        PyriteBakeRun.Bake();
    }

    static void AfterBake()
    {
        Lightmapping.bakeCompleted -= AfterBake;
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.SaveOpenScenes();
            PyriteReflectionSets.Bake();
            EditorSceneManager.SaveOpenScenes();
            File.AppendAllText(LOG, string.Format("{0} 베이크 {1:0.0}분, 라이트맵 {2}장, T 완료. RESULT: DONE\n", stag, sw.Elapsed.TotalMinutes, LightmapSettings.lightmaps.Length));
        };
    }

    // ── 해 궤도 렌더 ───────────────────────────────────────────
    //  해는 뒤쪽(북, 방위 4°)에서 떠서 머리 위를 지나 V자 골짜기(184°)로 진다고 가정한 시험 궤도.
    struct S { public string n; public float el, az; public Color col; public float inten; }
    static readonly S[] Suns =
    {
        new S{ n="noon",      el=62f, az=110f, col=new Color(1f, 0.97f, 0.92f), inten=1.25f },
        new S{ n="afternoon", el=35f, az=160f, col=new Color(1f, 0.90f, 0.75f), inten=1.2f },
        new S{ n="dusk22",    el=22f, az=184f, col=new Color(1f, 0.69f, 0.44f), inten=1.25f },   // 현재 노을 방향광
        new S{ n="sunset",    el=6f,  az=184f, col=new Color(1f, 0.55f, 0.30f), inten=0.9f },
    };

    [MenuItem("Tools/Pyrite/Z17c. Sun Sweep Render", false, 37)]
    public static void Sweep()
    {
        var sb = new StringBuilder("[Z17c] " + System.DateTime.Now.ToString("HH:mm:ss") + "  mixed=" + ModeStr() + "\n");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null && tod != null) f.SetValue(tod, false);
        if (tod != null) { tod.index = 0; tod.Apply(); }
        var sun = RenderSettings.sun != null ? RenderSettings.sun : Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        var sky0 = RenderSettings.skybox; var sky = new Material(sky0);
        var q0 = sun.transform.rotation; var c0 = sun.color; float i0 = sun.intensity;
        var discs = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "SkyDiscs");
        bool d0 = discs != null && discs.activeSelf;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        Directory.CreateDirectory("Assets/_preview/sun/");
        string tag = ModeStr() == "IndirectOnly" ? "bi" : "sm";
        try
        {
            RenderSettings.skybox = sky;
            if (discs != null) discs.SetActive(false);                       // 하늘 셰이더의 해 원반만
            foreach (var s in Suns)
            {
                // 방향광: 빛이 오는 방향 = 해 쪽. transform.forward = 빛이 가는 방향
                var toSun = Quaternion.Euler(-s.el, s.az, 0f) * Vector3.forward;
                sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
                sun.color = s.col; sun.intensity = s.inten;
                if (sky.HasProperty("_SunElevation")) { sky.SetFloat("_SunElevation", Mathf.Sin(s.el * Mathf.Deg2Rad)); sky.SetFloat("_SunAzimuth", s.az); }
                foreach (var v in new[] { Views[0], Views[1], Views[3] })
                {
                    var eye = v.eye; var t = Terrain.activeTerrain;
                    if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
                    string p = string.Format("Assets/_preview/sun/{0}_{1}_{2}.png", tag, s.n, v.n);
                    Shot(cam, eye, v.look, v.fov, p);
                    sb.AppendLine("  " + p);
                }
            }
        }
        finally
        {
            RenderSettings.skybox = sky0; Object.DestroyImmediate(sky);
            sun.transform.rotation = q0; sun.color = c0; sun.intensity = i0;
            if (discs != null) discs.SetActive(d0);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (tod != null) { tod.index = 0; tod.Apply(); }
        }
        AssetDatabase.Refresh();
        File.AppendAllText(LOG, sb.ToString());
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var t = new Texture2D(W, H, TextureFormat.RGB24, false);
        t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, t.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(t); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
