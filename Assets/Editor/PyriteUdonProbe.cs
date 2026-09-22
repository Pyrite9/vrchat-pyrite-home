// Udon 화이트리스트 검증 — 에디터 전용
//  UdonSharp는 "프로그램 에셋이 붙은" 스크립트만 컴파일한다.
//  그래서 .cs 파일을 프로젝트에 넣어두기만 해서는 검사가 안 된다(실측으로 확인).
//  씬에 컴포넌트를 붙여서 프로그램 에셋을 만들어야 비로소 컴파일 대상이 된다.
#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PyriteUdonProbe
{
    [MenuItem("Tools/Pyrite/H. Udon Whitelist Probe &7")]
    public static void Probe()
    {
        // 1) 프로젝트에 있는 UdonSharpBehaviour 파생 타입을 전부 센다
        var baseType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.FullName == "UdonSharp.UdonSharpBehaviour");
        if (baseType == null) { Debug.LogError("[Pyrite] UdonSharpBehaviour 타입을 못 찾음"); return; }

        var derived = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract && t != baseType)
            .OrderBy(t => t.Name).ToList();

        Debug.Log("[Pyrite] UdonSharpBehaviour 파생 타입 " + derived.Count + "개: "
                  + string.Join(", ", derived.Select(t => t.Name)));

        bool hasProbe = derived.Any(t => t.Name.StartsWith("PyriteLightmapProbe"));
        bool hasCtrl  = derived.Any(t => t.Name == "PyriteUdonControl");
        Debug.Log("[Pyrite] PyriteLightmapProbe 존재=" + hasProbe + " / PyriteUdonControl 존재=" + hasCtrl);

        // 2) 스크립트마다 UdonSharpProgramAsset을 만들어 짝지어 준다.
        //    이게 있어야 UdonSharp 컴파일러가 그 스크립트를 화이트리스트 검사한다.
        var paType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return new Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        if (paType == null) { Debug.LogError("[Pyrite] UdonSharpProgramAsset 타입 못 찾음"); return; }

        // Assets/Udon 의 모든 .cs 에 대해 프로그램 에셋을 만든다
        var csGuids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Udon" });
        var names = csGuids.Select(g => System.IO.Path.GetFileNameWithoutExtension(
                        AssetDatabase.GUIDToAssetPath(g))).Distinct().ToArray();
        Debug.Log("[Pyrite] Assets/Udon 스크립트 " + names.Length + "개: " + string.Join(", ", names));
        foreach (var name in names)
        {
            var csPath = "Assets/Udon/" + name + ".cs";
            var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(csPath);
            if (ms == null) { Debug.LogError("[Pyrite] 스크립트 없음: " + csPath); continue; }

            var assetPath = "Assets/Udon/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (existing != null) { Debug.Log("[Pyrite] 프로그램 에셋 이미 있음: " + assetPath); continue; }

            var pa = ScriptableObject.CreateInstance(paType);
            AssetDatabase.CreateAsset(pa, assetPath);
            var so = new SerializedObject(pa);
            var prop = so.FindProperty("sourceCsScript");
            if (prop == null) { Debug.LogError("[Pyrite] sourceCsScript 프로퍼티 없음 (" + name + ")"); continue; }
            prop.objectReferenceValue = ms;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pa);
            Debug.Log("[Pyrite] 프로그램 에셋 생성: " + assetPath);
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Pyrite] ★ 이제 Ctrl+R 하고 콘솔을 볼 것. "
                + "PyriteUdonControl(대조군)이 에러를 내야 검사가 동작하는 것이고, "
                + "그 상태에서 PyriteLightmapProbe가 조용하면 라이트맵 교체는 Udon에서 가능하다.");

    }
}
#endif
