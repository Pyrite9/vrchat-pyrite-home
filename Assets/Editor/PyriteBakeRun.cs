// 라이트맵 베이크 실행 — 에디터 전용
#if UNITY_EDITOR
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PyriteBakeRun
{
    static Stopwatch sw;

    [MenuItem("Tools/Pyrite/F. Bake Lighting Now &#8")]
    public static void Bake()
    {
        if (Lightmapping.isRunning) { Debug.LogWarning("[Pyrite] 이미 베이크 중"); return; }
        sw = Stopwatch.StartNew();
        Lightmapping.bakeCompleted -= Done;
        Lightmapping.bakeCompleted += Done;
        Debug.Log("[Pyrite] 베이크 시작 — " + Lightmapping.lightingSettings.name
                  + " / lightmapper " + Lightmapping.lightingSettings.lightmapper
                  + " / resolution " + Lightmapping.lightingSettings.lightmapResolution
                  + " / maxSize " + Lightmapping.lightingSettings.lightmapMaxSize);
        Lightmapping.BakeAsync();
    }

    static void Done()
    {
        Lightmapping.bakeCompleted -= Done;
        long bytes = 0; int n = 0;
        foreach (var d in LightmapSettings.lightmaps)
        {
            n++;
            if (d.lightmapColor != null)
                bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(d.lightmapColor);
            if (d.shadowMask != null)
                bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(d.shadowMask);
        }
        Debug.Log(string.Format("[Pyrite] 베이크 완료 — {0:0.0}분 / 라이트맵 {1}장 / 런타임 {2:0.0} MB",
                  sw.Elapsed.TotalMinutes, n, bytes / 1048576.0));
    }


    [MenuItem("Tools/Pyrite/F3. Cancel Bake")]
    public static void CancelBake()
    {
        Lightmapping.Cancel();
        Lightmapping.bakeCompleted -= Done;
        Debug.Log("[Pyrite] 베이크 취소 요청");
    }

    // GPU 라이트매퍼가 'InitializeLightmapData job ... exit code 2'로 실패할 때 쓴다.
    // CPU는 느리지만 같은 씬에서 안 터진다.
    [MenuItem("Tools/Pyrite/F4. Bake With CPU Lightmapper")]
    public static void BakeCpu()
    {
        if (Lightmapping.isRunning) { Lightmapping.Cancel(); }
        var ls = Lightmapping.lightingSettings;
        if (ls == null) { Debug.LogError("[Pyrite] LightingSettings 없음"); return; }
        Undo.RecordObject(ls, "cpu lightmapper");
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        EditorUtility.SetDirty(ls);
        AssetDatabase.SaveAssets();
        Debug.Log("[Pyrite] 라이트매퍼를 ProgressiveCPU로 전환");
        Bake();
    }

    [MenuItem("Tools/Pyrite/F5. Bake With GPU Lightmapper")]
    public static void BakeGpu()
    {
        if (Lightmapping.isRunning) { Lightmapping.Cancel(); }
        var ls = Lightmapping.lightingSettings;
        if (ls == null) { Debug.LogError("[Pyrite] LightingSettings 없음"); return; }
        Undo.RecordObject(ls, "gpu lightmapper");
        ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
        EditorUtility.SetDirty(ls);
        AssetDatabase.SaveAssets();
        Bake();
    }

    [MenuItem("Tools/Pyrite/F2. Bake Status")]
    public static void Status()
    {
        Debug.Log("[Pyrite] 베이크 중=" + Lightmapping.isRunning
                  + " / 진행 " + (Lightmapping.buildProgress * 100f).ToString("0.0") + "%"
                  + " / 라이트맵 " + LightmapSettings.lightmaps.Length + "장");
    }
}
#endif
