// Tools ▸ Pyrite4 ▸ Z56a. Skip Stone Remote-Path Test (ClientSim)
//  2026-10-05: 물수제비 돌을 남이 던지면 수면 위에서 꺾여 보이던 문제 → 보는 쪽 궤적 계산(PyriteSkipStone.NetThrow/NetSkip/NetSink/NetEnd)
//  ClientSim 은 혼자라 보는 쪽 경로가 안 돈다 → dbgRemote 로 주인도 같은 계산을 돌려, 계산 위치(Mesh)와 물리 위치(루트)를 비교한다
//  ① 세게(17 m/s) 호수로 ② 약하게(7 m/s) ③ 뭍으로. 결과 Logs/pyrite_stone_sim.txt. Play 는 약 30 s 뒤 스스로 끝난다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteStoneSimTest
{
    const string KEY = "pyrite_stonesim", LOG = "Logs/pyrite_stone_sim.txt";
    static double t0 = -1; static int step = 0;
    static float minVisY, minRootY, maxVisAboveAtSkip; static int frames, simFrames;

    static PyriteStoneSimTest() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Pyrite4/Z56a. Skip Stone Remote-Path Test (ClientSim)", false, 6201)]
    public static void Run()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, "[Z56a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        SessionState.SetBool(KEY, true);
        EditorApplication.isPlaying = true;
    }

    static void W(string s) { File.AppendAllText(LOG, s + "\n"); }

    static GameObject Stone(int i)
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "AmbientFX");
        var t = root ? root.transform.Find("SkipStones/Stone_" + i) : null;
        return t ? t.gameObject : null;
    }

    static UdonBehaviour Ub(GameObject g) { return g ? g.GetComponents<UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteSkipStone") : null; }

    static void Throw(int i, Vector3 pos, Vector3 vel, string tag)
    {
        var g = Stone(i); var ub = Ub(g);
        if (ub == null) { W("!! Stone_" + i + " 없음"); return; }
        ub.SetProgramVariable("dbgRemote", true);
        ub.SetProgramVariable("dbgPos", pos);
        ub.SetProgramVariable("dbgVel", vel);
        ub.SendCustomEvent("DebugThrow");
        minVisY = 99f; minRootY = 99f; frames = 0; simFrames = 0;
        W("-> " + tag + ": Stone_" + i + " 위치 " + pos.ToString("F2") + " 속도 " + vel.ToString("F2") + " (" + vel.magnitude.ToString("F1") + " m/s)");
    }

    static void Sample(int i)
    {
        var g = Stone(i); var ub = Ub(g); if (ub == null) return;
        var vis = g.transform.Find("Mesh"); if (vis == null) return;
        frames++;
        if ((bool)ub.GetProgramVariable("dbgSimOn")) { simFrames++; if (vis.position.y < minVisY) minVisY = vis.position.y; }
        if (g.transform.position.y < minRootY) minRootY = g.transform.position.y;
    }

    static void Report(int i, string tag)
    {
        var g = Stone(i); var ub = Ub(g); if (ub == null) return;
        var vis = g.transform.Find("Mesh");
        var water = ub.GetProgramVariable("waterCollider") as Collider;
        float top = water ? water.bounds.max.y : float.NaN;
        W(string.Format("   {0}: 튐 {1}번, 계산↔물리 최대 거리 {2:F3} m, 계산 위치 최저 y {3:F3} (수면 콜라이더 윗면 {4:F3}, 닿는 높이 {5:F3}), 계산 중이던 프레임 {6}/{7}, 지금 계산 중 {8}, Mesh 로컬 위치 {9} (끝났으면 0 이어야), 돌 위치 {10}",
            tag, ub.GetProgramVariable("dbgSkips"), ub.GetProgramVariable("dbgMaxGap"), minVisY, top, top + 0.01f, simFrames, frames, ub.GetProgramVariable("dbgSimOn"),
            vis ? vis.localPosition.ToString("F4") : "?", g.transform.position.ToString("F2")));
    }

    static void Tick()
    {
        if (!SessionState.GetBool(KEY, false)) return;
        if (!EditorApplication.isPlaying) { if (step > 0) { SessionState.SetBool(KEY, false); step = 0; t0 = -1; } return; }
        if (t0 < 0) { t0 = EditorApplication.timeSinceStartup; step = 1; return; }
        double t = EditorApplication.timeSinceStartup - t0;
        try
        {
            if (step == 1 && t > 6) { Throw(0, new Vector3(-10f, 1.3f, 30.0f), new Vector3(0f, 0.9f, -17f), "① 세게 호수로"); step++; }
            else if (step == 2) { Sample(0); if (t > 14) { Report(0, "①"); step++; } }
            else if (step == 3) { Throw(1, new Vector3(-10f, 1.3f, 30.0f), new Vector3(1.5f, 0.3f, -7f), "② 약하게"); step++; }
            else if (step == 4) { Sample(1); if (t > 19) { Report(1, "②"); step++; } }
            else if (step == 5) { Throw(2, new Vector3(-10f, 3.2f, 45.0f), new Vector3(0f, 1f, 9f), "③ 뭍으로"); step++; }
            else if (step == 6) { Sample(2); if (t > 24) { Report(2, "③"); step++; } }
            else if (step == 7)
            {
                foreach (int i in new[] { 0, 1, 2 }) { var ub = Ub(Stone(i)); if (ub != null) ub.SetProgramVariable("dbgRemote", false); }
                W("RESULT: DONE");
                step++; EditorApplication.isPlaying = false;
            }
        }
        catch (System.Exception e) { W("EXCEPTION " + e); step = 99; EditorApplication.isPlaying = false; }
    }
}
#endif
