// 캠프 배치 — 에디터 전용  [camp layout v1]
//
// 설계 의도
//  · 화로가 초점. 의자는 화로 건너편(+Z, 캠프 안쪽)에 놓아 앉으면
//    화로 너머로 호수(-Z)가 보인다.
//  · 타프+테이블은 서쪽 "주방 코너". 화로 원에서 한 발 빠져 있다.
//  · 텐트는 동쪽 뒤로 물려서 호수 시야를 막지 않는다.
//  · 랜턴 2개가 원 바깥을 둘러 빛 테두리를 만든다.
//  · 모든 좌표는 월드. 패드 중심(-10,53.5) 평탄반경 7.8m 안.
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteCampLayout
{
    struct P
    {
        public string name; public float x, z; public float yaw; public bool setYaw;
        public P(string n, float X, float Z) { name = n; x = X; z = Z; yaw = 0f; setYaw = false; }
        public P(string n, float X, float Z, float Y) { name = n; x = X; z = Z; yaw = Y; setYaw = true; }
    }

    // 이름 접두사 → 월드 XZ (+ 필요하면 월드 yaw)
    static readonly P[] LAYOUT = new[]
    {
        // 화로가 초점, 타프는 등 뒤 배경막, 텐트는 동쪽 끝으로 뺀다
        new P("camp05_campfire_stand", -10.5f, 51.5f),
        new P("camp02_hexa_tarp",       -9.3f, 57.6f,  90f),
        new P("camp03_table",          -13.6f, 55.4f,  70f),
        new P("camp01_tent",            -3.4f, 55.6f, 250f),
        new P("camp07_lantern_stand",   -7.6f, 53.0f),
        new P("camp06_lantern_YLW",    -14.4f, 54.3f),
    };

    // 의자는 화로 기준 극좌표. 회전은 스테이션 툴이 다시 계산한다.
    static readonly float[] CHAIR_ANG  = { 20f, -25f };   // +Z에서 시계/반시계
    static readonly float[] CHAIR_DIST = { 2.35f, 2.45f };

    static Terrain FindTerrain()
    {
        var t = Terrain.activeTerrain;
        if (t != null) return t;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { var x = r.GetComponentInChildren<Terrain>(); if (x != null) return x; }
        return null;
    }

    /// Camp 직속 자식 중, 이름이 prefix로 시작하는 트랜스폼을 품고 있는 것
    static Transform TopUnderCamp(Transform camp, string prefix)
    {
        foreach (Transform c in camp)
        {
            if (c.name.StartsWith(prefix)) return c;
            foreach (var t in c.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) return c;
        }
        return null;
    }

    [MenuItem("Tools/Pyrite/B. Arrange Camp &#4")]
    public static void Arrange()
    {
        GameObject camp = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "Camp") { camp = r; break; }
        if (camp == null) { Debug.LogError("[Pyrite] Camp 못 찾음"); return; }

        var terrain = FindTerrain();
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }

        int moved = 0;
        foreach (var p in LAYOUT)
        {
            var t = TopUnderCamp(camp.transform, p.name);
            if (t == null) { Debug.LogWarning("[Pyrite] 배치 대상 없음: " + p.name); continue; }
            Undo.RecordObject(t, "camp layout");
            float y = terrain.SampleHeight(new Vector3(p.x, 0f, p.z)) + terrain.transform.position.y;
            t.position = new Vector3(p.x, y, p.z);
            if (p.setYaw) t.rotation = Quaternion.Euler(0f, p.yaw, 0f);
            EditorUtility.SetDirty(t);
            moved++;
            Bounds wb = new Bounds(); bool hb = false;
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            { if (!hb) { wb = r.bounds; hb = true; } else wb.Encapsulate(r.bounds); }
            Debug.Log(string.Format("[Pyrite] 배치 {0,-26} → ({1:0.0}, {2:0.00}, {3:0.0})  yaw {4:0.0}  크기 X{5:0.0} Y{6:0.0} Z{7:0.0}",
                                    t.name, p.x, y, p.z, t.rotation.eulerAngles.y,
                                    wb.size.x, wb.size.y, wb.size.z));
        }

        // ── 의자: 화로 기준 원호 위 ──
        var fire = TopUnderCamp(camp.transform, "camp05_campfire_stand");
        if (fire == null) { Debug.LogError("[Pyrite] 화로 못 찾음"); return; }
        Vector3 f = fire.position;

        int ci = 0;
        foreach (Transform c in camp.transform)
        {
            bool isChair = c.name.StartsWith("ChairSeat_") || c.name.StartsWith("camp04_chair");
            if (!isChair) continue;
            if (ci >= CHAIR_ANG.Length) break;
            float a = CHAIR_ANG[ci] * Mathf.Deg2Rad;
            float d = CHAIR_DIST[ci];
            float x = f.x + Mathf.Sin(a) * d;
            float z = f.z + Mathf.Cos(a) * d;
            float y = terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
            Undo.RecordObject(c, "camp layout chair");
            c.position = new Vector3(x, y, z);
            EditorUtility.SetDirty(c);
            Debug.Log(string.Format("[Pyrite] 배치 {0,-26} → ({1:0.0}, {2:0.00}, {3:0.0})  화로거리 {4:0.00}m",
                                    c.name, x, y, z, d));
            ci++; moved++;
        }
        if (ci < CHAIR_ANG.Length) Debug.LogWarning("[Pyrite] 의자를 " + ci + "개만 찾음");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 캠프 배치 완료 — " + moved + "개. 이어서 Alt+Shift+5(의자 스테이션) 실행");

        // 의자 회전/좌석점 재계산
        PyriteStationTools.SetupChairStations();
    }
}
#endif
