// Tools ▸ Pyrite2 ▸ Z28b. Camp Tweaks (fire, spin chair, carry chair)  /  Z28c. Revert
//  1) 화로 불빛: 기준 밝기 9.82 → 7.86 (−20%), 범위 5.5 → 4.2 m  (DayCycle.campBase[0] + Light.range. Z18b 상수도 같이 바꿈)
//  2) 회전의자 SpinChair: EditorOnly 태그 + 끔 (빌드에서 빠진다. 되돌리기 가능)
//  3) 들고 다니는 접이식 의자 CarryChair: 타프 밑 (-11.2, 56.4), 모닥불 쪽을 본다
//     루트 = Rigidbody(키네마틱) + VRCPickup + VRCObjectSync + PyriteCarryChair(놓으면 땅에 똑바로) + 등받이 콜라이더
//     Seat  = Rigidbody(키네마틱, 픽업과 분리) + 앉는 면 콜라이더 + VRCStation + PyriteCarrySeat(앉은 동안 들기 금지)
//     의자 메시는 ChairSeat_1 과 같은 camp04_chair_BRN, SeatPoint/ExitPoint 도 같은 로컬 위치. 레이어 Pickup(13) → 몸에 안 걸린다
//  렌더 21:00 전후 → Assets/_preview/camp/tweak_*.png
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

public static class PyriteCampTweaks
{
    const float FIRE_BASE = 7.86f, FIRE_BASE_OLD = 9.82f, FIRE_RANGE = 4.2f, FIRE_RANGE_OLD = 5.5f;
    static readonly Vector3 CHAIR_AT = new Vector3(-11.2f, 0f, 56.4f);
    static readonly Vector3 FIRE = new Vector3(-10.5f, 0f, 51.5f);
    const int PICKUP_LAYER = 13;

    [MenuItem("Tools/Pyrite2/Z28b. Camp Tweaks (fire, spin chair, carry chair)", false, 21)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z28b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc == null) { sb.AppendLine("DayCycle 없음"); Flush(sb); return; }
        Shots(cyc, "before", sb);

        // 1) 화로
        var fire = cyc.campLights[0];
        sb.AppendLine(string.Format("fire {0}: base {1} → {2}, range {3:0.0} → {4}", fire ? fire.name : "null", cyc.campBase[0], FIRE_BASE, fire ? fire.range : 0f, FIRE_RANGE));
        var cb = cyc.campBase.ToArray(); cb[0] = FIRE_BASE; cyc.campBase = cb;
        if (fire != null) { Undo.RecordObject(fire, "fire"); fire.range = FIRE_RANGE; EditorUtility.SetDirty(fire); }
        UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);

        // 2) 회전의자
        var spin = Find("SpinChair");
        if (spin != null) { spin.tag = "EditorOnly"; spin.SetActive(false); EditorUtility.SetDirty(spin); sb.AppendLine("SpinChair → EditorOnly + off"); }
        else sb.AppendLine("SpinChair 없음");

        // 3) 들고 다니는 의자
        var old = Find("CarryChair"); if (old != null) Object.DestroyImmediate(old);
        var src = Find("ChairSeat_1");
        var srcMesh = src != null ? src.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault() : null;
        var srcStation = src != null ? src.GetComponent<VRCStation>() : null;
        if (srcMesh == null || srcStation == null) { sb.AppendLine("ChairSeat_1 메시/스테이션 없음"); Flush(sb); return; }
        var terr = Terrain.activeTerrain;
        var pos = CHAIR_AT; pos.y = terr ? terr.SampleHeight(pos) + terr.transform.position.y : 1.81f;
        var toFire = FIRE - pos; toFire.y = 0; toFire.Normalize();
        var root = new GameObject("CarryChair");
        Undo.RegisterCreatedObjectUndo(root, "carry chair");
        root.layer = PICKUP_LAYER;
        root.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-toFire, Vector3.up));   // 의자 앞 = 로컬 -Z (ExitPoint 가 -0.95)
        var mesh = Object.Instantiate(srcMesh.gameObject, root.transform);
        mesh.name = "Chair"; mesh.layer = PICKUP_LAYER;
        mesh.transform.localPosition = Vector3.zero; mesh.transform.localRotation = Quaternion.identity; mesh.transform.localScale = srcMesh.transform.localScale;
        foreach (var c in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        GameObjectUtility.SetStaticEditorFlags(mesh, 0);
        var mb = mesh.GetComponent<Renderer>().bounds;
        sb.AppendLine("chair bounds size " + mb.size.ToString("F2") + " min y " + (mb.min.y - pos.y).ToString("0.00") + " max y " + (mb.max.y - pos.y).ToString("0.00"));

        var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        var back = root.AddComponent<BoxCollider>(); back.center = new Vector3(0f, 0.58f, 0.2f); back.size = new Vector3(0.56f, 0.42f, 0.14f);
        var pk = root.AddComponent<VRCPickup>();
        pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
        pk.InteractionText = "Carry chair"; pk.proximity = 2f; pk.pickupable = true; pk.allowManipulationWhenEquipped = false;
        var os = root.AddComponent<VRCObjectSync>(); os.AllowCollisionOwnershipTransfer = false;

        var seat = new GameObject("Seat"); seat.layer = PICKUP_LAYER;
        seat.transform.SetParent(root.transform, false);
        var srb = seat.AddComponent<Rigidbody>(); srb.isKinematic = true; srb.useGravity = false;
        var sc = seat.AddComponent<BoxCollider>(); sc.center = new Vector3(0f, 0.36f, -0.06f); sc.size = new Vector3(0.52f, 0.14f, 0.46f);
        var st = seat.AddComponent<VRCStation>();
        EditorUtility.CopySerialized(srcStation, st);
        var seatPt = new GameObject("SeatPoint"); seatPt.transform.SetParent(root.transform, false); seatPt.transform.localPosition = new Vector3(0f, -0.10f, -0.06f);
        var exitPt = new GameObject("ExitPoint"); exitPt.transform.SetParent(root.transform, false); exitPt.transform.localPosition = new Vector3(0f, 0.05f, -0.95f);
        st.stationEnterPlayerLocation = seatPt.transform; st.stationExitPlayerLocation = exitPt.transform;
        EditorUtility.SetDirty(st);

        PyriteCarryChair cc; PyriteCarrySeat cs;
        try { cc = UdonSharpUndo.AddComponent<PyriteCarryChair>(root); cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(seat); }
        catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb); return; }
        cs.station = st; cs.pickup = pk;
        UdonSharpEditorUtility.CopyProxyToUdon(cc); UdonSharpEditorUtility.CopyProxyToUdon(cs);
        var ubs = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs); if (ubs != null) { ubs.interactText = "Sit"; EditorUtility.SetDirty(ubs); }
        sb.AppendLine("CarryChair at " + pos.ToString("F2") + " yaw " + root.transform.eulerAngles.y.ToString("0") + " | station copied from ChairSeat_1 (mobility " + st.PlayerMobility + ")");

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Shots(cyc, "after", sb);
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z28c. Camp Tweaks Revert", false, 22)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z28c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (cyc != null)
        {
            var cb = cyc.campBase.ToArray(); cb[0] = FIRE_BASE_OLD; cyc.campBase = cb;
            if (cyc.campLights[0] != null) { cyc.campLights[0].range = FIRE_RANGE_OLD; EditorUtility.SetDirty(cyc.campLights[0]); }
            UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        }
        var spin = Find("SpinChair"); if (spin != null) { spin.tag = "Untagged"; spin.SetActive(true); }
        var cc = Find("CarryChair"); if (cc != null) Object.DestroyImmediate(cc);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(PyriteDayCycle cyc, string tag, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        Directory.CreateDirectory("Assets/_preview/camp/");
        var views = new (string n, Vector3 e, Vector3 l, float f)[]
        {
            ("fire", new Vector3(-10.4f, 3.6f, 55.8f), new Vector3(-10.5f, 2.0f, 51.0f), 70f),
            ("tarp", new Vector3(-9.0f, 3.3f, 52.2f), new Vector3(-11.2f, 2.2f, 56.4f), 60f),
        };
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(21f);
            float sum = 0; int n = 0;
            foreach (var v in views)
            {
                var px = Shot(cam, v.e, v.l, v.f, "Assets/_preview/camp/tweak_" + tag + "_" + v.n + ".png");
                if (v.n == "fire") { for (int i = 0; i < px.Length; i += 7) { sum += px[i].grayscale; n++; } }
            }
            sb.AppendLine(string.Format("  {0}: fire view mean {1:0.0}", tag, 255f * sum / Mathf.Max(1, n)));
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static Color[] Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1280, H = 720;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        var px = tx.GetPixels();
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        return px;
    }

    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_camp3.txt", sb.ToString()); }
}
#endif
