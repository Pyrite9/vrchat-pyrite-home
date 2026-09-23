// Tools ▸ Pyrite2 ▸ Z33a. Bake Chain (F → T → Z23a → moon)
//  Pyrite 메뉴가 화면을 넘어 F·T 를 누를 수 없어서 Pyrite2 에 묶었다.
//  🔴 T(PyriteReflectionSets.Bake) 는 끝에 옛 ToD 를 노을(index 0)로 Apply → Sky_Moon 을 끈다(하늘에 달이 사라진 원인, Z32a 실측).
//     그래서 체인 끝에서 Sky_Moon 을 다시 켜고 DayCycle 을 에디터 시각으로 되돌린다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class PyriteBakeChain
{
    static System.Diagnostics.Stopwatch sw;

    [MenuItem("Tools/Pyrite2/Z33a. Bake Chain (F > T > Z23a > moon)", false, 70)]
    public static void Run()
    {
        if (Lightmapping.isRunning) { Log("이미 베이크 중"); return; }
        sw = System.Diagnostics.Stopwatch.StartNew();
        Log("[Z33a] " + System.DateTime.Now.ToString("HH:mm:ss") + " F 시작");
        Lightmapping.bakeCompleted -= AfterBake;
        Lightmapping.bakeCompleted += AfterBake;
        PyriteBakeRun.Bake();
    }

    // 🔴 Z33c 실측: LightingSettings 혼합 모드가 Shadowmask 로 돌아가 있었다(마지막 커밋 d984a58 은 Baked Indirect).
    //    해가 움직이는데 Shadowmask 면 정적 그림자가 18:20 해 방향으로 구워져 낮에 절벽·꽃이 어두워진다(절벽 42→15) → Baked Indirect 로 되돌리고 다시 굽는다
    [MenuItem("Tools/Pyrite2/Z33d. Baked Indirect + Bake Chain", false, 73)]
    public static void IndirectAndBake()
    {
        var ls = Lightmapping.lightingSettings;
        if (ls == null || Lightmapping.isRunning) { Log("LightingSettings 없음 또는 베이크 중"); return; }
        Log("[Z33d] " + System.DateTime.Now.ToString("HH:mm:ss") + " mixed " + ls.mixedBakeMode + " -> IndirectOnly");
        ls.mixedBakeMode = MixedLightingMode.IndirectOnly;
        EditorUtility.SetDirty(ls);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Run();
    }

    // 🔴 Baked Indirect 로 다시 구워도 정오 절벽 33(커밋본 42)·꽃 38(59)로 여전히 어두웠다.
    //    커밋된 라이트맵(d984a58, 09-23 18:42)은 DayCycle(Z18) 이전 = 옛 ToD 노을 프리셋 상태(스카이박스 앰비언트)에서 구웠다.
    //    지금 에디터 상태(DayCycle 18:20, Trilight 앰비언트)로 구우면 간접광이 더 어둡다 → 옛 ToD 노을을 Apply 한 상태로 굽는다
    [MenuItem("Tools/Pyrite2/Z33e. Legacy Dusk + Baked Indirect + Bake Chain", false, 74)]
    public static void LegacyDuskBake()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>(true);
        if (tod == null) { Log("PyriteTimeOfDay 없음"); return; }
        tod.index = 0;
        tod.Apply();
        Log("[Z33e] " + System.DateTime.Now.ToString("HH:mm:ss") + " 옛 ToD 노을 Apply → ambient " + RenderSettings.ambientMode + " skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name : "-") + " int " + RenderSettings.ambientIntensity);
        IndirectAndBake();
    }

    [MenuItem("Tools/Pyrite2/Z33b. Reflections Only (T > Z23a > moon)", false, 71)]
    public static void AfterBakeMenu() { sw = System.Diagnostics.Stopwatch.StartNew(); Log("[Z33b] " + System.DateTime.Now.ToString("HH:mm:ss")); Reflections(); }

    static void AfterBake()
    {
        Lightmapping.bakeCompleted -= AfterBake;
        Log(string.Format("  F 완료 {0:0.0}분 / 라이트맵 {1}장 / mixed {2} / shadowmask {3}", sw.Elapsed.TotalMinutes, LightmapSettings.lightmaps.Length, Lightmapping.lightingSettings.mixedBakeMode, LightmapSettings.lightmaps.Count(d => d.shadowMask != null)));
        EditorApplication.delayCall += Reflections;
    }

    static void Reflections()
    {
        try
        {
            PyriteReflectionSets.Bake();
            Log(string.Format("  T 완료 {0:0.0}분", sw.Elapsed.TotalMinutes));
            PyriteDayCubes.Run();
            Log(string.Format("  Z23a 완료 {0:0.0}분", sw.Elapsed.TotalMinutes));
        }
        catch (System.Exception e) { Log("  EXCEPTION " + e.Message); }
        foreach (var g in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.name == "Sky_Moon"))
        {
            Log("  Sky_Moon activeSelf " + g.activeSelf + " → True");
            g.SetActive(true); EditorUtility.SetDirty(g);
        }
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Log("RESULT: DONE");
    }

    static void Log(string s) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_bakechain.txt", s + "\n"); }
}
#endif
