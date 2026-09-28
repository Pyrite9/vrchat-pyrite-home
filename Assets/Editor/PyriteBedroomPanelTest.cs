// PyriteBedroomPanelTest.cs — 침실 팝업 ClientSim 동작 시험 (Z50c). Play 진입 → 단계별로 UdonBehaviour 이벤트 호출 → 결과 Logs/pyrite_bedroom_panel_test.txt → Play 종료
//  ⚠ Preview 창(Z49v)은 닫고 실행 (열려 있으면 0.3 FPS)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteBedroomPanelTest
{
    const string KEY = "pyrite_bp_test";
    const string LOG = "Logs/pyrite_bedroom_panel_test.txt";
    static int stage { get => SessionState.GetInt(KEY + "_st", 0); set => SessionState.SetInt(KEY + "_st", value); }
    static int fails { get => SessionState.GetInt(KEY + "_f", 0); set => SessionState.SetInt(KEY + "_f", value); }
    static int lastFrame = -1;
    static float t0;
    static string held;

    static PyriteBedroomPanelTest()
    {
        if (SessionState.GetBool(KEY, false)) EditorApplication.update += Tick;
    }

    [MenuItem("Tools/Pyrite3/Z50c. Bedroom Panel Play Test", false, 5003)]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        SessionState.SetBool(KEY, true);
        SessionState.SetString(KEY + "_log", "[Z50c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
        stage = 0;
        EditorApplication.isPlaying = true;
    }

    static void L(string s) { SessionState.SetString(KEY + "_log", SessionState.GetString(KEY + "_log", "") + s + "\n"); }
    static void Check(string what, bool ok, string detail) { if (!ok) fails++; L((ok ? "  OK   " : "  FAIL ") + what + " — " + detail); }

    static Transform P() { var r = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom"); return r ? r.transform.Find("BedroomPanel") : null; }
    static UdonBehaviour UB() { var p = P(); return p ? p.GetComponent<UdonBehaviour>() : null; }
    static T C<T>(string path) where T : Component { var p = P(); var t = p ? p.Find(path) : null; return t ? t.GetComponent<T>() : null; }
    static string Txt(string path) { var t = C<TextMeshProUGUI>(path); return t ? t.text : "?"; }
    static string LightsStr() { var r = P().parent.Find("Lights"); return r ? string.Join(", ", r.GetComponentsInChildren<Light>(true).Select(l => l.intensity.ToString("F3"))) : "?"; }
    static float Dim() { var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat"); return m ? m.GetFloat("_Dim") : -1f; }

    static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (stage >= 99 || (stage > 0 && !EditorApplication.isPlayingOrWillChangePlaymode)) Finish();
            return;
        }
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        if (stage == 0) { stage = 1; t0 = Time.realtimeSinceStartup; fails = 0; return; }
        float t = Time.realtimeSinceStartup - t0;
        var ub = UB();
        try
        {
            switch (stage)
            {
                case 1:
                    if (t < 4f) return;
                    if (ub == null) { L("!! BedroomPanel UdonBehaviour 없음"); stage = 98; return; }
                    var pc = P().Find("PanelCanvas").gameObject;
                    Check("시작 상태", !pc.activeSelf && !P().Find("Mirror").gameObject.activeSelf, "panel " + pc.activeSelf + ", 시계 '" + Txt("PanelCanvas/Clock") + "', 광원 " + LightsStr() + ", _Dim " + Dim().ToString("F2"));
                    ub.SendCustomEvent("OnIcon");
                    Check("아이콘 → 패널 열림", pc.activeSelf, "panel " + pc.activeSelf);
                    stage = 2; return;
                case 2:
                    var sl = C<Slider>("PanelCanvas/SleepSlider");
                    sl.value = 1f;
                    Check("수면 100%", Dim() < 0.41f && Txt("PanelCanvas/SleepValue") == "100%", "광원 " + LightsStr() + ", _Dim " + Dim().ToString("F2") + ", 표시 " + Txt("PanelCanvas/SleepValue"));
                    sl.value = 0.5f;
                    L("  수면 50%: 광원 " + LightsStr() + ", _Dim " + Dim().ToString("F2") + ", 표시 " + Txt("PanelCanvas/SleepValue"));
                    sl.value = 0f;
                    Check("수면 0% 복귀", Mathf.Abs(Dim() - 1f) < 0.01f, "광원 " + LightsStr() + ", _Dim " + Dim().ToString("F2"));
                    var mt = C<Toggle>("PanelCanvas/MirrorToggle");
                    mt.isOn = true;
                    Check("거울 켬", P().Find("Mirror").gameObject.activeSelf, "active " + P().Find("Mirror").gameObject.activeSelf);
                    mt.isOn = false;
                    Check("거울 끔", !P().Find("Mirror").gameObject.activeSelf, "active " + P().Find("Mirror").gameObject.activeSelf);
                    L("  알람 시작 " + Txt("PanelCanvas/AmPm/Value") + " " + Txt("PanelCanvas/Hour/Value") + ":" + Txt("PanelCanvas/Min/Value"));
                    ub.SendCustomEvent("AmPm");
                    Check("AM/PM 전환", Txt("PanelCanvas/AmPm/Value") == "PM", Txt("PanelCanvas/AmPm/Value"));
                    ub.SendCustomEvent("AmPm");
                    ub.SendCustomEvent("HourUp");
                    Check("시 ▲ 한 번", Txt("PanelCanvas/Hour/Value") == "08", Txt("PanelCanvas/Hour/Value"));
                    ub.SendCustomEvent("MinDown");
                    L("  분 ▼ 누름 (계속 누르는 중) " + Txt("PanelCanvas/Min/Value"));
                    t0 = Time.realtimeSinceStartup; stage = 3; return;
                case 3:
                    if (t < 2.5f) return;
                    ub.SendCustomEvent("Release");
                    string m1 = Txt("PanelCanvas/Min/Value"); held = m1;
                    int steps = (60 - int.Parse(m1)) % 60;
                    Check("분 ▼ 2.5 s 꾹 누름 → 연속", steps >= 15, "00 → " + m1 + " (" + steps + " 칸, 기대 약 30)");
                    t0 = Time.realtimeSinceStartup; stage = 4; return;
                case 4:
                    if (t < 1f) return;
                    string m2 = Txt("PanelCanvas/Min/Value");
                    Check("놓은 뒤 멈춤", m2 == held, held + " → 1 s 뒤 " + m2);
                    ub.SendCustomEvent("DebugArmNow");
                    t0 = Time.realtimeSinceStartup; stage = 5; return;
                case 5:
                    if (t < 1.5f) return;
                    var au = P().GetComponent<AudioSource>();
                    var stop = P().Find("PanelCanvas/Stop").gameObject;
                    Check("알람 울림", au.isPlaying && stop.activeSelf, "playing " + au.isPlaying + ", STOP " + stop.activeSelf + ", 설정 " + Txt("PanelCanvas/AmPm/Value") + " " + Txt("PanelCanvas/Hour/Value") + ":" + Txt("PanelCanvas/Min/Value") + ", 시계 " + Txt("PanelCanvas/Clock") + ", 아이콘 α " + C<Image>("IconCanvas/Moon").color.a.ToString("F2"));
                    ub.SendCustomEvent("OnIcon");
                    Check("아이콘 → 알람 끔 (패널은 그대로)", !au.isPlaying && !stop.activeSelf && P().Find("PanelCanvas").gameObject.activeSelf, "playing " + au.isPlaying + ", STOP " + stop.activeSelf);
                    t0 = Time.realtimeSinceStartup; stage = 6; return;
                case 6:
                    if (t < 1.5f) return;
                    Check("같은 분 다시 안 울림", !P().GetComponent<AudioSource>().isPlaying, "playing " + P().GetComponent<AudioSource>().isPlaying);
                    ub.SendCustomEvent("OnClose");
                    Check("닫기", !P().Find("PanelCanvas").gameObject.activeSelf, "");
                    stage = 98; return;
                case 98:
                    L(fails == 0 ? "RESULT: PASS" : "RESULT: FAIL " + fails);
                    stage = 99;
                    EditorApplication.isPlaying = false;
                    return;
            }
        }
        catch (System.Exception e) { L("EXCEPTION stage " + stage + " " + e); stage = 98; }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(KEY, false);
        string s = SessionState.GetString(KEY + "_log", "");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, s);
        EditorGUIUtility.systemCopyBuffer = s;
        stage = 0;
    }
}
#endif
