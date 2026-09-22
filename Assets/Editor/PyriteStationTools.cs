// 캠프 의자 앉기(VRCStation) 설정 — 에디터 전용
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteStationTools
{
    static Transform FindDeep(Transform root, string startsWith)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith(startsWith)) return t;
        return null;
    }

    static Transform MakeChild(Transform parent, string name, Vector3 worldPos, Vector3 worldFwd)
    {
        Transform t = null;
        foreach (Transform c in parent) if (c.name == name) { t = c; break; }
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "seat point");
            t = go.transform;
            t.SetParent(parent, false);
        }
        t.position = worldPos;
        t.rotation = Quaternion.LookRotation(worldFwd.normalized, Vector3.up);
        t.localScale = Vector3.one;
        return t;
    }

    // ── 의자 앉기 ──────────────────────────────────────────────────────────
    // VRChat이 위치/회전을 어느 트랜스폼에서 읽는지 추측하지 않는다.
    // 대신 "회전 래퍼"를 만들어서 스테이션 오브젝트 · 좌석점 · 의자 메시가
    // 전부 같은 방향(화로를 봄)을 갖게 한다. 어느 것을 읽든 결과가 같다.
    //
    //   Camp/
    //     ChairSeat_1                  ← rot = 화로 방향. VRCStation + BoxCollider
    //       camp04_chair_BRN           ← 로컬 회전으로 메시의 "앉는 방향"을 부모 +Z에 맞춤
    //       SeatPoint / ExitPoint
    //
    const float SEAT_Y_OFFSET = -0.10f;   // 의자 바닥 기준. 음수 = 더 낮게
    const float SEAT_BACK     =  0.06f;
    const float EXIT_DIST     =  0.95f;

    /// 메시에서 "앉은 사람이 바라보는 방향"(로컬)을 뽑는다.
    /// 등받이는 메시의 위쪽 절반에 몰려 있으므로, 그 무게중심이 좌석 중심에서
    /// 어느 쪽으로 치우쳤는지를 보면 등 방향을 알 수 있다. 그 반대가 시선 방향.
    static Vector3 SitDirLocal(Transform ch, out string dbg)
    {
        var mf = ch.GetComponentInChildren<MeshFilter>();
        dbg = "";
        if (mf == null || mf.sharedMesh == null) { dbg = "메시 없음"; return Vector3.forward; }
        var v = mf.sharedMesh.vertices;
        if (v.Length == 0) { dbg = "정점 읽기 실패"; return Vector3.forward; }

        float ymin = float.MaxValue, ymax = float.MinValue;
        foreach (var p in v) { if (p.y < ymin) ymin = p.y; if (p.y > ymax) ymax = p.y; }
        float thr = ymin + (ymax - ymin) * 0.62f;

        Vector3 hi = Vector3.zero; int nHi = 0;
        Vector3 all = Vector3.zero;
        foreach (var p in v)
        {
            all += p;
            if (p.y > thr) { hi += p; nHi++; }
        }
        all /= v.Length;
        if (nHi < 8) { dbg = "등받이 정점 부족(" + nHi + ")"; return Vector3.forward; }
        hi /= nHi;

        Vector3 back = new Vector3(hi.x - all.x, 0f, hi.z - all.z);
        if (back.sqrMagnitude < 1e-6f) { dbg = "등받이 편향 없음"; return Vector3.forward; }
        back.Normalize();
        dbg = string.Format("정점 {0} / 등받이 {1} / 등방향 로컬 ({2:0.00},{3:0.00})",
                            v.Length, nHi, back.x, back.z);
        return -back;   // 등의 반대 = 시선
    }

    [MenuItem("Tools/Pyrite/6. Setup Chair Stations &#5")]
    public static void SetupChairStations()
    {
        GameObject camp = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "Camp") { camp = r; break; }
        if (camp == null) { Debug.LogError("[Pyrite] Camp 못 찾음"); return; }

        var fire = FindDeep(camp.transform, "camp05_campfire_stand");
        if (fire == null) { Debug.LogError("[Pyrite] 화로 못 찾음"); return; }
        Vector3 firePos = fire.position;

        // 의자는 Camp 직속일 수도, 이미 만든 래퍼 안에 있을 수도 있다
        var chairs = new List<Transform>();
        foreach (var t in camp.GetComponentsInChildren<Transform>(true))
            if (t.name.StartsWith("camp04_chair")) chairs.Add(t);
        if (chairs.Count == 0) { Debug.LogError("[Pyrite] 의자 못 찾음"); return; }

        int idx = 0;
        foreach (var ch in chairs)
        {
            idx++;

            // 이전 버전 잔재 제거
            var oldSt = ch.GetComponent<VRCStation>();
            if (oldSt != null) Undo.DestroyObjectImmediate(oldSt);
            var oldBox = ch.GetComponent<BoxCollider>();
            if (oldBox != null) Undo.DestroyObjectImmediate(oldBox);
            foreach (var c in ch.Cast<Transform>().ToList())
                if (c.name == "SeatPoint" || c.name == "ExitPoint" || c.name == "Station")
                    Undo.DestroyObjectImmediate(c.gameObject);

            Bounds b = new Bounds(); bool has = false;
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true))
            { if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds); }
            if (!has) { Debug.LogError("[Pyrite] " + ch.name + " 렌더러 없음"); continue; }

            Vector3 floor = new Vector3(b.center.x, b.min.y, b.center.z);
            Vector3 look = firePos - floor; look.y = 0f;
            if (look.sqrMagnitude < 0.0001f) look = ch.forward;
            look.Normalize();
            Quaternion lookRot = Quaternion.LookRotation(look, Vector3.up);

            string dbg;
            Vector3 sitLocal = SitDirLocal(ch, out dbg);

            // ── 회전 래퍼 ──
            string wrapName = "ChairSeat_" + idx;
            Transform wrap = null;
            foreach (Transform c in camp.transform) if (c.name == wrapName) wrap = c;
            if (wrap == null)
            {
                var go = new GameObject(wrapName);
                Undo.RegisterCreatedObjectUndo(go, "chair wrapper");
                wrap = go.transform;
                wrap.SetParent(camp.transform, false);
            }
            wrap.position = floor;
            wrap.rotation = lookRot;
            wrap.localScale = Vector3.one;

            // 의자를 래퍼 밑으로. 메시의 시선 방향이 래퍼의 +Z를 향하도록 로컬 회전
            Undo.SetTransformParent(ch, wrap, "reparent chair");
            ch.localPosition = new Vector3(0f, 0f, 0f);
            ch.localRotation = Quaternion.FromToRotation(sitLocal, Vector3.forward);
            ch.localScale = Vector3.one;
            // 바운즈 기준점이 의자 원점과 다를 수 있으니 XZ 보정
            Bounds b2 = new Bounds(); bool h2 = false;
            foreach (var r in ch.GetComponentsInChildren<Renderer>(true))
            { if (!h2) { b2 = r.bounds; h2 = true; } else b2.Encapsulate(r.bounds); }
            if (h2)
            {
                Vector3 d = new Vector3(floor.x - b2.center.x, floor.y - b2.min.y, floor.z - b2.center.z);
                ch.position += d;
            }

            Vector3 seatPos = floor + Vector3.up * SEAT_Y_OFFSET - look * SEAT_BACK;
            Vector3 exitPos = floor - look * EXIT_DIST + Vector3.up * 0.05f;
            var seat = MakeChild(wrap, "SeatPoint", seatPos, look);
            var exit = MakeChild(wrap, "ExitPoint", exitPos, look);

            var box = wrap.GetComponent<BoxCollider>();
            if (box == null) box = Undo.AddComponent<BoxCollider>(wrap.gameObject);
            Undo.RecordObject(box, "chair box");
            box.center = new Vector3(0f, 0.45f, 0f);
            box.size = new Vector3(0.58f, 0.20f, 0.58f);
            box.isTrigger = false;

            var st = wrap.GetComponent<VRCStation>();
            if (st == null) st = Undo.AddComponent<VRCStation>(wrap.gameObject);
            Undo.RecordObject(st, "station");
            st.stationEnterPlayerLocation = seat;
            st.stationExitPlayerLocation = exit;
            st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize;
            st.canUseStationFromStation = true;
            st.disableStationExit = false;
            st.seated = true;
            EditorUtility.SetDirty(wrap.gameObject);

            Debug.Log(string.Format(
                "[Pyrite] {0} ▸ {1}   래퍼 yaw {2:0.0}도 / 의자 월드 yaw {3:0.0}도 / 좌석 yaw {4:0.0}도  [{5}]",
                ch.name, wrapName, wrap.rotation.eulerAngles.y,
                ch.rotation.eulerAngles.y, seat.rotation.eulerAngles.y, dbg));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 의자 " + chairs.Count + "개 — 회전 래퍼로 통일. SEAT_Y_OFFSET=" + SEAT_Y_OFFSET);
    }
}
#endif
