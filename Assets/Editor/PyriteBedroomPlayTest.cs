// PyriteBedroomPlayTest.cs — Tools ▸ Pyrite3 ▸ Z49e. Bedroom Play Test (ClientSim)
//  Play 진입 → 6 s 뒤 TentDoor 의 _interact → 2 s 뒤 위치 기록 → DoorFlap 의 _interact → 2 s 뒤 위치 기록 → Play 종료
//  결과: Logs/pyrite_bedroom.txt + 클립보드
//  🔴 Play 진입 때 "VideoPlayerShim Missing" 대화상자 → No. Console Error Pause 는 꺼 둘 것
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteBedroomPlayTest
{
    const string KEY = "PyriteBedroomPlayTest";
    static double t0;
    static float el;
    static int stage;
    static string log;

    static PyriteBedroomPlayTest()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(KEY, false))
            {
                t0 = EditorApplication.timeSinceStartup; el = 0f; stage = 0; log = "[Z49e] " + System.DateTime.Now.ToString("HH:mm:ss") + " ClientSim\n";
                EditorApplication.update += Tick;
            }
        };
    }

    [MenuItem("Tools/Pyrite3/Z49e. Bedroom Play Test (ClientSim)", false, 4904)]
    public static void Run()
    {
        if (EditorApplication.isPlaying) return;
        SessionState.SetBool(KEY, true);
        EditorApplication.EnterPlaymode();
    }

    static UdonBehaviour UB(string rootName, string child)
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == rootName);
        if (root == null) return null;
        var t = child == null ? root.transform : root.transform.Find(child);
        return t ? t.GetComponent<UdonBehaviour>() : null;
    }

    static string Pos()
    {
        var p = Networking.LocalPlayer;
        if (p == null) return "LocalPlayer null";
        var v = p.GetPosition();
        return "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ") yaw " + p.GetRotation().eulerAngles.y.ToString("0");
    }

    static void Tick()
    {
        el += Mathf.Min(Time.unscaledDeltaTime, 0.1f);   // 모달 대화상자 동안 멈춘 시간은 세지 않는다
        double t = el;
        try
        {
            if (stage == 0 && t > 6)
            {
                log += "start " + Pos() + "\n";
                var ub = UB("TentDoor", null);
                log += "TentDoor udon " + (ub != null) + "\n";
                if (ub != null) ub.SendCustomEvent("_interact");
                stage = 1;
            }
            else if (stage == 1 && t > 8)
            {
                log += "after TentDoor → " + Pos() + "  (기대 (2000.00, 0.0x, -2.00) yaw 0)\n";
                var ub = UB("TentBedroom", "DoorFlap");
                log += "DoorFlap udon " + (ub != null) + "\n";
                if (ub != null) ub.SendCustomEvent("_interact");
                stage = 2;
            }
            else if (stage == 2 && t > 10)
            {
                log += "after DoorFlap → " + Pos() + "  (기대 (-5.56, 1.83, 54.35) yaw 240)\n";
                stage = 3;
                Finish();
            }
            else if (EditorApplication.timeSinceStartup - t0 > 120) { log += "TIMEOUT stage " + stage + "\n"; Finish(); }
        }
        catch (System.Exception e) { log += "EXCEPTION " + e.Message + "\n"; Finish(); }
    }

    static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(KEY, false);
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_bedroom.txt", log);
        EditorGUIUtility.systemCopyBuffer = log;
        EditorApplication.ExitPlaymode();
    }
}
#endif
