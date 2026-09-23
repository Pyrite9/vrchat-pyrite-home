// Tools ▸ Pyrite ▸ Z20a. Firefly Audit
//  인게임 21:00 에서 반딧불 빛이 안 보인다(ACES 적용 후). 반딧불 파티클 상태를 전부 기록 — 씬은 안 바꾼다.
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteFireflyAudit
{
    [MenuItem("Tools/Pyrite/Z20a. Firefly Audit", false, 48)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z20a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        sb.AppendLine("cycle fireflies: " + (cyc == null ? "-" : cyc.fireflies.Length.ToString()) + "  ffRateMul " + (cyc == null ? "" : string.Join(" ", cyc.ffRateMul.Select(v => v.ToString("0.##")))) + "  ffColorA[0] " + (cyc == null ? "" : cyc.ffColorA[0].ToString()));
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(true).OrderBy(p => p.name))
        {
            var r = ps.GetComponent<ParticleSystemRenderer>();
            var m = ps.main; var em = ps.emission; var sh = ps.shape;
            var mat = r != null ? r.sharedMaterial : null;
            string col = "";
            if (mat != null)
                foreach (var pn in new[] { "_TintColor", "_Color", "_EmissionColor", "_BaseColor" })
                    if (mat.HasProperty(pn)) col += " " + pn + "=" + mat.GetColor(pn);
            sb.AppendLine(string.Format("{0} (parent {1}) active {2} rEnabled {3} play {4} rate {5:0.##} max {6} life {7:0.##} size {8:0.###}-{9:0.###} startCol {10} shape {11} {12} pos {13} mat {14} shader {15}{16} layer {17}",
                ps.name, ps.transform.parent ? ps.transform.parent.name : "-", ps.gameObject.activeInHierarchy, r != null && r.enabled, m.playOnAwake,
                em.rateOverTime.constant, m.maxParticles, m.startLifetime.constantMax, m.startSize.constantMin, m.startSize.constantMax, m.startColor.colorMax,
                sh.shapeType, sh.scale, ps.transform.position, mat ? AssetDatabase.GetAssetPath(mat) : "null", mat ? mat.shader.name : "-", col, ps.gameObject.layer));
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_firefly.txt", sb.ToString());
    }
}
#endif
