// 메시 권취(winding) / 법선 정합성 감사 — 에디터 전용
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteMeshAudit
{
    [MenuItem("Tools/Pyrite/Z. Audit Mesh Winding")]
    public static void Audit()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== MESH WINDING AUDIT ===");
        sb.AppendLine("판정 기준:");
        sb.AppendLine(" * geoN = Cross(v1-v0, v2-v0)  ← Unity가 앞면 판정(백페이스 컬링)에 쓰는 기하 법선");
        sb.AppendLine(" * shadeN = 정점 법선 평균     ← 셰이딩에 쓰이는 법선 (임포트된 vn)");
        sb.AppendLine(" * dot(geoN, shadeN) < 0 이면 '보이는 면'과 '밝게 칠해지는 면'이 반대 → 안팎 뒤집힘");
        sb.AppendLine();

        var seen = new HashSet<Mesh>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            var m = mf.sharedMesh;
            if (m == null || !seen.Add(m)) continue;
            var tf = mf.transform;

            var v = m.vertices; var n = m.normals; var t = m.triangles;
            if (n == null || n.Length != v.Length) { sb.AppendLine(mf.name + " (" + m.name + "): 법선 없음, 건너뜀"); continue; }

            int tri = t.Length / 3;
            int flipped = 0, agree = 0, degenerate = 0;
            // 방사형(원통) 메시용 추가 검사: 기하법선이 바깥(원점 기준 방사)을 향하는가
            int geoOutward = 0, shadeOutward = 0, radialTested = 0;
            Vector3 centre = Vector3.zero;

            for (int i = 0; i < tri; i++)
            {
                var a = v[t[i * 3]]; var b = v[t[i * 3 + 1]]; var c = v[t[i * 3 + 2]];
                var geo = Vector3.Cross(b - a, c - a);
                if (geo.sqrMagnitude < 1e-12f) { degenerate++; continue; }
                geo.Normalize();
                var sh = (n[t[i * 3]] + n[t[i * 3 + 1]] + n[t[i * 3 + 2]]).normalized;
                if (Vector3.Dot(geo, sh) < 0f) flipped++; else agree++;

                var fc = (a + b + c) / 3f;
                var radial = new Vector3(fc.x - centre.x, 0f, fc.z - centre.z);
                if (radial.sqrMagnitude > 4f && Mathf.Abs(geo.y) < 0.6f)
                {
                    radialTested++;
                    radial.Normalize();
                    if (Vector3.Dot(geo, radial) > 0f) geoOutward++;
                    if (Vector3.Dot(sh, radial) > 0f) shadeOutward++;
                }
            }
            sb.AppendFormat("{0}  (mesh {1}, tris {2})\n", mf.gameObject.name, m.name, tri);
            sb.AppendFormat("   권취 vs 법선: 일치 {0} / 뒤집힘 {1} / 퇴화 {2}   → {3}\n",
                agree, flipped, degenerate,
                flipped == 0 ? "정상" : (agree == 0 ? "★ 전부 뒤집힘 ★" : "혼재(문제)"));
            if (radialTested > 0)
                sb.AppendFormat("   방사검사({0}면): 기하법선이 바깥 {1} ({2:P0}) / 셰이딩법선이 바깥 {3} ({4:P0})\n",
                    radialTested, geoOutward, (float)geoOutward / radialTested,
                    shadeOutward, (float)shadeOutward / radialTested);
            sb.AppendLine();
        }

        var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "mesh_audit.txt");
        File.WriteAllText(path, sb.ToString());
        Debug.Log("[Pyrite] 메시 감사 저장: " + path + "\n" + sb.ToString());
    }
}
#endif
