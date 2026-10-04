// PyriteBedroomPlayTest.cs — Tools ▸ Pyrite3 ▸ Z49e. Bedroom Play Test (ClientSim)
//  Play 진입 → 6 s 뒤 TentDoor → 침실 → Lie_2 눕기 → 이불 주머니(켬) → 확인 → 이불 끔 + ExitStation → DoorFlap → 캠프 → Play 종료
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
    static int stage, lastFrame;
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
        if (Time.frameCount == lastFrame) return;       // EditorApplication.update 는 한 프레임에 여러 번 불린다 → 프레임당 한 번만 센다
        lastFrame = Time.frameCount;
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
                log += "after TentDoor → " + Pos() + "  (기대 (1997.60, 0.02, 1.30) yaw 90)\n";
                var lie = UB("TentBedroom", "Beds/Lie_2");
                log += "Lie_2 udon " + (lie != null) + "\n";
                if (lie != null) lie.SendCustomEvent("_interact");
                stage = 2;
            }
            else if (stage == 2 && t > 10)
            {
                var lp = GameObject.Find("TentBedroom/Beds/Lie_2/LiePoint");
                log += "after Lie → " + Pos() + "  (LiePoint " + (lp ? lp.transform.position.ToString("F2") : "?") + ")\n";
                log += "window cam (침실 안) " + WinCam() + "\n";
                var sack = UB("TentBedroom", "Beds/StuffSack");
                log += "StuffSack udon " + (sack != null) + "\n";
                if (sack != null) sack.SendCustomEvent("_interact");
                stage = 3;
            }
            else if (stage == 3 && t > 12.5)
            {
                var sack = UB("TentBedroom", "Beds/StuffSack");
                var cover = GameObject.Find("TentBedroom/Beds/BlanketCover");
                var foldT = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g => g.name == "TentBedroom").transform.Find("Beds/BlanketFold");
                var fold = foldT != null && foldT.gameObject.activeInHierarchy ? foldT.gameObject : null;
                object on = sack != null ? sack.GetProgramVariable("isOn") : null;
                var m = cover ? cover.GetComponent<Renderer>().sharedMaterial : null;
                log += "blanket isOn " + on + " cover " + (cover ? cover.GetComponent<Renderer>().enabled.ToString() : "?") + " fold active " + (fold != null) + " _Drop " + (m ? m.GetFloat("_Drop").ToString("F2") : "?") + " _SegCount " + (m ? m.GetFloat("_SegCount").ToString("F0") : "?") + "\n";
                if (sack != null) sack.SendCustomEvent("_interact");
                var lieGo = GameObject.Find("TentBedroom/Beds/Lie_2");   // 2026-10-01 눕기 Station 제거됨 → 없으면 건너뜀
                var st = lieGo ? lieGo.GetComponent<VRC.SDK3.Components.VRCStation>() : null;
                if (st != null) st.ExitStation(Networking.LocalPlayer);
                stage = 4;
            }
            else if (stage == 4 && t > 15)
            {
                var sack = UB("TentBedroom", "Beds/StuffSack");
                var cover = GameObject.Find("TentBedroom/Beds/BlanketCover");
                log += "blanket off → isOn " + (sack != null ? sack.GetProgramVariable("isOn") : null) + " cover " + (cover ? cover.GetComponent<Renderer>().enabled.ToString() : "?") + "\n";
                log += "after ExitStation → " + Pos() + "  (ExitPoint 기대 z 0.25)\n";
                var ub = UB("TentBedroom", "DoorFlap");
                if (ub != null) ub.SendCustomEvent("_interact");
                stage = 5;
            }
            else if (stage == 5 && t > 17)
            {
                log += "after DoorFlap → " + Pos() + "  (기대 (-5.56, 1.83, 54.35) yaw 240)\n";
                stage = 6;
            }
            else if (stage == 6 && t > 19)
            {
                log += "window cam (캠프) " + WinCam() + "\n";
                stage = 7;
                Finish();
            }
            else if (EditorApplication.timeSinceStartup - t0 > 120) { log += "TIMEOUT stage " + stage + "\n"; Finish(); }
        }
        catch (System.Exception e) { log += "EXCEPTION " + e.Message + "\n"; Finish(); }
    }

    static string WinCam()
    {
        var root = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "BedroomWindowCam");
        if (root == null) return "없음";
        var c = root.GetComponent<Camera>();
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat");
        return "camera.enabled " + (c ? c.enabled.ToString() : "?") + " _LiveOn " + (m ? m.GetFloat("_LiveOn").ToString("F0") : "?");
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
