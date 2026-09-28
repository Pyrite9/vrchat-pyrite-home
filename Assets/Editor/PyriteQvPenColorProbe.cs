// PyriteQvPenColorProbe.cs — QvPen 펜 색이 어디 있는지 (Z51h, 읽기 전용). 캠프 QvPen 의 펜 15개: 트레일 그라디언트 키 · 렌더러 머티리얼 색 · 프로퍼티 블록
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteQvPenColorProbe
{
    [MenuItem("Tools/Pyrite3/Z51h. QvPen Color Probe", false, 5108)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z51h] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var qv = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "QvPen");
            var pens = qv.transform.Find("Pens");
            foreach (Transform pm in pens)
            {
                var line = new StringBuilder(pm.name + ":");
                var tr = pm.GetComponentInChildren<TrailRenderer>(true);
                if (tr) line.Append(" trail keys " + string.Join("/", tr.colorGradient.colorKeys.Select(k => ColorUtility.ToHtmlStringRGB(k.color))) + " mat " + (tr.sharedMaterial ? tr.sharedMaterial.name : "-"));
                foreach (var r in pm.GetComponentsInChildren<Renderer>(true).Where(r => !(r is TrailRenderer)).Take(6))
                {
                    var m = r.sharedMaterial;
                    string col = m && m.HasProperty("_Color") ? ColorUtility.ToHtmlStringRGB(m.color) : "-";
                    var mpb = new MaterialPropertyBlock(); r.GetPropertyBlock(mpb);
                    line.Append(" | " + r.name + " " + (m ? m.name : "-") + " " + col + (mpb.isEmpty ? "" : " mpb"));
                }
                sb.AppendLine(line.ToString());
            }
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        sb.AppendLine("RESULT: DONE");
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
