// 외부 unitypackage 임포트 (에디터 전용) — 파일 대화상자 없이 경로로 바로 넣는다
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyriteImportTools
{
    const string FLOWERS_PKG =
        @"E:\VRC\FlowersGrassland\FlowersGrassland\FlowersGrassland.unitypackage";

    [MenuItem("Tools/Pyrite/G. Import FlowersGrassland &#7")]
    public static void ImportFlowers()
    {
        if (!File.Exists(FLOWERS_PKG))
        { Debug.LogError("[Pyrite] 패키지 없음: " + FLOWERS_PKG); return; }
        Debug.Log("[Pyrite] 임포트 시작 (" + (new FileInfo(FLOWERS_PKG).Length / 1048576) + " MB): " + FLOWERS_PKG);
        AssetDatabase.ImportPackage(FLOWERS_PKG, false);
    }
}
#endif
