// Tools ▸ Pyrite ▸ Z15. Lake Glass (calmer base)  /  Z15b. Revert
//  레퍼런스(관리자 스크린샷): 파장 긴 완만한 물결만, 잔물결은 거의 없음 → 반사가 크게 휠 뿐 부서지지 않는다.
//  그 위에 사람이 만든 파문(Z16)이 잘 보이려면 기본 수면이 먼저 조용해야 한다.
//   물 3종 + M_WaterMirror  _WaveNormal 1.8 → 1.0 (큰 물결 기울기 과장)
//   물 3종                  _NormalScale 0.15 → 0.06 (잔물결)
//   M_WaterMirror           잔물결 몫은 _RippleDistort 0.2 그대로 (NormalScale 는 물과 같이 0.06)
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteLakeGlass
{
    static readonly string[] MATS = { "Assets/Materials/M_Water_Dusk.mat", "Assets/Materials/M_Water_Night.mat", "Assets/Materials/M_Water_Dawn.mat", "Assets/Materials/M_WaterMirror.mat" };

    [MenuItem("Tools/Pyrite/Z15. Lake Glass (calmer base)", false, 30)]
    public static void Run() { Apply(1.0f, 0.06f, "[Z15]"); }

    [MenuItem("Tools/Pyrite/Z15b. Lake Glass Revert", false, 31)]
    public static void Revert() { Apply(1.8f, 0.15f, "[Z15b]"); }

    static void Apply(float swell, float ripple, string tag)
    {
        var sb = new StringBuilder(tag + " " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var p in MATS)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { sb.AppendLine("  없음 " + p); continue; }
            sb.AppendLine(string.Format("  {0} _WaveNormal {1} -> {2}, _NormalScale {3} -> {4}", m.name, m.GetFloat("_WaveNormal"), swell, m.GetFloat("_NormalScale"), ripple));
            m.SetFloat("_WaveNormal", swell); m.SetFloat("_NormalScale", ripple);
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        sb.AppendLine("RESULT: DONE");
        File.WriteAllText("Logs/pyrite_lakeglass.txt", sb.ToString());
    }
}
#endif
