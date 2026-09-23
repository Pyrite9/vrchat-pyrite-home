// Tools ▸ Pyrite ▸ M0. Occlusion Audit  /  M5. Occlusion Measure
//  B6 전 실측. M(플래그 정리)은 v3 때 만든 것이라 그 뒤 추가된 것(계단·호수 반사·뿌리 랜턴·스위치·하늘 원반)을 모른다.
//   M0 — 루트별 렌더러 수, 정적 플래그(Occluder/Occludee/없음), 비활성, 투명, 크기 상위. M 의 건너뛰기 목록이 실제로 있는지
//   M5 — 고정 시점마다 cam.useOcclusionCulling 끔/켬으로 렌더해 삼각형·배치·SetPass 비교 (UnityStats). 오클루전 데이터 유무 기록
//  씬은 바꾸지 않는다. → Logs/pyrite_occlusion.txt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteOcclusionAudit
{
    const string LOG = "Logs/pyrite_occlusion.txt";
    static readonly string[] M_SKIP = { "Mirror", "TimePanel", "TimeDial", "LightFX", "Ambience", "Main Camera", "VRCWorld", "EventSystem" };

    [MenuItem("Tools/Pyrite/M0. Occlusion Audit", false, 261)]
    public static void Audit()
    {
        var sb = new StringBuilder("[M0] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        sb.AppendLine(string.Format("umbra data {0:0.00} MB, smallestOccluder {1}, smallestHole {2}, backface {3}",
            StaticOcclusionCulling.umbraDataSize / 1048576f, StaticOcclusionCulling.smallestOccluder, StaticOcclusionCulling.smallestHole, StaticOcclusionCulling.backfaceThreshold));
        var all = Object.FindObjectsOfType<Transform>(true).Select(t => t.name).ToHashSet();
        sb.AppendLine("M 건너뛰기 목록 존재: " + string.Join(", ", M_SKIP.Select(s => s + (all.Contains(s) ? "" : "(없음)"))));

        sb.AppendLine("루트 | 렌더러 | 비활성 | 투명 | 정적0 | Occludee | Occluder | 최대 크기");
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects().OrderBy(g => g.name))
        {
            var rs = root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToList();
            var terr = root.GetComponentsInChildren<Terrain>(true);
            if (rs.Count == 0 && terr.Length == 0) continue;
            int inactive = rs.Count(r => !r.gameObject.activeInHierarchy || !r.enabled);
            int transp = rs.Count(r => !Opaque(r));
            int none = rs.Count(r => GameObjectUtility.GetStaticEditorFlags(r.gameObject) == 0);
            int ee = rs.Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.OccludeeStatic) != 0);
            int er = rs.Count(r => (GameObjectUtility.GetStaticEditorFlags(r.gameObject) & StaticEditorFlags.OccluderStatic) != 0);
            float mx = rs.Count == 0 ? 0 : rs.Max(r => Mathf.Max(r.bounds.size.x, r.bounds.size.y, r.bounds.size.z));
            sb.AppendLine(string.Format("{0,-22} {1,4} {2,4} {3,4} {4,4} {5,4} {6,4}  {7,6:0.0}m{8}", root.name, rs.Count, inactive, transp, none, ee, er, mx,
                terr.Length > 0 ? "  (terrain flags " + GameObjectUtility.GetStaticEditorFlags(terr[0].gameObject) + ")" : ""));
        }
        // 런타임에 켜고 끄거나 움직이는데 정적 플래그가 있는 것 — 오클루전·배칭에 위험
        sb.AppendLine("주의 후보(정적인데 움직이거나 토글될 수 있는 것):");
        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            var f = GameObjectUtility.GetStaticEditorFlags(r.gameObject);
            if (f == 0) continue;
            bool udon = r.GetComponentInParent<VRC.Udon.UdonBehaviour>(true) != null;
            bool pickup = r.GetComponentInParent<Rigidbody>(true) != null;
            bool inactive = !r.gameObject.activeInHierarchy;
            if (udon || pickup || inactive) sb.AppendLine(string.Format("  {0}  flags {1}  udon {2} rigidbody {3} inactive {4}", PathOf(r.gameObject), f, udon, pickup, inactive));
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, sb.ToString());
    }

    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="camp_lake",  eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f },
        new V{ n="camp_back",  eye=new Vector3(-10.0f, 1.7f, 50.0f), look=new Vector3(-10.0f, 4.0f, 90.0f), fov=70f },
        new V{ n="shore",      eye=new Vector3( 12.0f, 1.7f, 36.0f), look=new Vector3(-13.0f, 2.0f, -60.0f), fov=70f },
        new V{ n="lm1",        eye=new Vector3(-10.0f, 3.0f, -52.0f), look=new Vector3(-13.0f, 5.0f, -73.0f), fov=55f },
        new V{ n="far_to_camp",eye=new Vector3(  5.0f, 1.7f, -40.0f), look=new Vector3( -5.0f, 6.0f, 78.0f), fov=70f },
        new V{ n="west_wall",  eye=new Vector3( 25.0f, 1.7f, 45.0f), look=new Vector3(-78.0f, 5.0f, 20.0f), fov=65f },
        new V{ n="terrace_in", eye=new Vector3(-10.0f, 2.0f, 70.0f), look=new Vector3( 30.0f, 3.0f, 65.0f), fov=70f },
        new V{ n="tent_side",  eye=new Vector3( -4.0f, 1.7f, 58.0f), look=new Vector3(-16.0f, 1.5f, 56.0f), fov=70f },
    };

    // UnityStats 는 Game 뷰가 실제로 그릴 때만 갱신된다(cam.Render() 로는 안 바뀐다 — 첫 시도에서 8시점 모두 같은 값).
    // → 카메라를 옮기고 Game 뷰를 다시 그리게 한 뒤 몇 프레임 기다렸다가 읽는 상태 기계. Game 뷰 탭이 보이고 있어야 한다.
    static int step, wait;
    static int[,] res;
    static StringBuilder msb;
    static Vector3 mp0; static Quaternion mr0; static float mf0; static bool moc0;

    [MenuItem("Tools/Pyrite/M5. Occlusion Measure", false, 266)]
    public static void Measure()
    {
        var cam = Camera.main;
        msb = new StringBuilder("[M5] " + System.DateTime.Now.ToString("HH:mm:ss") + "  (Game 뷰 통계)\n");
        msb.AppendLine(string.Format("umbra data {0:0.00} MB", StaticOcclusionCulling.umbraDataSize / 1048576f));
        mp0 = cam.transform.position; mr0 = cam.transform.rotation; mf0 = cam.fieldOfView; moc0 = cam.useOcclusionCulling;
        res = new int[Views.Length * 2, 3];
        step = 0; wait = 0;
        var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor");
        if (gv != null) EditorWindow.GetWindow(gv, false, null, true);
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        var cam = Camera.main; var t = Terrain.activeTerrain;
        int vi = step / 2, k = step % 2;
        if (wait == 0)
        {
            if (vi >= Views.Length) { Finish(); return; }
            var v = Views[vi];
            var eye = v.eye;
            if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0, eye.z)) + t.transform.position.y);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((v.look - eye).normalized, Vector3.up));
            cam.fieldOfView = v.fov; cam.farClipPlane = 1000f;
            cam.useOcclusionCulling = k == 1;
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        wait++;
        if (wait < 6) return;
        res[step, 0] = UnityStats.triangles; res[step, 1] = UnityStats.batches; res[step, 2] = UnityStats.visibleSkinnedMeshes + UnityStats.drawCalls;
        wait = 0; step++;
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        var cam = Camera.main;
        cam.useOcclusionCulling = moc0; cam.transform.SetPositionAndRotation(mp0, mr0); cam.fieldOfView = mf0;
        long so = 0, sn = 0;
        for (int i = 0; i < Views.Length; i++)
        {
            int a = res[i * 2, 0], b = res[i * 2 + 1, 0];
            so += a; sn += b;
            msb.AppendLine(string.Format("  {0,-12} tris {1,8} -> {2,8} ({3,5:0.0}%)  batches {4,4} -> {5,4}  draws {6,4} -> {7,4}",
                Views[i].n, a, b, a > 0 ? 100.0 * (a - b) / a : 0, res[i * 2, 1], res[i * 2 + 1, 1], res[i * 2, 2], res[i * 2 + 1, 2]));
        }
        msb.AppendLine(string.Format("  합계 tris {0} -> {1} ({2:0.0}% 감소)", so, sn, so > 0 ? 100.0 * (so - sn) / so : 0));
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, msb.ToString());
        Debug.Log("[M5] 완료 — Logs/pyrite_occlusion.txt");
    }

    static bool Opaque(Renderer r)
    {
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) return false;
            int q = m.renderQueue; if (q < 0) q = m.shader != null ? m.shader.renderQueue : 2000;
            if (q >= 2450) return false;
        }
        return true;
    }

    static string PathOf(GameObject g) { var s = g.name; for (var t = g.transform.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
}
#endif
