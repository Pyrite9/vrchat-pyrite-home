// 면 방향(안팎) 판정 — 에디터 전용
//
// 닫힌 메시의 "부호 있는 부피" Σ dot(v0, cross(v1,v2))/6 은
// 면이 한쪽으로 일관되게 뒤집히면 부호가 통째로 반대가 된다.
// 핸디드니스를 추측하지 않으려고 Unity 기본 Cube를 같은 식으로 재서 기준으로 삼는다.
// 기준과 부호가 같으면 정상(바깥), 다르면 뒤집힘.
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteVolumeAudit
{
    static double SignedVolume(Mesh m, Transform t)
    {
        var v = m.vertices; var tri = m.triangles;
        double s = 0;
        for (int i = 0; i < tri.Length; i += 3)
        {
            Vector3 a = t != null ? t.TransformPoint(v[tri[i]])     : v[tri[i]];
            Vector3 b = t != null ? t.TransformPoint(v[tri[i + 1]]) : v[tri[i + 1]];
            Vector3 c = t != null ? t.TransformPoint(v[tri[i + 2]]) : v[tri[i + 2]];
            s += Vector3.Dot(a, Vector3.Cross(b, c)) / 6.0;
        }
        return s;
    }

    [MenuItem("Tools/Pyrite/Z2. Audit Face Direction &#2")]
    public static void Audit()
    {
        // ── 기준: Unity 기본 Cube (면이 바깥을 보는 게 확실한 메시) ──
        var refGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        double refV = SignedVolume(refGo.GetComponent<MeshFilter>().sharedMesh, null);
        Object.DestroyImmediate(refGo);
        int refSign = refV > 0 ? 1 : -1;
        Debug.Log(string.Format("[Pyrite] 기준 Unity Cube 부호부피 {0:0.0000}  (1x1x1 이므로 크기 1.0) → 정상 부호 = {1}",
                                refV, refSign > 0 ? "+" : "-"));

        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name != "Dock" && root.name != "PyriteCliffs_Visual"
                && !root.name.StartsWith("Crystals")) continue;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                double sv = SignedVolume(mf.sharedMesh, null);          // 로컬 = 임포트된 그대로
                int sign = sv > 0 ? 1 : -1;
                bool ok = sign == refSign;
                Debug.Log(string.Format("[Pyrite] {0}/{1}  삼각형 {2}  부호부피 {3:0.000}  → {4}",
                    root.name, mf.name, mf.sharedMesh.triangles.Length / 3, sv,
                    ok ? "정상(바깥)" : "🔴 뒤집힘(안쪽)"));
            }
        }
    }
}
#endif
