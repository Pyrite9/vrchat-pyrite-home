// PyriteDrunkBuild.cs — 맥주 취기 효과 (Z57a 빌드 / Z57b 되돌림 / Z57c ClientSim 시험). 재실행 안전
//  2026-10-05 관리자: 화면 효과만 · 2병(16모금)에 최대 · 3분에 깸 · 설정 토글(기본 켬)
//  루트 DrunkFX: U# PyriteDrunk + 자식 PP_Drunk(전역 PostProcessVolume, layer 23, priority 9, weight 0, 프로필 Assets/TerrainAssets/PP_Drunk.asset)
//  맥주 12병의 PyriteBeer.drunk · 설정 PyriteSettings.drunk 를 연결. 설정 토글 자체는 Z25a 가 만든다 → 순서: Z57a → Z25a
//  ⚠ Z53b(맥주 재빌드)는 FindObjectOfType 로 다시 연결하지만, DrunkFX 를 지웠다 만들면(Z57a 재실행) 여기서 다시 연결한다
//  결과 Logs/pyrite_drunk.txt, 렌더 Assets/_preview/drunk/drunk_{000,050,100}.jpg
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteDrunkBuild
{
    const string PROFILE = "Assets/TerrainAssets/PP_Drunk.asset";
    const string PREV = "Assets/_preview/drunk/";
    const string LOG = "Logs/pyrite_drunk.txt";
    const string TLOG = "Logs/pyrite_drunk_test.txt";
    const string KEY = "pyrite_drunktest";
    const int PP_LAYER = 23;
    static StringBuilder sb;

    static PyriteDrunkBuild() { EditorApplication.update += Tick; }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    [MenuItem("Tools/Pyrite4/Z57a. Drunk FX Build", false, 6301)]
    public static void Build()
    {
        sb = new StringBuilder("[Z57a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) sb.AppendLine("RESULT: DONE (다음: Z25a 로 설정 토글 만들기)"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite4/Z57b. Drunk FX Revert", false, 6302)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z57b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = Root("DrunkFX"); if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("DrunkFX 삭제 (맥주·설정의 drunk 참조는 null 이 되어 효과 없음)"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (프로필 에셋은 남김)");
        Flush();
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Assets ▸ Refresh 후 Z57a 다시");
        return false;
    }

    static T AddS<T>(PostProcessProfile p) where T : PostProcessEffectSettings
    {
        var s = p.AddSettings<T>();
        s.name = typeof(T).Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, p);      // 🔴 이게 없으면 재시작 때 효과가 빈 참조가 된다 (00 문서 5.1)
        return s;
    }

    static bool Inner()
    {
        if (!EnsureProgram("PyriteDrunk")) return false;

        // 프로필
        var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(PROFILE);
        if (p == null) { p = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(p, PROFILE); }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(PROFILE).Where(o => o is PostProcessEffectSettings).ToArray()) Object.DestroyImmediate(o, true);
        p.settings.Clear();
        var ca = AddS<ChromaticAberration>(p); ca.enabled.Override(true); ca.intensity.Override(0.85f); ca.fastMode.Override(true);
        var vg = AddS<Vignette>(p); vg.enabled.Override(true); vg.intensity.Override(0.28f); vg.smoothness.Override(0.6f); vg.color.Override(new Color(0.05f, 0.02f, 0f, 1f));
        var bl = AddS<Bloom>(p); bl.enabled.Override(true); bl.intensity.Override(3.0f); bl.threshold.Override(0.85f); bl.softKnee.Override(0.7f);
        var cg = AddS<ColorGrading>(p); cg.enabled.Override(true); cg.saturation.Override(25f); cg.contrast.Override(-4f); cg.temperature.Override(12f); cg.postExposure.Override(1.3f);   // 1차(가장자리 0.40 · 대비 −12)는 100% 에서 평균 밝기 75.7 → 56.7 로 침침했다. 기본 프로필 노출 1.0 기준 +0.3 EV
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        sb.AppendLine("프로필 " + PROFILE + ": 색 번짐 0.85 · 가장자리 0.28 · 블룸 3.0(문턱 0.85) · 채도 +25 · 대비 −4 · 색온도 +12 · 노출 1.3, 효과 " + p.settings.Count + "개");

        // 씬
        var old = Root("DrunkFX"); if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("DrunkFX");
        var vgo = new GameObject("PP_Drunk"); vgo.layer = PP_LAYER; vgo.transform.SetParent(root.transform, false);
        var vol = vgo.AddComponent<PostProcessVolume>(); vol.isGlobal = true; vol.priority = 9; vol.weight = 0f; vol.sharedProfile = p;
        var d = UdonSharpUndo.AddComponent<PyriteDrunk>(root);
        d.volume = vol; d.sipsToMax = 16; d.soberSeconds = 180f; d.allow = true; d.level = 0f;
        UdonSharpEditorUtility.CopyProxyToUdon(d); EditorUtility.SetDirty(d);
        sb.AppendLine("DrunkFX: 볼륨 전역 · priority 9 · weight 0, 16모금에 최대 · 180 s 에 깸 · 기본 켬");

        // 연결
        var beers = Object.FindObjectsOfType<PyriteBeer>(true);
        foreach (var b in beers) { b.drunk = d; UdonSharpEditorUtility.CopyProxyToUdon(b); EditorUtility.SetDirty(b); }
        sb.AppendLine("맥주 연결 " + beers.Length + "병" + (beers.Length == 0 ? " — !! BeerCooler 없음" : ""));
        var st = Object.FindObjectOfType<PyriteSettings>(true);
        if (st != null) { st.drunk = d; UdonSharpEditorUtility.CopyProxyToUdon(st); EditorUtility.SetDirty(st); sb.AppendLine("설정 연결 (토글 " + (st.drunkToggle != null ? "있음" : "없음 — Z25a 를 돌려야 생김") + ")"); }
        else sb.AppendLine("!! PyriteSettings 없음");

        // 렌더: 캠프에서 모닥불·호수 쪽, weight 0 / 0.5 / 1
        Directory.CreateDirectory(PREV);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; float n0 = cam.nearClipPlane;
        try
        {
            Vector3 eye = new Vector3(-10.9f, 3.35f, 56.3f); Vector3 at = new Vector3(-10.4f, 2.3f, 50.5f);
            float[] ws = { 0f, 0.5f, 1f }; string[] tags = { "000", "050", "100" };
            for (int i = 0; i < ws.Length; i++)
            {
                vol.weight = ws[i];
                sb.AppendLine("  shot drunk_" + tags[i] + " (weight " + ws[i].ToString("F1") + ") " + Shot(cam, eye, at, PREV + "drunk_" + tags[i] + ".jpg"));
            }
        }
        finally { vol.weight = 0f; cam.targetTexture = null; cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.nearClipPlane = n0; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    static string Shot(Camera cam, Vector3 eye, Vector3 at, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((at - eye).normalized, Vector3.up));
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev; cam.targetTexture = null;
        File.WriteAllBytes(path, tx.EncodeToJPG(88));
        var px = tx.GetPixels32();
        double all = 0, corner = 0, sat = 0; int nc = 0;
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
        {
            var q = px[y * W + x]; double l = (q.r + q.g + q.b) / 3.0; all += l;
            int mx = Mathf.Max(q.r, Mathf.Max(q.g, q.b)), mn = Mathf.Min(q.r, Mathf.Min(q.g, q.b)); if (mx > 0) sat += (mx - mn) / (double)mx;
            if ((x < 120 || x >= W - 120) && (y < 80 || y >= H - 80)) { corner += l; nc++; }
        }
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        return string.Format("평균 밝기 {0:F1} · 모서리 밝기 {1:F1} · 평균 채도 {2:F3}", all / (W * H), corner / nc, sat / (W * H));
    }

    // ── ClientSim 시험 ──
    static double t0 = -1; static int step = 0;

    [MenuItem("Tools/Pyrite4/Z57c. Drunk FX Play Test (ClientSim)", false, 6303)]
    public static void Test()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(TLOG, "[Z57c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        SessionState.SetBool(KEY, true);
        EditorApplication.isPlaying = true;
    }

    static void W(string s) { File.AppendAllText(TLOG, s + "\n"); }
    static int fails;
    static void Check(string name, bool ok, string detail) { W((ok ? "  OK   " : "  FAIL ") + name + " — " + detail); if (!ok) fails++; }

    static UdonBehaviour Ub(GameObject g, string prog) { return g ? g.GetComponents<UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == prog) : null; }

    static void Tick()
    {
        if (!SessionState.GetBool(KEY, false)) return;
        if (!EditorApplication.isPlaying) { if (step > 0) { SessionState.SetBool(KEY, false); step = 0; t0 = -1; } return; }
        if (t0 < 0) { t0 = EditorApplication.timeSinceStartup; step = 1; fails = 0; return; }
        double t = EditorApplication.timeSinceStartup - t0;
        try
        {
            var root = Root("DrunkFX"); var d = Ub(root, "PyriteDrunk");
            var vol = root ? root.GetComponentInChildren<PostProcessVolume>(true) : null;
            if (step == 1 && t > 6)
            {
                if (d == null || vol == null) { W("!! DrunkFX 없음"); step = 90; return; }
                var beers = Object.FindObjectsOfType<UdonBehaviour>(true).Where(u => u.programSource != null && u.programSource.name == "PyriteBeer").ToArray();
                int wired = beers.Count(b => b.GetProgramVariable("drunk") != null);
                Check("맥주 → 취기 연결", beers.Length > 0 && wired == beers.Length, wired + " / " + beers.Length + "병");
                Check("시작 weight 0", vol.weight == 0f, "weight " + vol.weight.ToString("F3") + ", level " + d.GetProgramVariable("level"));
                d.SendCustomEvent("AddSip");
                step++;
            }
            else if (step == 2 && t > 6.5)
            {
                float lv = (float)d.GetProgramVariable("level");
                Check("한 모금 → level ≈ 1/16", lv > 0.055f && lv < 0.0626f, "level " + lv.ToString("F4") + ", weight " + vol.weight.ToString("F3"));
                for (int i = 0; i < 20; i++) d.SendCustomEvent("AddSip");
                step++;
            }
            else if (step == 3 && t > 7)
            {
                float lv = (float)d.GetProgramVariable("level");
                Check("21모금 → level 최대 1 에서 멈춤", lv > 0.99f && lv <= 1f, "level " + lv.ToString("F4") + ", weight " + vol.weight.ToString("F3"));
                Check("취했을 때 weight 0.8 이상", vol.weight > 0.8f, "weight " + vol.weight.ToString("F3"));
                d.SetProgramVariable("allow", false);
                step++;
            }
            else if (step == 4 && t > 7.5)
            {
                Check("설정 끔 → weight 0 (level 은 유지)", vol.weight == 0f && (float)d.GetProgramVariable("level") > 0.9f, "weight " + vol.weight.ToString("F3") + ", level " + ((float)d.GetProgramVariable("level")).ToString("F3"));
                d.SetProgramVariable("allow", true);
                d.SetProgramVariable("soberSeconds", 4f);    // 깨는 속도만 빠르게 (4 s 에 1 → 0)
                step++;
            }
            else if (step == 5 && t > 9.5)
            {
                float lv = (float)d.GetProgramVariable("level");
                Check("2 s 뒤 절반쯤 깸 (4 s 에 깨는 설정)", lv > 0.3f && lv < 0.7f, "level " + lv.ToString("F3") + ", weight " + vol.weight.ToString("F3"));
                step++;
            }
            else if (step == 6 && t > 13)
            {
                Check("다 깨면 weight 0", vol.weight == 0f && (float)d.GetProgramVariable("level") == 0f, "level " + d.GetProgramVariable("level") + ", weight " + vol.weight.ToString("F3"));
                // 설정 토글 경로
                var sp = Object.FindObjectsOfType<UdonBehaviour>(true).FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteSettings");
                var tg = sp != null ? sp.GetProgramVariable("drunkToggle") as UnityEngine.UI.Toggle : null;
                if (tg == null) W("  (설정 토글 없음 — Z25a 전)");
                else
                {
                    d.SetProgramVariable("soberSeconds", 180f);
                    tg.isOn = false;
                    Check("설정 토글 끔 → allow false", !(bool)d.GetProgramVariable("allow"), "allow " + d.GetProgramVariable("allow"));
                    tg.isOn = true;
                    Check("설정 토글 켬 → allow true", (bool)d.GetProgramVariable("allow"), "allow " + d.GetProgramVariable("allow"));
                }
                W("RESULT: " + (fails == 0 ? "PASS" : "FAIL " + fails));
                step++; EditorApplication.isPlaying = false;
            }
            else if (step == 90) { W("RESULT: FAIL (없음)"); step++; EditorApplication.isPlaying = false; }
        }
        catch (System.Exception e) { W("EXCEPTION " + e); step = 99; EditorApplication.isPlaying = false; }
    }
}
#endif
