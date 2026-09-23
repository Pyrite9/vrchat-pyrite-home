// Tools ▸ Pyrite ▸ Y2. Trim Landmark Towers
//  중앙(Landmark_1)·왼쪽(Landmark_2) 기둥이 하늘을 가린다 → 2개씩만 남긴다.
//   Landmark_1 : 맨 아래 두 결정(Shard_01 + 옆에 붙은 Shard_06) — 세로로 선 구성
//   Landmark_2 : Shard_01 + Shard_02 를 땅으로 내려 옆에 비스듬히 기대게 — 가로로 퍼진 구성
//  나머지는 삭제하지 않고 비활성화 (되돌리려면 다시 켜면 된다)
#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLandmarkTrim
{
    [MenuItem("Tools/Pyrite/Y2. Trim Landmark Towers", false, 291)]
    public static void Run()
    {
        var log = new StringBuilder("[Y2] ");
        var crystals = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Crystals");
        if (crystals == null) { Debug.LogError("[Y2] Crystals 없음"); return; }
        Transform L1 = null, L2 = null;
        foreach (Transform c in crystals.transform)
        {
            if (c.name.StartsWith("Landmark_1")) L1 = c;
            if (c.name.StartsWith("Landmark_2")) L2 = c;
        }
        if (L1 == null || L2 == null) { Debug.LogError("[Y2] Landmark_1/2 없음"); return; }

        Keep(L1, new[] { "Shard_01", "Shard_06" }, log);
        Keep(L2, new[] { "Shard_01", "Shard_02" }, log);

        // Landmark_2 : Shard_02 를 Shard_01 옆 땅으로 내리고 그쪽으로 기대게
        var s1 = L2.Find("Shard_01"); var s2 = L2.Find("Shard_02");
        Undo.RecordObject(s2, "lean shard");
        Vector3 side = new Vector3(-0.80f, 0f, 0.60f).normalized;              // 로컬 기준 옆 방향
        s2.localPosition = new Vector3(side.x * 2.55f, 1.0f, side.z * 2.55f);
        Vector3 toward = -side;                                                 // Shard_01 쪽
        Vector3 axis = Vector3.Cross(Vector3.up, toward).normalized;
        s2.localRotation = Quaternion.AngleAxis(34f, axis) * Quaternion.Euler(8f, 40f, -6f);

        // 가장 낮은 꼭짓점을 지면 아래로 크기의 30% 묻는다
        var t = Terrain.activeTerrain;
        var mf = s2.GetComponent<MeshFilter>();
        if (mf != null && t != null)
        {
            var w = mf.sharedMesh.vertices.Select(v => s2.TransformPoint(v)).ToArray();
            var low = w.OrderBy(p => p.y).First();
            float g = t.SampleHeight(low) + t.transform.position.y;
            float size = (w.Max(p => p.y) - w.Min(p => p.y));
            float target = g - 0.30f * size * 0.5f;
            s2.position += Vector3.up * (target - low.y);
            log.Append("L2 Shard_02 low→").Append(target.ToString("F2")).Append(" ground ").Append(g.ToString("F2")).Append(" | ");
        }
        EditorUtility.SetDirty(s2);

        foreach (var L in new[] { L1, L2 })
        {
            var rs = L.GetComponentsInChildren<Renderer>(false);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            log.Append(L.name).Append(" top y=").Append(b.max.y.ToString("F1")).Append(" size=").Append(b.size.ToString("F1")).Append(" | ");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
        System.IO.Directory.CreateDirectory("Assets/_preview/");
        System.IO.File.WriteAllText("Assets/_preview/landmark_trim.txt", log.ToString());

        PyriteViews.CaptureSet("camp_lake shore camp_left lm1 lm2");
    }

    static void Keep(Transform L, string[] keep, StringBuilder log)
    {
        foreach (Transform c in L)
        {
            bool on = keep.Contains(c.name);
            if (c.gameObject.activeSelf != on) { Undo.RecordObject(c.gameObject, "trim"); c.gameObject.SetActive(on); }
        }
        log.Append(L.name).Append(" keep ").Append(string.Join("+", keep)).Append(" | ");
    }
}
#endif
