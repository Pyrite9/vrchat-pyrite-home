// Tools ▸ Pyrite ▸ Z14. Dock Mood (plank tone + root lantern)  /  Z14b. Revert
//  Z13(노멀·AO) 뒤에도 인게임 체감이 없었다 — 판자 하나하나가 아니라 부두 전체가 한 톤인 게 문제.
//   B 판자 톤: T_DockWood_V.png (Tools/python/mk_dock_tone.py) — 띠(=판자)마다 밝기 0.62~1.18 · 채도 · 색 온도,
//             길이 방향 얼룩 ±8%, 판자 끝 10 cm 어둡게. M_DockWood._MainTex 교체 (원본 T_DockWood.png 는 그대로)
//   A 빛 변화: 부두 뿌리에 두 번째 랜턴(끝 랜턴 복제) — 따뜻한 빛 웅덩이 두 개 사이로 달빛 구간이 생긴다.
//             캠프 조명 배열(PyriteTimeOfDay.campLights)에 넣어 시간대 배수를 같이 받는다. 그림자 없음.
//  단계별 렌더: dock/mood0(전) → mood1(톤) → mood2(톤+빛). Logs/pyrite_dockmood.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteDockMood
{
    const string MAT = "Assets/Materials/M_DockWood.mat";
    const string TEX_V = "Assets/TerrainAssets/T_DockWood_V.png";
    const string TEX_0 = "Assets/TerrainAssets/T_DockWood.png";
    const string ROOT_LANTERN = "Lantern_DockRoot";
    const float ROOT_INTENSITY = 0.7f, ROOT_RANGE = 5.5f;

    [MenuItem("Tools/Pyrite/Z14. Dock Mood (plank tone + root lantern)", false, 28)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z14] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (m == null || dock == null || tod == null) { sb.AppendLine("준비 안 됨"); Flush(sb); return; }

        foreach (var l in dock.GetComponentsInChildren<Light>(true))
            sb.AppendLine(string.Format("  기존 {0}: {1} int {2} range {3} color {4} shadows {5} mode {6} inCamp {7}",
                PathOf(l.gameObject), l.type, l.intensity, l.range, l.color, l.shadows, l.renderMode, tod.campLights != null && tod.campLights.Contains(l)));
        sb.AppendLine("campLights " + (tod.campLights == null ? 0 : tod.campLights.Length) + ", campLightMul " + string.Join("/", tod.campLightMul ?? new float[0]));

        sb.Append(PyriteDockLightmap.CaptureTagged("mood0"));

        // B — 판자 톤
        AssetDatabase.ImportAsset(TEX_V, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(TEX_V) as TextureImporter;
        var t0 = AssetImporter.GetAtPath(TEX_0) as TextureImporter;
        if (ti != null)
        {
            ti.sRGBTexture = true; ti.wrapMode = TextureWrapMode.Repeat; ti.anisoLevel = 8; ti.mipmapEnabled = true;
            if (t0 != null) { ti.maxTextureSize = t0.maxTextureSize; ti.filterMode = t0.filterMode; }
            ti.SaveAndReimport();
        }
        Undo.RecordObject(m, "dock tone");
        m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_V));
        EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        sb.AppendLine("  _MainTex -> T_DockWood_V");
        sb.Append(PyriteDockLightmap.CaptureTagged("mood1"));

        // A — 뿌리 랜턴
        var old = dock.transform.Find(ROOT_LANTERN);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var endLantern = dock.transform.Find("Lantern_Dock");
        var dmr = dock.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "default");
        if (endLantern == null || dmr == null) { sb.AppendLine("Lantern_Dock / DockMesh 없음 — 빛 단계 생략"); Flush(sb); return; }
        var b = dmr.bounds;
        var go = Object.Instantiate(endLantern.gameObject, dock.transform);
        go.name = ROOT_LANTERN;
        Undo.RegisterCreatedObjectUndo(go, "root lantern");
        // 덱 윗면 y 0.50, 뭍 쪽 끝에서 0.45 m 안, 오른쪽 가장자리(스위치 반대편)
        var endPos = endLantern.position;
        float lift = endPos.y - 1.25f;                                    // 끝 랜턴은 계선주(1.25) 위 — 같은 높이 차로 덱 위에
        go.transform.position = new Vector3(b.max.x - 0.18f, 0.50f + Mathf.Max(0f, lift), b.max.z - 0.45f);
        var lights = go.GetComponentsInChildren<Light>(true);
        foreach (var l in lights)
        {
            Undo.RecordObject(l, "root lantern light");
            l.intensity = ROOT_INTENSITY; l.range = ROOT_RANGE; l.shadows = LightShadows.None;
            l.renderMode = LightRenderMode.Auto; l.lightmapBakeType = LightmapBakeType.Realtime;
        }
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) GameObjectUtility.SetStaticEditorFlags(r.gameObject, 0);
        sb.AppendLine(string.Format("  {0} at {1} (끝 랜턴 {2}), 빛 {3}개 int {4} range {5}", ROOT_LANTERN, go.transform.position, endPos, lights.Length, ROOT_INTENSITY, ROOT_RANGE));

        if (lights.Length > 0)
        {
            Undo.RecordObject(tod, "camp lights");
            var list = (tod.campLights ?? new Light[0]).Where(x => x != null && !lights.Contains(x)).ToList();
            list.AddRange(lights);
            tod.campLights = list.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(tod);
            EditorUtility.SetDirty(tod);
            var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(tod, false);
            sb.AppendLine("  campLights -> " + tod.campLights.Length);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.Append(PyriteDockLightmap.CaptureTagged("mood2"));
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite/Z14b. Dock Mood Revert", false, 29)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z14b]\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (m != null) { m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TEX_0)); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets(); }
        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var rl = dock == null ? null : dock.transform.Find(ROOT_LANTERN);
        if (rl != null)
        {
            var ls = rl.GetComponentsInChildren<Light>(true);
            if (tod != null && tod.campLights != null) { tod.campLights = tod.campLights.Where(x => x != null && !ls.Contains(x)).ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(tod); EditorUtility.SetDirty(tod); }
            Undo.DestroyObjectImmediate(rl.gameObject);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("reverted"); Flush(sb);
    }

    static string PathOf(GameObject g) { var s = g.name; for (var t = g.transform.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_dockmood.txt", sb.ToString()); }
}
#endif
