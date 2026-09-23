// Tools ▸ Pyrite ▸ Z12. Lake Calm (less ripple sparkle)  /  Z12b. Revert
//  인게임: 반사 형태는 좋은데 수면 전체에 흰 긁힘 같은 잔반짝임이 미관을 해쳤다.
//   원인 1 — 거울 오버레이: 잔물결 노멀이 반사 좌표를 _Distort 0.06 만큼 흔들어 별이 짧은 선으로 찢어짐
//   원인 2 — 물 본체: 프로브 반사가 같은 잔물결(_NormalScale 0.35)로 반짝이며 오버레이 틈으로 비침
//  큰 물결(스웰)은 그대로 두고 잔물결만 줄인다.
//   M_WaterMirror  _Distort 0.06 → 0.03, _RippleDistort 1 → 0.2 (셰이더 기본값도 같게)
//   M_Water_Dusk/Night/Dawn  _NormalScale 0.35 → 0.15
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteLakeCalm
{
    const string MIRROR = "Assets/Materials/M_WaterMirror.mat";
    static readonly string[] WATER = { "Assets/Materials/M_Water_Dusk.mat", "Assets/Materials/M_Water_Night.mat", "Assets/Materials/M_Water_Dawn.mat" };

    [MenuItem("Tools/Pyrite/Z12. Lake Calm (less ripple sparkle)", false, 23)]
    public static void Run() { Apply(0.03f, 0.2f, 0.15f, "[Z12]"); }

    [MenuItem("Tools/Pyrite/Z12b. Lake Calm Revert", false, 24)]
    public static void Revert() { Apply(0.06f, 1f, 0.35f, "[Z12b]"); }

    static void Apply(float distort, float rippleShare, float waterRipple, string tag)
    {
        var sb = new StringBuilder(tag + " " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MIRROR);
        if (m != null)
        {
            sb.AppendLine(string.Format("  M_WaterMirror _Distort {0} -> {1}, _RippleDistort {2} -> {3}",
                m.GetFloat("_Distort"), distort, m.HasProperty("_RippleDistort") ? m.GetFloat("_RippleDistort") : -1f, rippleShare));
            m.SetFloat("_Distort", distort);
            m.SetFloat("_RippleDistort", rippleShare);
            EditorUtility.SetDirty(m);
        }
        else sb.AppendLine("  M_WaterMirror 없음");
        foreach (var p in WATER)
        {
            var w = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (w == null) { sb.AppendLine("  없음 " + p); continue; }
            sb.AppendLine(string.Format("  {0} _NormalScale {1} -> {2}", w.name, w.GetFloat("_NormalScale"), waterRipple));
            w.SetFloat("_NormalScale", waterRipple);
            EditorUtility.SetDirty(w);
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine("RESULT: DONE");
        File.WriteAllText("Logs/pyrite_lakecalm.txt", sb.ToString());
    }
}
#endif
