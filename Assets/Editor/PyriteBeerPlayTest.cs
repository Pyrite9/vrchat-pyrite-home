// Tools ▸ Pyrite4 ▸ Z53e. Beer Play Test (ClientSim)
//  Play 에 들어가서 Beer_0 으로: 뚜껑 닫힘 → 집기 막힘 · 뚜껑 열기 → 집기 됨 · 따기(뚜껑 숨김 · 날아가는 뚜껑 · 거품 · 소리) · 8모금 → 빈 병 · 9번째 무시
//  · 멀리 옮긴 뒤 ReturnHome → 새 병 · 6 초 뒤 날아간 뚜껑 꺼짐 을 기록하고 Play 를 끝낸다. 결과 Logs/pyrite_beertest.txt
//  시간은 Play 프레임 누적(프레임당 최대 0.1 s, 프레임마다 한 번만 — EditorApplication.update 는 Play 중 한 프레임에 여러 번 불린다)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.Udon;

[InitializeOnLoad]
public static class PyriteBeerPlayTest
{
    const string KEY = "pyrite_beertest", LOG = "Logs/pyrite_beertest.txt";
    static float t; static int step = 0; static int pass = 0, fail = 0; static int lastFrame = -1;
    static Vector3 flyY0;

    static PyriteBeerPlayTest() { EditorApplication.update += Tick; }

    [MenuItem("Tools/Pyrite4/Z53e. Beer Play Test (ClientSim)", false, 5305)]
    public static void Run()
    {
        File.WriteAllText(LOG, "[Z53e] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        SessionState.SetBool(KEY, true); step = 0; t = 0f; pass = 0; fail = 0; lastFrame = -1;
        EditorApplication.isPlaying = true;
    }

    static void W(string s) { File.AppendAllText(LOG, s + "\n"); }
    static void Check(string what, bool ok) { if (ok) pass++; else fail++; W((ok ? "  ok   " : "  FAIL ") + what); }

    static UdonBehaviour Ub(GameObject g, string prog) => g == null ? null : g.GetComponents<UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == prog);

    static GameObject beer, stateGo, lidGo, fly;
    static UdonBehaviour B, S, L;

    static void Find()
    {
        beer = GameObject.Find("BeerCooler/Bottles/Beer_0");
        stateGo = GameObject.Find("BeerCooler/Bottles/Beer_0/State");
        lidGo = GameObject.Find("BeerCooler/Container/Lid");
        fly = GameObject.Find("BeerCooler/CapFly")?.transform.Find("CapFly_0")?.gameObject;
        B = Ub(beer, "PyriteBeer"); S = Ub(stateGo, "PyriteBeerState"); L = Ub(lidGo, "PyriteTrunkLid");
    }

    static bool Act(string path) { var tr = beer.transform.Find(path); return tr != null && tr.gameObject.activeInHierarchy; }
    static bool Pickable() { var pk = beer.GetComponent<VRCPickup>(); return pk != null && pk.pickupable; }
    static string St() => string.Format("opened {0} sips {1} | cap {2} full {3} empty {4} | pos {5}", S.GetProgramVariable("opened"), S.GetProgramVariable("sips"),
        Act("Visual/Body/Cap"), Act("Visual/Body/GlassFull"), Act("Visual/Body/GlassEmpty"), beer.transform.position.ToString("F3"));

    static void Tick()
    {
        if (!SessionState.GetBool(KEY, false)) return;
        if (!EditorApplication.isPlaying) { if (step > 0) { SessionState.SetBool(KEY, false); step = 0; } return; }
        if (EditorApplication.isPaused) return;
        if (Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        t += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
        try
        {
            if (step == 0 && t > 3f)
            {
                Find();
                if (B == null || S == null || L == null) { W("!! 못 찾음 beer " + (B != null) + " state " + (S != null) + " lid " + (L != null)); End(); return; }
                W("start: " + St() + " | lid open " + L.GetProgramVariable("open"));
                Check("처음 새 병 (opened false, sips 8, 뚜껑 보임, 가득)", !(bool)S.GetProgramVariable("opened") && (int)S.GetProgramVariable("sips") == 8 && Act("Visual/Body/Cap") && Act("Visual/Body/GlassFull") && !Act("Visual/Body/GlassEmpty"));
                Check("뚜껑 닫힘 → 박스 안 병 집기 막힘", !Pickable());
                L.SendCustomEvent("_interact"); W("-> lid _interact");
                step = 1;
            }
            else if (step == 1 && t > 4.5f)
            {
                Check("뚜껑 열림 (open true, 회전 " + lidGo.transform.localEulerAngles.z.ToString("F0") + "°)", (bool)L.GetProgramVariable("open") && Mathf.Abs(Mathf.DeltaAngle(lidGo.transform.localEulerAngles.z, -100f)) < 2f);
                Check("뚜껑 열림 → 집기 됨", Pickable());
                flyY0 = fly != null ? fly.transform.position : Vector3.zero;
                S.SendCustomEvent("Open"); W("-> Open");
                var src = stateGo.GetComponent<AudioSource>();
                Check("따기 소리 재생 중", src != null && src.isPlaying);
                step = 2;
            }
            else if (step == 2 && t > 4.9f)
            {
                W("opened: " + St());
                Check("딴 병: opened true · 병뚜껑 숨김", (bool)S.GetProgramVariable("opened") && !Act("Visual/Body/Cap"));
                var capW = beer.transform.Find("Visual/Body/Cap").position;
                Check("날아가는 뚜껑 켜짐 · 병뚜껑 자리에서 " + (fly != null ? Vector3.Distance(fly.transform.position, capW).ToString("F2") : "?") + " m · 위로 " + (fly != null ? (fly.transform.position.y - capW.y).ToString("F2") : "?") + " m", fly != null && fly.activeSelf && Vector3.Distance(fly.transform.position, capW) < 1.2f && fly.transform.position.y > capW.y - 0.05f);
                var foam = beer.transform.Find("Visual/Body/Foam")?.GetComponent<ParticleSystem>();
                Check("거품 파티클 " + (foam != null ? foam.particleCount.ToString() : "?") + "개", foam != null && foam.isPlaying && foam.particleCount > 0);
                var liqR = beer.transform.Find("Visual/Body/Liquid")?.GetComponent<Renderer>();
                Check("가득일 땐 공유 재질 (" + (liqR ? liqR.sharedMaterial.name : "?") + ")", liqR != null && !liqR.sharedMaterial.name.Contains("Instance"));
                for (int i = 0; i < 4; i++) S.SendCustomEvent("Sip");
                float lv4 = liqR ? liqR.sharedMaterial.GetFloat("_Level") : -1f;
                Check(string.Format("4모금 뒤 수면 {0:F3} (기대 {1:F3}) · 이 병만 복제 재질", lv4, Mathf.Lerp(0.022f, 0.192f, 3f / 7f)), liqR != null && Mathf.Abs(lv4 - Mathf.Lerp(0.022f, 0.192f, 3f / 7f)) < 0.002f && liqR.sharedMaterial.name.Contains("Instance"));
                for (int i = 0; i < 5; i++) S.SendCustomEvent("Sip");
                Check("8모금 뒤 액체 꺼짐", liqR != null && !liqR.gameObject.activeInHierarchy);
                W("-> Sip x4 + x5");
                W("after sips: " + St());
                Check("8모금 뒤 빈 병 (sips 0, 빈 유리)", (int)S.GetProgramVariable("sips") == 0 && Act("Visual/Body/GlassEmpty") && !Act("Visual/Body/GlassFull"));
                beer.transform.position += new Vector3(1.2f, 0f, -0.8f);
                beer.transform.rotation = Quaternion.Euler(30f, 40f, 0f);
                Physics.SyncTransforms();   // 손에 든 물건처럼 리지드바디 자세도 같게 (안 하면 보간이 놓기 처리의 세우기를 덮는다 — 시험만의 문제)
                W("moved → " + beer.transform.position.ToString("F3") + " (기울임 30°)");
                B.SendCustomEvent("_onDrop"); W("-> _onDrop (놓기 0.12 s 지연)");
                W("   dropT 직후 " + B.GetProgramVariable("dropT") + " · held " + B.GetProgramVariable("held"));
                var rb0 = beer.GetComponent<Rigidbody>();
                Check("놓은 직후엔 아직 처리 전 (키네마틱 그대로)", rb0 != null && rb0.isKinematic);
                step = 3;
            }
            else if (step == 3 && t > 5.4f)
            {
                var rb1 = beer.GetComponent<Rigidbody>();
                W("   step3 dropT " + B.GetProgramVariable("dropT") + " · kin " + (rb1 ? rb1.isKinematic.ToString() : "?") + " · rot " + beer.transform.eulerAngles.ToString("F1") + " · pk.IsHeld " + (beer.GetComponent<VRC.SDK3.Components.VRCPickup>() ? beer.GetComponent<VRC.SDK3.Components.VRCPickup>().IsHeld.ToString() : "?"));
                Check(string.Format("0.12 s 뒤 놓기 처리됨: 물리 켜짐 · 똑바로 (기울기 {0:F1}°)", Vector3.Angle(beer.transform.up, Vector3.up)), rb1 != null && !rb1.isKinematic && Vector3.Angle(beer.transform.up, Vector3.up) < 1f);
                Check("놓기 처리 뒤 회전 잠김 (" + (rb1 ? rb1.constraints.ToString() : "?") + ")", rb1 != null && rb1.constraints == RigidbodyConstraints.FreezeRotation);
                B.SendCustomEvent("_onPickup"); W("-> _onPickup");
                Check("다시 잡으면 회전 풀림 (" + (rb1 ? rb1.constraints.ToString() : "?") + ")", rb1 != null && rb1.constraints == RigidbodyConstraints.None);
                B.SetProgramVariable("held", false);   // 시험은 OnDrop 을 안 보내므로 직접 (안 그러면 뚜껑 판정이 멈춘다)
                B.SendCustomEvent("ReturnHome"); W("-> ReturnHome");
                Check("복귀하면 다시 잠김 (" + (rb1 ? rb1.constraints.ToString() : "?") + ")", rb1 != null && rb1.constraints == RigidbodyConstraints.FreezeRotation);
                step = 31;
            }
            else if (step == 31 && t > 5.9f)
            {
                var home = GameObject.Find("BeerCooler/Container/Slots/Slot_0").transform;
                W("returned: " + St() + " home " + home.position.ToString("F3"));
                Check("제자리 복귀 (오차 " + Vector3.Distance(beer.transform.position, home.position).ToString("F4") + " m)", Vector3.Distance(beer.transform.position, home.position) < 0.005f);
                Check("복귀 = 새 병 (opened false, sips 8, 뚜껑·가득)", !(bool)S.GetProgramVariable("opened") && (int)S.GetProgramVariable("sips") == 8 && Act("Visual/Body/Cap") && Act("Visual/Body/GlassFull"));
                var rb = beer.GetComponent<Rigidbody>();
                Check("복귀 뒤 키네마틱 (박스 안에서 안 움직임)", rb != null && rb.isKinematic);
                var lr2 = beer.transform.Find("Visual/Body/Liquid")?.GetComponent<Renderer>();
                Check("복귀 = 액체 다시 가득 (" + (lr2 ? lr2.sharedMaterial.GetFloat("_Level").ToString("F3") : "?") + ")", lr2 != null && lr2.gameObject.activeInHierarchy && Mathf.Abs(lr2.sharedMaterial.GetFloat("_Level") - 0.192f) < 0.002f);
                step = 4;
            }
            else if (step == 4 && t > 11.5f)
            {
                Check("6 초 뒤 날아간 뚜껑 꺼짐 (마지막 위치 " + (fly != null ? fly.transform.position.ToString("F2") : "?") + ")", fly != null && !fly.activeSelf);
                L.SendCustomEvent("_interact");
                step = 5;
            }
            else if (step == 5 && t > 13f)
            {
                Check("뚜껑 다시 닫힘 → 집기 막힘", !(bool)L.GetProgramVariable("open") && !Pickable());
                End();
            }
        }
        catch (System.Exception e) { W("EXCEPTION " + e); End(); }
    }

    static void End()
    {
        W(string.Format("RESULT: {0}/{1} pass{2}", pass, pass + fail, fail > 0 ? " — FAIL 있음" : ""));
        step = 99;
        EditorApplication.isPlaying = false;
    }
}
#endif
