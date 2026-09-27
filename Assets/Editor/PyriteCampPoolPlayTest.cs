// Tools ▸ Pyrite3 ▸ Z43e. Camp Pool Play Test (ClientSim)
//  Play 에 들어가서 설정 패널 이벤트를 차례로 보내고(의자 +8, 돗자리 +6, 의자 −3, 돗자리 −2, 제자리, 펜·지우개 소환) 상태를 기록한 뒤 Play 를 끝낸다
//  결과 Logs/pyrite_pooltest.txt. 🔴 Console 의 Error Pause 가 켜져 있으면 ProTV 예외(약 30 s)에 멈출 수 있다 — 테스트는 12 s 안에 끝난다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteCampPoolPlayTest
{
    const string KEY = "pyrite_pooltest", LOG = "Logs/pyrite_pooltest.txt";
    static double t0 = -1; static int step = 0;

    static PyriteCampPoolPlayTest() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Pyrite3/Z43e. Camp Pool Play Test (ClientSim)", false, 24)]
    public static void Run()
    {
        File.WriteAllText(LOG, "[Z43e] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        SessionState.SetBool(KEY, true);
        EditorApplication.isPlaying = true;
    }

    static void W(string s) { File.AppendAllText(LOG, s + "\n"); }

    static UdonBehaviour Ub(string go, string prog)
    {
        var g = GameObject.Find(go); if (g == null) return null;
        return g.GetComponents<UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == prog);
    }

    static void State(string tag)
    {
        var pool = Ub("CampPool", "PyriteCampPool");
        if (pool == null) { W(tag + " pool 없음"); return; }
        var chairs = (GameObject[])pool.GetProgramVariable("chairs"); var homes = (Transform[])pool.GetProgramVariable("chairHomes");
        var mats = (GameObject[])pool.GetProgramVariable("mats"); var mhomes = (Transform[])pool.GetProgramVariable("matHomes");
        string Line(GameObject[] a, Transform[] h) => string.Join(" ", a.Select((g, i) => g.activeSelf ? (Vector3.Distance(g.transform.position, h[i].position) < 0.02f ? "H" : "o") : "."));
        W(string.Format("{0}: chairMask {1} [{2}] | matMask {3} [{4}]", tag, pool.GetProgramVariable("chairMask"), Line(chairs, homes), pool.GetProgramVariable("matMask"), Line(mats, mhomes)));
        var st = Ub("SettingsProjector", "PyriteSettings");
        if (st != null)
        {
            var ct = st.GetProgramVariable("chairCountText") as TMPro.TextMeshProUGUI; var mt = st.GetProgramVariable("matCountText") as TMPro.TextMeshProUGUI;
            W("   panel texts: chairs '" + (ct ? ct.text : "null") + "' mats '" + (mt ? mt.text : "null") + "'");
        }
    }

    static void Send(string ev, int n = 1)
    {
        var st = Ub("SettingsProjector", "PyriteSettings");
        if (st == null) { W("settings 없음"); return; }
        for (int i = 0; i < n; i++) st.SendCustomEvent(ev);
        W("-> " + ev + " x" + n);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(KEY, false)) return;
        if (!EditorApplication.isPlaying) { if (step > 0) { SessionState.SetBool(KEY, false); step = 0; t0 = -1; } return; }
        if (t0 < 0) { t0 = EditorApplication.timeSinceStartup; step = 1; return; }
        double t = EditorApplication.timeSinceStartup - t0;
        try
        {
            if (step == 1 && t > 6) { State("start"); Send("NextPage"); Send("ChairPlus", 8); step++; }
            else if (step == 2 && t > 7) { State("chair+8"); Send("MatPlus", 6); step++; }
            else if (step == 3 && t > 8) { State("mat+6"); Send("ChairMinus", 3); Send("MatMinus", 2); step++; }
            else if (step == 4 && t > 9)
            {
                State("chair-3 mat-2");
                var pool = Ub("CampPool", "PyriteCampPool"); var chairs = (GameObject[])pool.GetProgramVariable("chairs");
                chairs[0].transform.position += new Vector3(3f, 0, 3f); chairs[4].transform.position += new Vector3(-2f, 0, 0);
                State("moved 0,4"); Send("PropsBack"); step++;
            }
            else if (step == 5 && t > 10)
            {
                State("back");
                var pool = Ub("CampPool", "PyriteCampPool"); var pens = (GameObject[])pool.GetProgramVariable("pens"); var ers = (GameObject[])pool.GetProgramVariable("erasers");
                var p0 = pens.Select(p => p.transform.position).ToArray();
                Send("SummonPen"); Send("SummonPen"); Send("SummonEraser");
                var lp = VRC.SDKBase.Networking.LocalPlayer;
                var head = lp != null ? lp.GetTrackingData(VRC.SDKBase.VRCPlayerApi.TrackingDataType.Head).position : Vector3.zero;
                for (int i = 0; i < pens.Length; i++) if (Vector3.Distance(p0[i], pens[i].transform.position) > 0.01f) W(string.Format("   pen {0} moved → {1} (head {2}, dist {3:F2})", i, pens[i].transform.position.ToString("F2"), head.ToString("F2"), Vector3.Distance(head, pens[i].transform.position)));
                W(string.Format("   eraser0 {0}", ers.Length > 0 ? ers[0].transform.position.ToString("F2") : "-"));
                Send("PensBack"); step++;
            }
            else if (step == 6 && t > 11.5)
            {
                var pool = Ub("CampPool", "PyriteCampPool"); var pens = (GameObject[])pool.GetProgramVariable("pens");
                W("   after PensBack pen0 " + pens[0].transform.position.ToString("F2") + " pen1 " + pens[1].transform.position.ToString("F2"));
                W("RESULT: DONE");
                step++;
                EditorApplication.isPlaying = false;
            }
        }
        catch (System.Exception e) { W("EXCEPTION " + e); step = 99; EditorApplication.isPlaying = false; }
    }
}
#endif
