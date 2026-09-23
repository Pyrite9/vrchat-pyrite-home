// Tools ▸ Pyrite2 ▸ Z28b. Camp Tweaks (fire, spin chair, carry chair)  /  Z28c. Revert
//  1) 화로 불빛: 기준 밝기 9.82 → 7.86 (−20%), 범위 5.5 → 4.2 m  (DayCycle.campBase[0] + Light.range. Z18b 상수도 같이 바꿈)
//  2) 회전의자 SpinChair: EditorOnly 태그 + 끔 (빌드에서 빠진다. 되돌리기 가능)
//  3) 들고 다니는 접이식 의자: CarryChair(타프 밑 (-11.2, 56.4), 모닥불 쪽) + CarryChair_1·2(기존 ChairSeat_1·2 자리, 원본은 EditorOnly)
//     루트 = Rigidbody(키네마틱) + VRCPickup + VRCObjectSync + PyriteCarryChair(놓으면 땅에 똑바로) + 등받이 콜라이더
//     Seat  = Rigidbody(키네마틱, 픽업과 분리) + 앉는 면 콜라이더 + VRCStation + PyriteCarrySeat(앉은 동안 들기 금지)
//     의자 메시는 ChairSeat_1 과 같은 camp04_chair_BRN. 등받이는 로컬 -Z → 앞(+Z)이 모닥불을 본다. 판정: 등받이 위쪽 = 들기, 앉는 면 = 앉기. 레이어 Pickup(13) → 몸에 안 걸린다
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

        // 3) 들고 다니는 의자: 타프 밑 새 의자 + 기존 의자 ChairSeat_1·2 를 같은 자리·방향의 들고 다니는 의자로 바꾼다
        foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.name.StartsWith("CarryChair")).ToArray()) Object.DestroyImmediate(o);
        var src = Find("ChairSeat_1");
        var srcMesh = src != null ? src.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault() : null;
        var srcStation = src != null ? src.GetComponent<VRCStation>() : null;
        if (srcMesh == null || srcStation == null) { sb.AppendLine("ChairSeat_1 메시/스테이션 없음"); Flush(sb); return; }
        var terr = Terrain.activeTerrain;
        var pos0 = CHAIR_AT; pos0.y = terr ? terr.SampleHeight(pos0) + terr.transform.position.y : 1.81f;
        var toFire = FIRE - pos0; toFire.y = 0; toFire.Normalize();
        if (!Make("CarryChair", pos0, Quaternion.LookRotation(toFire, Vector3.up))) return;           // 실측(Z29a): 등받이가 로컬 -Z → 앞 = +Z
        foreach (var n in new[] { "ChairSeat_1", "ChairSeat_2" })
        {
            var cs0 = Find(n); if (cs0 == null) { sb.AppendLine(n + " 없음"); continue; }
            var m0 = cs0.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(); if (m0 == null) continue;
            if (!Make("CarryChair_" + n.Substring(n.Length - 1), m0.transform.position, m0.transform.rotation)) return;
            cs0.tag = "EditorOnly"; cs0.SetActive(false); EditorUtility.SetDirty(cs0);
            sb.AppendLine("  " + n + " → EditorOnly + off (들고 다니는 의자로 대체)");
        }

        bool Make(string name, Vector3 pos, Quaternion rot)
        {
            var root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "carry chair");
            root.layer = PICKUP_LAYER;
            root.transform.SetPositionAndRotation(pos, rot);
            var mesh = Object.Instantiate(srcMesh.gameObject, root.transform);
            mesh.name = "Chair"; mesh.layer = PICKUP_LAYER; mesh.SetActive(true);
            mesh.transform.localPosition = Vector3.zero; mesh.transform.localRotation = Quaternion.identity; mesh.transform.localScale = srcMesh.transform.localScale;
            foreach (var c in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            GameObjectUtility.SetStaticEditorFlags(mesh, 0);

            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
            var back = root.AddComponent<BoxCollider>(); back.center = new Vector3(0f, 0.57f, -0.225f); back.size = new Vector3(0.50f, 0.22f, 0.06f);   // 등받이 위쪽만
            var pk = root.AddComponent<VRCPickup>();
            pk.AutoHold = VRC.SDKBase.VRC_Pickup.AutoHoldMode.Yes; pk.orientation = VRC.SDKBase.VRC_Pickup.PickupOrientation.Any;
            pk.InteractionText = "Carry chair"; pk.proximity = 2f; pk.pickupable = true; pk.allowManipulationWhenEquipped = false;
            var os = root.AddComponent<VRCObjectSync>(); os.AllowCollisionOwnershipTransfer = false;

            var seat = new GameObject("Seat"); seat.layer = PICKUP_LAYER;
            seat.transform.SetParent(root.transform, false);
            var srb = seat.AddComponent<Rigidbody>(); srb.isKinematic = true; srb.useGravity = false;
            var sc = seat.AddComponent<BoxCollider>(); sc.center = new Vector3(0f, 0.34f, 0.01f); sc.size = new Vector3(0.46f, 0.08f, 0.40f);        // 앉는 면만 (y 0.30~0.37)
            var st = seat.AddComponent<VRCStation>();
            EditorUtility.CopySerialized(srcStation, st);
            var seatPt = new GameObject("SeatPoint"); seatPt.transform.SetParent(root.transform, false); seatPt.transform.localPosition = new Vector3(0f, -0.10f, -0.06f);
            var exitPt = new GameObject("ExitPoint"); exitPt.transform.SetParent(root.transform, false); exitPt.transform.localPosition = new Vector3(0f, 0.05f, 0.65f);
            st.stationEnterPlayerLocation = seatPt.transform; st.stationExitPlayerLocation = exitPt.transform;
            EditorUtility.SetDirty(st);

            PyriteCarryChair cc; PyriteCarrySeat cs;
            try { cc = UdonSharpUndo.AddComponent<PyriteCarryChair>(root); cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(seat); }
            catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb); return false; }
            cs.station = st; cs.pickup = pk;
            UdonSharpEditorUtility.CopyProxyToUdon(cc); UdonSharpEditorUtility.CopyProxyToUdon(cs);
            var ubs = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs); if (ubs != null) { ubs.interactText = "Sit"; EditorUtility.SetDirty(ubs); }
            sb.AppendLine(name + " at " + pos.ToString("F2") + " yaw " + root.transform.eulerAngles.y.ToString("0"));
            return true;
        }

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
        foreach (var o in Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid() && g.name.StartsWith("CarryChair")).ToArray()) Object.DestroyImmediate(o);
        foreach (var n in new[] { "ChairSeat_1", "ChairSeat_2" }) { var g = Find(n); if (g != null) { g.tag = "Untagged"; g.SetActive(true); } }
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
