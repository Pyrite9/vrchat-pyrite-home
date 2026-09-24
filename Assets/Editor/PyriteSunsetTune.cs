// Tools ▸ Pyrite2 ▸ Z37a. Sunset Sky Tune / Z37b. Sunset Sky Revert
//  노을 2단계 (2026-09-24): 노을(18.33h)·해넘이(19.0h) 키의 하늘 번짐(Mie)·지평선 연무·구름을 낮춰 우윳빛을 걷어낸다
//  DayCycle 의 키 배열 값만 바꾼다. 원래 값은 Logs/pyrite_sunset_revert.txt 에 한 번만 저장 → Z37b 가 복구
#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PyriteSunsetTune
{
    const string REVERT = "Logs/pyrite_sunset_revert.txt";
    //                       hour    Mie   haze  cloud
    static readonly float[][] TUNE =
    {
        new[] { 18.333f, 1.4f, 0.9f, 0.25f },   // 노을   (was 2.40 / 1.50 / 0.40)
        new[] { 19.0f,   1.3f, 0.8f, 0.30f },   // 해넘이 (was 2.20 / 1.30 / 0.45)
    };

    [MenuItem("Tools/Pyrite2/Z37a. Sunset Sky Tune", false, 110)]
    public static void Tune() { Apply(false); }

    [MenuItem("Tools/Pyrite2/Z37b. Sunset Sky Revert", false, 111)]
    public static void Revert() { Apply(true); }

    static void Apply(bool revert)
    {
        var sb = new StringBuilder((revert ? "[Z37b] " : "[Z37a] ") + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc == null) { sb.AppendLine("DayCycle 없음"); Flush(sb); return; }
        var inv = CultureInfo.InvariantCulture;
        if (!revert && !File.Exists(REVERT))
        {
            var r = new StringBuilder();
            foreach (var t in TUNE) { int i = Key(cyc, t[0]); if (i >= 0) r.AppendLine(string.Join(" ", new[] { cyc.keyHour[i], cyc.skyMie[i], cyc.skyHaze[i], cyc.skyCloud[i] }.Select(v => v.ToString("R", inv)))); }
            File.WriteAllText(REVERT, r.ToString());
            sb.AppendLine("원래 값 저장 → " + REVERT);
        }
        float[][] src = TUNE;
        if (revert)
        {
            if (!File.Exists(REVERT)) { sb.AppendLine("되돌릴 값 없음"); Flush(sb); return; }
            src = File.ReadAllLines(REVERT).Where(l => l.Trim().Length > 0).Select(l => l.Split(' ').Select(x => float.Parse(x, inv)).ToArray()).ToArray();
        }
        foreach (var t in src)
        {
            int i = Key(cyc, t[0]);
            if (i < 0) { sb.AppendLine("키 없음 " + t[0]); continue; }
            sb.AppendLine(string.Format("  key {0:0.00}h: Mie {1:0.00}→{2:0.00}  haze {3:0.00}→{4:0.00}  cloud {5:0.00}→{6:0.00}", cyc.keyHour[i], cyc.skyMie[i], t[1], cyc.skyHaze[i], t[2], cyc.skyCloud[i], t[3]));
            cyc.skyMie[i] = t[1]; cyc.skyHaze[i] = t[2]; cyc.skyCloud[i] = t[3];
        }
        UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        EditorSceneManager.MarkSceneDirty(cyc.gameObject.scene); EditorSceneManager.SaveOpenScenes();
        if (revert) File.Delete(REVERT);
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z37c. Sky Material Dump", false, 112)]
    public static void Dump()
    {
        var sb = new StringBuilder("[Z37c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = RenderSettings.skybox; var sh = m.shader;
        sb.AppendLine("sky " + m.name + " shader " + sh.name);
        for (int i = 0; i < sh.GetPropertyCount(); i++)
        {
            var n = sh.GetPropertyName(i); var t = sh.GetPropertyType(i); string v;
            switch (t)
            {
                case UnityEngine.Rendering.ShaderPropertyType.Color: v = m.GetColor(n).ToString("F3"); break;
                case UnityEngine.Rendering.ShaderPropertyType.Vector: v = m.GetVector(n).ToString("F3"); break;
                case UnityEngine.Rendering.ShaderPropertyType.Float: case UnityEngine.Rendering.ShaderPropertyType.Range: v = m.GetFloat(n).ToString("0.###"); break;
                default: v = m.GetTexture(n) ? m.GetTexture(n).name : "null"; break;
            }
            sb.AppendLine("  " + n + " (" + t + ") = " + v + "   // " + sh.GetPropertyDescription(i));
        }
        Flush(sb);
    }

    // 하늘 셰이더 자체 ACES 켬/끔 전환 (후처리도 ACES 라 두 번 걸린다 — 실측용). 씬 저장 안 함(머티리얼 에셋만 바뀜)
    [MenuItem("Tools/Pyrite2/Z37d. Sky In-shader ACES Toggle", false, 113)]
    public static void ToggleAces()
    {
        var m = RenderSettings.skybox; float v = m.GetFloat("_InShaderTonemap");
        m.SetFloat("_InShaderTonemap", v > 0.5f ? 0f : 1f); EditorUtility.SetDirty(m); AssetDatabase.SaveAssets();
        Flush(new StringBuilder("[Z37d] " + System.DateTime.Now.ToString("HH:mm:ss") + " _InShaderTonemap " + v + " → " + m.GetFloat("_InShaderTonemap") + "\n"));
    }

    // 노을 4단계: 노을 후처리(PP_PyriteDusk — 모든 시간대의 바탕, 낮·밤 볼륨이 위에 덮는다) 색온도·채도·게인
    const string DUSK_PP = "Assets/TerrainAssets/PP_PyriteDusk.asset";
    const string PP_REVERT = "Logs/pyrite_duskpp_revert.txt";
    [MenuItem("Tools/Pyrite2/Z37e. Dusk Grade Tune", false, 114)]
    public static void GradeTune() { Grade(false); }
    [MenuItem("Tools/Pyrite2/Z37f. Dusk Grade Revert", false, 115)]
    public static void GradeRevert() { Grade(true); }

    static void Grade(bool revert)
    {
        var sb = new StringBuilder((revert ? "[Z37f] " : "[Z37e] ") + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var p = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.PostProcessing.PostProcessProfile>(DUSK_PP);
        UnityEngine.Rendering.PostProcessing.ColorGrading cg;
        if (p == null || !p.TryGetSettings(out cg)) { sb.AppendLine("PP_PyriteDusk ColorGrading 없음"); Flush(sb); return; }
        var inv = CultureInfo.InvariantCulture;
        float temp = 20f, sat = 15f; Vector4 gain = new Vector4(1.08f, 1.00f, 0.90f, 0.02f);
        if (revert)
        {
            if (!File.Exists(PP_REVERT)) { sb.AppendLine("되돌릴 값 없음"); Flush(sb); return; }
            var v = File.ReadAllText(PP_REVERT).Split(' ').Select(x => float.Parse(x, inv)).ToArray();
            temp = v[0]; sat = v[1]; gain = new Vector4(v[2], v[3], v[4], v[5]);
        }
        else if (!File.Exists(PP_REVERT))
        {
            var g0 = cg.gain.value;
            File.WriteAllText(PP_REVERT, string.Join(" ", new[] { cg.temperature.value, cg.saturation.value, g0.x, g0.y, g0.z, g0.w }.Select(x => x.ToString("R", inv))));
        }
        sb.AppendLine(string.Format("  temperature {0} → {1}, saturation {2} → {3}, gain {4} → {5}", cg.temperature.value, temp, cg.saturation.value, sat, cg.gain.value.ToString("F2"), gain.ToString("F2")));
        cg.temperature.Override(temp); cg.saturation.Override(sat); cg.gain.Override(gain);
        EditorUtility.SetDirty(cg); EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        if (revert) File.Delete(PP_REVERT);
        sb.AppendLine("RESULT: DONE"); Flush(sb);
    }

    static int Key(PyriteDayCycle c, float h)
    {
        for (int i = 0; i < c.keyHour.Length; i++) if (Mathf.Abs(c.keyHour[i] - h) < 0.02f) return i;
        return -1;
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_sunset.txt", sb.ToString()); }
}
#endif
