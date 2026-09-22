using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Occlusion Culling 준비 + 굽기
//  M  : 정적 플래그 정리 — 큰 덩어리만 Occluder, 나머지는 Occludee
//  M2 : 굽기 (백그라운드)
//  M3 : 상태 / 취소
//
//  주의
//   · 런타임에 켜고 끄는 것(거울면, 시간대 패널, 파티클)은 정적으로 잡으면 안 된다 → 제외 목록
//   · ContributeGI 같은 기존 플래그는 지우지 않고 OR 로 더하기만 한다. 지우면 라이트맵이 깨진다
public static class PyriteOcclusionTools
{
    // 이 이름(또는 그 자식)은 건드리지 않는다
    static readonly string[] SKIP_ROOTS =
    { "Mirror", "TimePanel", "TimeDial", "LightFX", "Ambience", "Main Camera", "VRCWorld", "EventSystem" };

    const float OCCLUDER_MIN = 3.0f;   // 이 크기(최대 변, m) 넘는 것만 가림막으로 쓴다

    [MenuItem("Tools/Pyrite/M. Setup Occlusion Flags", false, 262)]
    public static void SetupFlags()
    {
        int occluder = 0, occludee = 0, skipped = 0;
        var big = new List<string>();

        foreach (var r in Object.FindObjectsOfType<Renderer>(true))
        {
            var go = r.gameObject;
            if (InSkip(go.transform)) { skipped++; continue; }
            if (r is ParticleSystemRenderer) { skipped++; continue; }
            if (!go.activeInHierarchy) { skipped++; continue; }

            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            // 애초에 정적이 아닌 것(런타임 생성물 등)은 그대로 둔다
            if (flags == 0) { skipped++; continue; }

            flags |= StaticEditorFlags.OccludeeStatic;
            occludee++;

            float sz = Mathf.Max(r.bounds.size.x, Mathf.Max(r.bounds.size.y, r.bounds.size.z));
            bool opaque = IsOpaque(r);
            if (sz >= OCCLUDER_MIN && opaque)
            {
                flags |= StaticEditorFlags.OccluderStatic;
                occluder++;
                if (big.Count < 12) big.Add(go.name + " " + sz.ToString("F1") + "m");
            }
            else
            {
                flags &= ~StaticEditorFlags.OccluderStatic;   // 작은 것이 가림막이면 데이터만 커진다
            }
            GameObjectUtility.SetStaticEditorFlags(go, flags);
        }

        // 터레인은 Renderer 가 아니라 따로 잡는다
        foreach (var t in Object.FindObjectsOfType<Terrain>(true))
        {
            var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            f |= StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, f);
            occluder++;
        }

        // 굽기 파라미터 — 절벽이 커서 가림막 하한을 넉넉히 잡아도 된다. 데이터가 작아진다.
        StaticOcclusionCulling.smallestOccluder   = 2.5f;
        StaticOcclusionCulling.smallestHole       = 0.25f;
        StaticOcclusionCulling.backfaceThreshold  = 100f;   // 100 = 뒷면 제거 안 함(닫히지 않은 메시가 많을 때 안전)

        EditorSceneManager_MarkDirty();
        Debug.Log(string.Format(
            "[OCC] 플래그 정리 — Occluder {0} / Occludee {1} / 건너뜀 {2}\n  큰 가림막 예: {3}\n  파라미터: smallestOccluder {4} / smallestHole {5} / backface {6}",
            occluder, occludee, skipped, string.Join(", ", big),
            StaticOcclusionCulling.smallestOccluder, StaticOcclusionCulling.smallestHole,
            StaticOcclusionCulling.backfaceThreshold));
    }

    [MenuItem("Tools/Pyrite/M2. Bake Occlusion", false, 263)]
    public static void Bake()
    {
        if (StaticOcclusionCulling.isRunning) { Debug.LogWarning("[OCC] 이미 굽는 중"); return; }
        StaticOcclusionCulling.GenerateInBackground();
        Debug.Log("[OCC] 굽기 시작 — M3 으로 상태 확인");
    }

    [MenuItem("Tools/Pyrite/M3. Occlusion Status", false, 264)]
    public static void Status()
    {
        Debug.Log(string.Format("[OCC] 진행중={0} / 데이터={1} / 용량={2:F2} MB",
            StaticOcclusionCulling.isRunning,
            StaticOcclusionCulling.umbraDataSize > 0 ? "있음" : "없음",
            StaticOcclusionCulling.umbraDataSize / 1048576f));
    }

    [MenuItem("Tools/Pyrite/M4. Cancel Occlusion Bake", false, 265)]
    public static void Cancel()
    {
        StaticOcclusionCulling.Cancel();
        Debug.Log("[OCC] 취소 요청");
    }

    static bool InSkip(Transform t)
    {
        while (t != null)
        {
            foreach (var s in SKIP_ROOTS) if (t.name == s) return true;
            t = t.parent;
        }
        return false;
    }

    static bool IsOpaque(Renderer r)
    {
        foreach (var m in r.sharedMaterials)
        {
            if (m == null) return false;
            int q = m.renderQueue;
            if (q < 0) q = m.shader != null ? m.shader.renderQueue : 2000;
            if (q >= 2450) return false;      // AlphaTest 경계 위 = 투명 계열
        }
        return true;
    }

    static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
