// Tools ▸ Pyrite ▸ Y3. Apply Tune File
//  Logs/pyrite_tune.txt 의 key=value 를 시간대 배열·머티리얼에 바로 넣고 확인 렌더까지 한다.
//  (상수 바꿔서 재컴파일하는 왕복을 줄이는 튜닝 전용. 확정값은 PyriteNightTools 상수로 옮긴다)
//   ambSky=r,g,b        밤 하늘빛(위)            ambGround=r,g,b   밤 하늘빛(아래)
//   matcap=d,n,w        황철석 MatCap 노을/밤/새벽 emis=d,n,w      황철석 emission 배수
//   specNoTint=0|1      황철석 반사 색조 제외
//   views=camp_left shoreline ...   presets=1 0 2
#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

public static class PyriteTune
{
    const string FILE = "Logs/pyrite_tune.txt";
    static readonly string[] PYRITE = { "M_Pyrite", "M_Pyrite_Cliff", "M_Pyrite_Tarnish", "M_Pyrite_Iris" };

    [MenuItem("Tools/Pyrite/Y3. Apply Tune File", false, 292)]
    public static void Run()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var path = Path.Combine(root, FILE);
        if (!File.Exists(path)) { Debug.LogError("[Y3] " + FILE + " 없음"); return; }
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null) { Debug.LogError("[Y3] ToD 없음"); return; }
        string views = null; int[] presets = { 1 };
        var log = new System.Text.StringBuilder("[Y3] ");

        foreach (var raw in File.ReadAllLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#") || !line.Contains("=")) continue;
            var k = line.Substring(0, line.IndexOf('=')).Trim();
            var v = line.Substring(line.IndexOf('=') + 1).Trim();
            float[] f = v.Split(new[] { ',', ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => { float r; return float.TryParse(x, NumberStyles.Float, CultureInfo.InvariantCulture, out r) ? r : float.NaN; }).ToArray();
            switch (k)
            {
                case "ambSky":    tod.nightAmbSky    = Ensure(tod.nightAmbSky);    tod.nightAmbSky[1]    = new Color(f[0], f[1], f[2]); break;
                case "ambGround": tod.nightAmbGround = Ensure(tod.nightAmbGround); tod.nightAmbGround[1] = new Color(f[0], f[1], f[2]); break;
                case "matcap":    tod.crystalMatcap = new[] { f[0], f[1], f[2] }; break;
                case "matcapTint":
                    tod.crystalMatcapTint = (tod.crystalMatcapTint != null && tod.crystalMatcapTint.Length >= 3) ? tod.crystalMatcapTint : new[] { Color.white, Color.white, Color.white };
                    tod.crystalMatcapTint[1] = new Color(f[0], f[1], f[2]); break;
                case "emis":      tod.crystalEmissionMul = new[] { f[0], f[1], f[2] }; break;
                case "specNoTint":
                    foreach (var n in PYRITE)
                    {
                        var m = AssetDatabase.FindAssets(n + " t:Material").Select(AssetDatabase.GUIDToAssetPath)
                                  .Select(AssetDatabase.LoadAssetAtPath<Material>).FirstOrDefault(x => x != null && x.name == n);
                        if (m != null) { m.SetFloat("_SpecNoTint", f[0]); EditorUtility.SetDirty(m); }
                    }
                    break;
                case "views":   views = v; break;
                case "presets": presets = f.Select(x => (int)x).ToArray(); break;
            }
            log.Append(k).Append('=').Append(v).Append(" | ");
        }
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        AssetDatabase.SaveAssets();
        Debug.Log(log.ToString());
        PyriteViews.CaptureSet(views, presets);
    }

    static Color[] Ensure(Color[] a) => (a != null && a.Length >= 3) ? a : new[] { Color.black, Color.black, Color.black };
}
#endif
