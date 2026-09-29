// PyriteBeanbagPoolBuild.cs — 침실 빈백 6개 풀 + 들고 옮기기 (Z52i 빌드 / Z52j 되돌림). 재실행 안전
//  2026-09-30 관리자: "빈백을 0~6개 개수조절 · 색상 3개 · 캠프사이트 의자처럼 들고 위치조정" / 조절 = 머리맡 패널, 셋째 색 = 오트밀, 기본 2개
//  구조 (캠프 CarryChair 와 같은 방식, 전부 새 오브젝트 — 기존 오브젝트에 Udon 을 붙이면 빌드 네트워크 ID 가 깨짐):
//   TentBedroom/BeanbagPool (U# PyriteBeanbagPool, mask 동기화) / Homes/Home_1~6
//   TentBedroom/Beanbags/Beanbag_1~6 (Pickup 레이어): Rigidbody(키네마틱) + 뒤쪽 잡기 상자 + VRCPickup "Carry beanbag" + VRCObjectSync + PyriteCarryChair(놓으면 바닥에 똑바로)
//     Body (침실 레이어, Beanbag.asset + 색 3가지 순환: 머스터드 · 딥 틸 · 오트밀)
//     Seat (Pickup 레이어): Rigidbody(키네마틱, 분리) + 앞쪽 앉기 상자 + VRCStation(옛 Beanbag_1 설정 복사 = AC_BeanbagSit) + PyriteCarrySeat(앉은 동안 들기 금지)
//       SitPoint · ExitPoint (옛 값 복사)
//   옛 Beanbags(Z51l) 는 이름을 Beanbags_Old 로 바꿔 끄고 EditorOnly — Z52j 가 복구
//  제자리: 1·2 = Z52g V 자, 3~6 = 러그 앞쪽 빈 바닥(서로 1.4 m 이상, 침대·낮은 테이블·입구·컨테이너 피함), TV 쪽을 보되 무리 가운데로 25°
//  ⚠ 새 U# 라 첫 실행은 프로그램 에셋만 만들고 멈춤 → Refresh 후 다시. 그 뒤 Z50a(패널 재빌드)로 패널 빈백 줄 연결 → SDK UI 셰이더 Auto Fix
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteBeanbagPoolBuild
{
    const string DIR = "Assets/Bedroom/Beanbag/";
    const string PREV = "Assets/_preview/bedroom/";
    const int PICKUP_LAYER = 13;
    static readonly Vector3 TV = new Vector3(-2.67f, 1.20f, -0.90f);
    static readonly Vector3[] HOME =
    {
        new Vector3(-0.93f, 0f, 1.63f), new Vector3(0.03f, 0f, 0.57f),     // Z52g V 자
        new Vector3(0.82f, 0f, 1.72f), new Vector3(-1.82f, 0f, 0.41f), new Vector3(1.98f, 0f, 0.49f), new Vector3(2.20f, 0f, 1.95f),
    };
    const float V_INWARD = 35f, OTHER_INWARD = 25f;
    static readonly Color[] COL = { new Color(0.52f, 0.38f, 0.17f), new Color(0.13f, 0.29f, 0.31f), new Color(0.70f, 0.64f, 0.53f) };   // 머스터드 · 딥 틸 · 오트밀
    static readonly string[] CNAME = { "Mustard", "Teal", "Oatmeal" };
    const int DEFAULT_MASK = 3;
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z52i. Beanbag Pool Build", false, 5135)]
    public static void Build() => Run("Z52i", true);

    [MenuItem("Tools/Pyrite3/Z52j. Beanbag Pool Revert", false, 5136)]
    public static void Revert() => Run("Z52j", false);

    static void Run(string tag, bool build)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (build ? Inner() : Undo_()) sb.AppendLine("RESULT: DONE"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_beanbag_pool.txt", sb.ToString(), new UTF8Encoding(false));
    }

    static Transform Room()
    {
        var r = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        if (r == null) sb.AppendLine("!! TentBedroom 없음");
        return r ? r.transform : null;
    }

    static bool Undo_()
    {
        var room = Room(); if (room == null) return false;
        foreach (var n in new[] { "BeanbagPool", "Beanbags" }) { var t = room.Find(n); if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(n + " 삭제"); } }
        var old = room.Find("Beanbags_Old");
        if (old) { old.name = "Beanbags"; old.gameObject.SetActive(true); old.gameObject.tag = "Untagged"; sb.AppendLine("Beanbags_Old → Beanbags 복구 (패널 빈백 줄은 Z50a 다시 돌리면 '없음'으로)"); }
        Save(); return true;
    }

    static bool Inner()
    {
        bool p1 = EnsureProgram("PyriteBeanbagPool");
        if (!p1) return false;
        var room = Room(); if (room == null) return false;

        // 옛 빈백(원본 설정) 확보: Beanbags_Old 가 있으면 그것, 없으면 지금 Beanbags 를 옛것으로 돌림
        var old = room.Find("Beanbags_Old");
        if (old == null)
        {
            var cur = room.Find("Beanbags");
            if (cur == null) { sb.AppendLine("!! Beanbags 없음 (Z51l → Z51o 먼저)"); return false; }
            cur.name = "Beanbags_Old";
            old = cur;
        }
        else { var cur = room.Find("Beanbags"); if (cur) Object.DestroyImmediate(cur.gameObject); }
        var oldPool = room.Find("BeanbagPool"); if (oldPool) Object.DestroyImmediate(oldPool.gameObject);
        old.gameObject.SetActive(true);   // 값 읽기
        var src = old.Find("Beanbag_1");
        var srcSeat = src ? src.Find("Seat") : null; var srcSt = srcSeat ? srcSeat.GetComponent<VRCStation>() : null;
        var srcBody = src ? src.Find("Body") : null; var mesh = srcBody ? srcBody.GetComponent<MeshFilter>().sharedMesh : null;
        if (srcSt == null || mesh == null) { sb.AppendLine("!! 옛 Beanbag_1 Seat/Body 없음"); return false; }
        var sitL = srcSeat.Find("SitPoint").localPosition; var exitL = srcSeat.Find("ExitPoint").localPosition;
        sb.AppendLine("원본: " + old.name + "/Beanbag_1 · 컨트롤러 " + (srcSt.animatorController ? srcSt.animatorController.name : "기본") + " · SitPoint " + V(sitL) + " · ExitPoint " + V(exitL) + " · 메시 " + mesh.name);

        // 바닥 레이어 (PyriteCarryChair.Settle 이 쓰는 마스크): 제자리마다 아래로 쏴서 맞는 레이어
        int ground = 1 | (1 << 11);
        var floorHits = new List<string>();
        foreach (var h in HOME)
        {
            var w = room.TransformPoint(h + Vector3.up * 1.0f);
            var hits = Physics.RaycastAll(w, Vector3.down, 2f, ~((1 << PICKUP_LAYER) | (1 << 10) | (1 << 9)), QueryTriggerInteraction.Ignore).OrderBy(x => x.distance).ToArray();
            if (hits.Length > 0) { var hh = hits[0]; ground |= 1 << hh.collider.gameObject.layer; floorHits.Add(hh.collider.name + "[" + LayerMask.LayerToName(hh.collider.gameObject.layer) + "] y " + room.InverseTransformPoint(hh.point).y.ToString("F2")); }
            else floorHits.Add("없음");
        }
        sb.AppendLine("제자리 아래 첫 충돌: " + string.Join(" | ", floorHits) + " → groundMask " + ground);
        old.gameObject.SetActive(false); old.gameObject.tag = "EditorOnly";

        // 방향: 1·2 는 서로 쪽으로 35°, 3~6 은 무리 가운데 쪽으로 25° (TV 정면 기준)
        var cen = HOME.Aggregate(Vector3.zero, (a, b) => a + b) / HOME.Length;
        Quaternion Yaw(int i)
        {
            var toTV = TV - HOME[i]; toTV.y = 0f; toTV.Normalize();
            Vector3 f;
            if (i < 2) { var o = HOME[1 - i] - HOME[i]; o.y = 0f; f = Vector3.RotateTowards(toTV, o.normalized, V_INWARD * Mathf.Deg2Rad, 0f); }
            else { var c = cen - HOME[i]; c.y = 0f; f = c.sqrMagnitude < 1e-4f ? toTV : Vector3.RotateTowards(toTV, c.normalized, OTHER_INWARD * Mathf.Deg2Rad, 0f); }
            return Quaternion.LookRotation(f, Vector3.up);
        }

        var mats = new Material[3];
        for (int c = 0; c < 3; c++) mats[c] = Mat("M_Beanbag_" + (c + 1), COL[c]);

        var poolGo = new GameObject("BeanbagPool"); poolGo.transform.SetParent(room, false);
        var homesT = new GameObject("Homes").transform; homesT.SetParent(poolGo.transform, false);
        var bagsRoot = new GameObject("Beanbags").transform; bagsRoot.SetParent(room, false);
        var bags = new GameObject[HOME.Length]; var homes = new Transform[HOME.Length];
        for (int i = 0; i < HOME.Length; i++)
        {
            var q = Yaw(i);
            var hm = new GameObject("Home_" + (i + 1)).transform; hm.SetParent(homesT, false); hm.localPosition = HOME[i]; hm.localRotation = q; homes[i] = hm;

            var root = new GameObject("Beanbag_" + (i + 1)); root.layer = PICKUP_LAYER;
            root.transform.SetParent(bagsRoot, false); root.transform.localPosition = HOME[i]; root.transform.localRotation = q;
            var body = new GameObject("Body"); body.transform.SetParent(root.transform, false); body.layer = PyriteBedroomV3.LAYER;
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = body.AddComponent<MeshRenderer>(); mr.sharedMaterial = mats[i % 3]; mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.On;

            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var grab = root.AddComponent<BoxCollider>(); grab.center = new Vector3(0f, 0.37f, -0.23f); grab.size = new Vector3(0.60f, 0.54f, 0.26f);   // 뒤쪽(등받이) 절반 = 들기
            var pk = root.AddComponent<VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
            pk.InteractionText = "Carry beanbag"; pk.proximity = 2f; pk.pickupable = true; pk.allowManipulationWhenEquipped = false;
            var os = root.AddComponent<VRCObjectSync>(); os.AllowCollisionOwnershipTransfer = false;

            var seat = new GameObject("Seat"); seat.layer = PICKUP_LAYER; seat.transform.SetParent(root.transform, false);
            var srb = seat.AddComponent<Rigidbody>(); srb.isKinematic = true; srb.useGravity = false;
            var sc = seat.AddComponent<BoxCollider>(); sc.center = new Vector3(0f, 0.31f, 0.13f); sc.size = new Vector3(0.71f, 0.62f, 0.46f);        // 앞쪽 = 앉기
            var st = seat.AddComponent<VRCStation>();
            EditorUtility.CopySerialized(srcSt, st);
            var sp = new GameObject("SitPoint").transform; sp.SetParent(seat.transform, false); sp.localPosition = sitL;
            var ex = new GameObject("ExitPoint").transform; ex.SetParent(seat.transform, false); ex.localPosition = exitL;
            st.stationEnterPlayerLocation = sp; st.stationExitPlayerLocation = ex;
            EditorUtility.SetDirty(st);

            var cc = UdonSharpUndo.AddComponent<PyriteCarryChair>(root); cc.groundMask = ground;
            var cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(seat); cs.station = st; cs.pickup = pk;
            UdonSharpEditorUtility.CopyProxyToUdon(cc); UdonSharpEditorUtility.CopyProxyToUdon(cs);
            var ubs = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs); if (ubs != null) { ubs.interactText = "Sit"; ubs.proximity = 2f; EditorUtility.SetDirty(ubs); }

            root.SetActive((DEFAULT_MASK & (1 << i)) != 0);
            bags[i] = root;
            sb.AppendLine("  Beanbag_" + (i + 1) + " " + CNAME[i % 3] + " 제자리 " + V(HOME[i]) + " yaw " + q.eulerAngles.y.ToString("F0") + (root.activeSelf ? " (기본 켬)" : ""));
        }
        // 제자리끼리 거리
        float md = 99f; for (int a = 0; a < HOME.Length; a++) for (int b = a + 1; b < HOME.Length; b++) md = Mathf.Min(md, (HOME[a] - HOME[b]).magnitude);
        sb.AppendLine("제자리 최소 간격 " + md.ToString("F2") + " m (빈백 지름 약 1.2)");

        var pool = UdonSharpUndo.AddComponent<PyriteBeanbagPool>(poolGo);
        pool.bags = bags; pool.homes = homes; pool.mask = DEFAULT_MASK;
        UdonSharpEditorUtility.CopyProxyToUdon(pool);
        sb.AppendLine("풀: 빈백 " + bags.Length + " · 기본 mask " + DEFAULT_MASK + " (" + CountBits(DEFAULT_MASK) + "개)");
        Save();
        Shots(room, bags);
        return true;
    }

    static int CountBits(int m) { int c = 0; while (m != 0) { c += m & 1; m >>= 1; } return c; }

    // 6개 전부 켠 모습 + 기본(2개) 모습
    static void Shots(Transform room, GameObject[] bags)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var act = bags.Select(b => b.activeSelf).ToArray();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 58f;
            Shot(cam, room.TransformPoint(new Vector3(1.7f, 1.7f, 2.4f)), room.TransformPoint(new Vector3(-0.3f, 0.3f, 0.6f)), "bp_2_room");
            foreach (var b in bags) b.SetActive(true);
            Shot(cam, room.TransformPoint(new Vector3(1.7f, 1.7f, 2.4f)), room.TransformPoint(new Vector3(-0.3f, 0.3f, 0.6f)), "bp_6_room");
            Shot(cam, room.TransformPoint(new Vector3(-2.3f, 1.5f, -0.6f)), room.TransformPoint(new Vector3(0.3f, 0.3f, 1.2f)), "bp_6_tvside");
            try { PyriteBedroomLayoutProbe.Run(); File.Copy(PREV + "lay_top.jpg", PREV + "bp_6_top.jpg", true); sb.AppendLine("  top bp_6_top"); } catch (System.Exception e) { sb.AppendLine("  (위 렌더 실패 " + e.Message + ")"); }
        }
        finally
        {
            for (int i = 0; i < bags.Length; i++) bags[i].SetActive(act[i]);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
            Save();
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    static Material Mat(string name, Color c)
    {
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { var src = AssetDatabase.LoadAssetAtPath<Material>(DIR + "M_Beanbag_1.mat"); m = src ? new Material(src) : new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; EditorUtility.SetDirty(m);
        return m;
    }

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Assets ▸ Refresh 후 Z52i 다시");
        return false;
    }

    static void Save() { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
}
#endif
