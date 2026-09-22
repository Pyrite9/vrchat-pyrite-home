using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// 시간대별 리플렉션 프로브 큐브맵 굽기
//
//  라이트맵은 한 벌뿐이고 런타임 교체가 막혀 있다(LightmapSettings 는 Udon 차단).
//  하지만 물 반사는 라이트맵이 아니라 리플렉션 프로브에서 온다.
//  ReflectionProbe.customBakedTexture 는 Udon 에 열려 있으므로
//  프리셋마다 큐브맵을 따로 구워두고 PyriteTimeOfDay.Apply() 에서 갈아 끼운다.
//
//  T  : 프리셋 3개 × 프로브 4개 = 큐브맵 12장 굽기 → 프로브를 Custom 모드로 전환 → TimeOfDay 에 배선
//  T2 : 프리셋별로 부두에서 호수를 본 렌더 3장 (적용이 되는지 눈으로 확인)
public static class PyriteReflectionSets
{
    const string DIR = "Assets/Reflections/";
    static readonly string[] LABEL = { "Dusk", "Night", "Dawn" };   // 파일명엔 한글을 쓰지 않는다

    [MenuItem("Tools/Pyrite/T. Bake Reflection Sets", false, 291)]
    public static void Bake()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) { Debug.LogError("[REFL] PyriteTimeOfDay 없음"); return; }

        var probes = Object.FindObjectsOfType<ReflectionProbe>(true)
                           .Where(p => p.mode != ReflectionProbeMode.Realtime)
                           .OrderBy(p => p.name).ToArray();
        if (probes.Length == 0) { Debug.LogError("[REFL] 프로브 없음"); return; }

        Directory.CreateDirectory(DIR);
        int np = probes.Length;
        int ns = tod.presetName.Length;
        var cubes = new Texture[ns * np];

        // 캡처 동안엔 Baked 모드로 둔다 — Custom 상태에서 예전 큐브맵이 서로의 캡처에 섞이는 걸 줄인다
        foreach (var p in probes) { Undo.RecordObject(p, "probe mode"); p.mode = ReflectionProbeMode.Baked; }
        tod.probes = null; tod.probeCubes = null;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            for (int i = 0; i < ns; i++)
            {
                tod.index = i;
                tod.Apply();                      // 스카이박스·앰비언트·포그·태양·머티리얼 전부 이 프리셋으로

                for (int k = 0; k < np; k++)
                {
                    string label = i < LABEL.Length ? LABEL[i] : ("P" + i);
                    string path = DIR + probes[k].name + "_" + i + "_" + label + ".exr";
                    EditorUtility.DisplayProgressBar("리플렉션 프로브 굽기",
                        label + " — " + probes[k].name, (i * np + k) / (float)(ns * np));

                    bool ok = Lightmapping.BakeReflectionProbe(probes[k], path);
                    if (!ok) { Debug.LogError("[REFL] 굽기 실패: " + path); continue; }
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    cubes[i * np + k] = AssetDatabase.LoadAssetAtPath<Texture>(path);
                }
            }
        }
        finally { EditorUtility.ClearProgressBar(); }

        // Custom 모드로 전환하고 노을 큐브맵을 기본값으로
        for (int k = 0; k < np; k++)
        {
            probes[k].mode = ReflectionProbeMode.Custom;
            probes[k].customBakedTexture = cubes[k];
            EditorUtility.SetDirty(probes[k]);
        }

        tod.probes = probes;
        tod.probeCubes = cubes;
        tod.index = 0;
        tod.Apply();
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

        int got = cubes.Count(c => c != null);
        Debug.Log(string.Format("[REFL] 완료 — 큐브맵 {0}/{1}장, {2:F1}초\n  프로브 순서: {3}\n  → 프로브는 Custom 모드. 라이트맵을 다시 구워도 이 큐브맵은 덮이지 않는다",
            got, ns * np, sw.Elapsed.TotalSeconds, string.Join(", ", probes.Select(p => p.name))));
    }

    [MenuItem("Tools/Pyrite/T2. Capture Reflection Check", false, 292)]
    public static void Check()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) { Debug.LogError("[REFL] PyriteTimeOfDay 없음"); return; }
        Directory.CreateDirectory("Assets/_preview/");

        // 부두 중간에서 호수 쪽으로 — 수면이 화면 대부분을 채우게 살짝 내려본다
        var eye  = new Vector3(-10.0f, 2.00f, 38.0f);
        var look = new Vector3(-6.0f, -0.6f, 10.0f);

        int ns = tod.presetName.Length;
        for (int i = 0; i < ns; i++)
        {
            tod.index = i;
            tod.Apply();
            Shot(eye, look, "Assets/_preview/refl_" + i + ".png");
        }
        tod.index = 0;
        tod.Apply();
        AssetDatabase.Refresh();
        Debug.Log("[REFL] 확인 렌더 " + ns + "장 — Assets/_preview/refl_*.png");
    }

    static void Shot(Vector3 eye, Vector3 look, string path)
    {
        var go = new GameObject("__shot");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        cam.fieldOfView = 60f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 900f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
