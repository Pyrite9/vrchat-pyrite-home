// Tools ▸ Pyrite ▸ Y1. Carry Lantern on Stand
//  camp07_lantern_stand 고리에 랜턴을 걸고, 집어서 들고 다닐 수 있게 만든다.
//  선행: Assets/Udon/PyriteLanternHook.cs 의 프로그램 에셋 (H. Udon Whitelist Probe 로 생성)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteCarryLantern
{
    const string FBX  = "Assets/Noagami/camp/Fbx/camp06_lantern_YLW.fbx";
    const string NAME = "Lantern_Carry";
    const string OUT  = "Assets/_preview/";

    [MenuItem("Tools/Pyrite/Y1. Carry Lantern on Stand", false, 290)]
    public static void Run()
    {
        var log = new StringBuilder("[Y1] ");
        var camp  = FindRoot("Camp");
        var stand = camp == null ? null : camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.StartsWith("camp07_lantern_stand"));
        if (stand == null) { Debug.LogError("[Y1] camp07_lantern_stand 없음"); return; }

        // ── 1. 고리 끝 찾기 (정점 기반) ─────────────────────────────
        var pts = stand.GetComponentsInChildren<MeshFilter>(true)
            .Where(f => f.sharedMesh != null)
            .SelectMany(f => f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v))).ToArray();
        if (pts.Length == 0) { Debug.LogError("[Y1] 스탠드 메시 정점 없음"); return; }
        float minY = pts.Min(p => p.y), maxY = pts.Max(p => p.y), H = maxY - minY;
        var basePts = pts.Where(p => p.y < minY + 0.10f * H).ToArray();
        var c = new Vector2(basePts.Average(p => p.x), basePts.Average(p => p.z));
        var upper = pts.Where(p => p.y > minY + 0.55f * H).ToArray();
        float dMax = upper.Max(p => Hz(p, c));
        var tip = upper.Where(p => Hz(p, c) > dMax - 0.06f).ToArray();
        var hookPt = new Vector3(tip.Average(p => p.x), tip.Min(p => p.y), tip.Average(p => p.z));
        log.Append("stand H=").Append(H.ToString("F2")).Append(" base=").Append(c.ToString("F2"))
           .Append(" arm=").Append(dMax.ToString("F2")).Append(" hook=").Append(hookPt.ToString("F3"))
           .Append(" tipVerts=").Append(tip.Length).Append(" | ");

        // ── 2. 랜턴 ────────────────────────────────────────────────
        var old = camp.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == NAME);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var oldHook = stand.Find("LanternHook");
        if (oldHook != null) Undo.DestroyObjectImmediate(oldHook.gameObject);

        var root = new GameObject(NAME);
        Undo.RegisterCreatedObjectUndo(root, "carry lantern");
        root.transform.SetParent(camp.transform, false);
        root.transform.SetPositionAndRotation(hookPt, Quaternion.Euler(0f, stand.eulerAngles.y, 0f));

        var src = AssetDatabase.LoadAssetAtPath<GameObject>(FBX);
        var vis = (GameObject)PrefabUtility.InstantiatePrefab(src, root.transform);
        vis.name = "Visual";
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;
        CopyMaterialsFrom(vis, camp, "camp06_lantern");

        // 손잡이 꼭대기(bounds 윗면 중심)가 고리 끝에 오도록
        var b = WorldBounds(vis);
        vis.transform.position += hookPt - new Vector3(b.center.x, b.max.y, b.center.z) + new Vector3(0f, 0.02f, 0f);   // 손잡이 고리가 갈고리 바닥을 감싸도록 2cm 올림
        b = WorldBounds(vis);
        log.Append("lantern size=").Append(b.size.ToString("F2")).Append(" | ");

        // 비정적 (Camp 는 Static Everything)

        // 쥐는 점 = 손잡이 꼭대기 → 손에 매달린 모양
        var grip = new GameObject("Grip").transform;
        grip.SetParent(root.transform, false);
        grip.position = new Vector3(b.center.x, b.max.y - 0.02f, b.center.z);
        grip.rotation = root.transform.rotation;

        // 몸체 피벗 = 손잡이 꼭대기. 들린 동안 Udon 이 이걸 세로로 세운다 (PC 손 방향 보정)
        var body = new GameObject("Body").transform;
        body.SetParent(root.transform, false);
        body.position = grip.position;
        body.rotation = root.transform.rotation;
        vis.transform.SetParent(body, true);

        // 빛 — 실시간, 그림자 없음 (움직이므로 Mixed/Baked 금지)
        var lgo = new GameObject("Light");
        lgo.transform.SetParent(body, false);
        lgo.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.45f, b.center.z);
        var l = lgo.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = new Color(1.00f, 0.80f, 0.42f);
        l.intensity = 1.5f;
        l.range = 6.5f;
        l.shadows = LightShadows.None;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        l.renderMode = LightRenderMode.ForcePixel;

        // 물리·픽업·동기화
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = root.transform.InverseTransformPoint(b.center);
        col.size = b.size + new Vector3(0.06f, 0.06f, 0.06f);

        var pk = root.AddComponent<VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes;
        pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Grip;
        pk.ExactGrip = grip;
        pk.InteractionText = "랜턴";
        pk.pickupable = true;
        pk.allowManipulationWhenEquipped = false;

        var os = root.AddComponent<VRCObjectSync>();
        os.AllowCollisionOwnershipTransfer = false;

        // 걸이 위치 표식 — 스탠드 자식 (스탠드를 옮겨도 같이 간다)
        var hook = new GameObject("LanternHook").transform;
        hook.SetParent(stand, true);
        hook.SetPositionAndRotation(root.transform.position, root.transform.rotation);

        // 비정적 (Camp 는 Static Everything)
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);

        var hb = UdonSharpUndo.AddComponent<PyriteLanternHook>(root);
        hb.hook = hook;
        hb.snapRadius = 0.8f;
        hb.body = body;
        UdonSharpEditorUtility.CopyProxyToUdon(hb);

        // ── 3. 시간대 조명 배수에 편입 ─────────────────────────────
        var tod = Object.FindObjectsOfType<PyriteTimeOfDay>(true).FirstOrDefault();
        if (tod != null)
        {
            var list = (tod.campLights ?? new Light[0]).Where(x => x != null).ToList();
            if (!list.Contains(l)) list.Add(l);
            tod.campLights = list.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(tod);
            log.Append("campLights=").Append(list.Count).Append(" | ");
        }
        else log.Append("ToD 없음 | ");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // ── 4. 확인 렌더 ───────────────────────────────────────────
        Directory.CreateDirectory(OUT);
        var fwd = stand.forward; fwd.y = 0; fwd.Normalize();
        var side = Vector3.Cross(Vector3.up, fwd);
        Shot(hookPt + fwd * 1.6f + side * 0.6f + Vector3.down * 0.3f, hookPt + Vector3.down * 0.35f, "lantern_stand_a", 50f);
        Shot(hookPt + side * 1.8f - fwd * 0.4f + Vector3.down * 0.2f, hookPt + Vector3.down * 0.35f, "lantern_stand_b", 50f);
        Shot(hookPt + fwd * 4.5f + side * 2f + Vector3.up * 0.4f,     hookPt + Vector3.down * 0.8f,  "lantern_stand_wide", 55f);
        AssetDatabase.Refresh();
        log.Append("renders -> ").Append(OUT);
        Debug.Log(log.ToString());
        File.WriteAllText(OUT + "lantern_report.txt", log.ToString());
    }

    static float Hz(Vector3 p, Vector2 c) => new Vector2(p.x - c.x, p.z - c.y).magnitude;

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static void CopyMaterialsFrom(GameObject target, GameObject camp, string prefix)
    {
        MeshRenderer donor = null;
        foreach (var t in camp.GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith(prefix) || target.transform.IsChildOf(t)) continue;
            donor = t.GetComponentInChildren<MeshRenderer>(true);
            if (donor != null) break;
        }
        if (donor == null) { Debug.LogWarning("[Y1] 머티리얼 원본 없음"); return; }
        foreach (var r in target.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterials = donor.sharedMaterials;
    }

    static GameObject FindRoot(string n)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects()) if (r.name == n) return r;
        return null;
    }

    static void Shot(Vector3 eye, Vector3 look, string name, float fov)
    {
        var go = new GameObject("__shot");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        cam.fieldOfView = fov; cam.nearClipPlane = 0.03f; cam.farClipPlane = 600f;
        cam.clearFlags = CameraClearFlags.Skybox;
        var rt = new RenderTexture(960, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(960, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(OUT + name + ".png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }
}
#endif
