// Tools ▸ Pyrite2 ▸ Z29b. Lanterns + TV Autoplay  /  Z29c. Revert
//  1) 맵의 모든 랜턴을 들고 다니게: camp06_lantern_YLW · Lantern_Tarp · Lantern_Dock · Lantern_DockRoot 를
//     Lantern_Carry 와 같은 구조의 새 루트(Pick_*: Rigidbody + VRCPickup + VRCObjectSync + PyriteLanternHook, 레이어 Pickup)로 감싼다
//     (기존 오브젝트에 Udon 을 붙이지 않는다 — 붙이면 빌드가 AssignSceneNetworkIDs 에서 멈춘다)
//     루트 피벗 = 손잡이 꼭대기(랜턴 위치 + 0.28 m), 콜라이더는 물리용(트리거 아님), 회전 고정 → 떨어져도 똑바로 선다
//  2) 걸이: 랜턴 스탠드 LanternHook + 부두 끝 기둥 LanternHook_Dock. 든 채로 좌클릭(사용) → 가까운 빈 걸이에 걸기, 아니면 땅에 떨어뜨리기
//  3) 밝기 (화로 7.86 / 4.2 m 보다 작게): 들고 다니는 랜턴 7.59 → 3.0 (6.5 → 4.0 m), 노란 랜턴 10.13 → 3.0 (3.0 → 3.5 m),
//     타프·부두 랜턴 1.5 유지 (6.5 → 4.0 m). 노란 랜턴 불꽃 Animator 가 세기를 덮어쓰면 Animator 를 끈다(로그)
//  4) ProTV 자동 재생: autoplayMainUrl = 관리자 영상, 반복 재생. 영상 빔 기본 켬 (자동 재생 화면이 보이게)
//  되돌리기 값은 Logs/pyrite_lantern_revert.txt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharp;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteLanternsTV
{
    const string REVERT = "Logs/pyrite_lantern_revert.txt";
    const string VIDEO = "https://www.youtube.com/watch?v=dR03EqtruJg";
    static readonly string[] WRAP = { "camp06_lantern_YLW", "Lantern_Tarp", "Lantern_Dock", "Lantern_DockRoot" };
    const int PICKUP_LAYER = 13;

    [MenuItem("Tools/Pyrite2/Z29b. Lanterns + TV Autoplay", false, 31)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z29b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        if (File.Exists(REVERT)) { Revert(); sb.AppendLine("이전 빌드 되돌림 후 다시"); }
        var revert = new List<string>();
        var carry = Find("Lantern_Carry");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var stand = Find("camp07_lantern_stand");
        var standHook = stand != null ? stand.transform.Find("LanternHook") : null;
        if (carry == null || cyc == null || standHook == null) { sb.AppendLine("Lantern_Carry / DayCycle / LanternHook 없음"); Flush(sb); return; }
        var cHook = carry.GetComponent<PyriteLanternHook>();
        var cPick = carry.GetComponent<VRCPickup>();
        var cBox = carry.GetComponent<BoxCollider>();
        var cGrip = carry.transform.Find("Grip");
        var cBody = carry.transform.Find("Body");
        var cVisual = cBody.Find("Visual");
        float drop = -cVisual.localPosition.y;                                    // 손잡이 꼭대기 → 랜턴 원점 (0.28)
        var cBoxC = cBox.center; var cBoxS = cBox.size;
        sb.AppendLine(string.Format("carry template: visual offset {0}, box c {1} s {2}", cVisual.localPosition.ToString("F3"), cBox.center.ToString("F2"), cBox.size.ToString("F2")));

        // 1) 감싸기
        var roots = new List<Transform> { carry.transform };
        Transform dockHook = null;
        foreach (var n in WRAP)
        {
            var lan = Find(n);
            if (lan == null) { sb.AppendLine("  " + n + " 없음"); continue; }
            var t = lan.transform;
            revert.Add(string.Format("P|{0}|{1}|{2}|{3}|{4}", n, t.parent ? PathOf(t.parent) : "", V(t.position), Q(t.rotation), V(t.localScale)));
            var root = new GameObject("Pick_" + n);
            root.layer = PICKUP_LAYER;
            root.transform.SetParent(t.parent, false);
            root.transform.SetPositionAndRotation(t.position + t.rotation * (-cVisual.localPosition), t.rotation);
            var grip = new GameObject("Grip").transform; grip.SetParent(root.transform, false);
            grip.localPosition = cGrip.localPosition; grip.localRotation = cGrip.localRotation;
            var body = new GameObject("Body").transform; body.SetParent(root.transform, false);
            t.SetParent(body, true);
            t.localPosition = cVisual.localPosition; t.localRotation = Quaternion.identity;
            var rb = root.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.constraints = RigidbodyConstraints.FreezeRotation;
            var box = root.AddComponent<BoxCollider>(); box.center = cBox.center; box.size = cBox.size; box.isTrigger = false;
            var pk = root.AddComponent<VRCPickup>();
            EditorUtility.CopySerialized(cPick, pk);
            pk.ExactGrip = grip;
            root.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
            PyriteLanternHook lh;
            try { lh = UdonSharpUndo.AddComponent<PyriteLanternHook>(root); }
            catch (System.Exception e) { sb.AppendLine("AddComponent 실패: " + e.Message); Flush(sb); return; }
            lh.body = body; lh.snapRadius = cHook.snapRadius; lh.swing = cHook.swing;
            roots.Add(root.transform);
            revert.Add("W|Pick_" + n);
            sb.AppendLine(string.Format("  wrap {0} → root {1}", n, root.transform.position.ToString("F2")));
            if (n == "Lantern_Dock")
            {
                var dh = new GameObject("LanternHook_Dock").transform;
                dh.SetParent(root.transform.parent, false);
                dh.SetPositionAndRotation(root.transform.position, root.transform.rotation);
                dockHook = dh; revert.Add("W|LanternHook_Dock");
            }
        }
        var hooks = new[] { standHook, dockHook }.Where(h => h != null).ToArray();

        // 2) 모든 랜턴: 걸이·점유·물리 콜라이더·문구
        foreach (var r in roots)
        {
            var lh = r.GetComponent<PyriteLanternHook>();
            lh.hooks = hooks; lh.others = roots.ToArray();
            UdonSharpEditorUtility.CopyProxyToUdon(lh); EditorUtility.SetDirty(lh);
            var pk = r.GetComponent<VRCPickup>(); pk.InteractionText = "Lantern"; pk.UseText = "Hang / Drop"; EditorUtility.SetDirty(pk);
            var rb = r.GetComponent<Rigidbody>(); rb.constraints = RigidbodyConstraints.FreezeRotation; EditorUtility.SetDirty(rb);
            var bx = r.GetComponent<BoxCollider>(); bx.isTrigger = false;
            bx.center = new Vector3(cBoxC.x, -drop * 0.5f, cBoxC.z); bx.size = new Vector3(cBoxS.x, drop, cBoxS.z);   // 손잡이 꼭대기 ~ 바닥면 (떨어졌을 때 뜨지 않게)
            EditorUtility.SetDirty(bx);
        }
        revert.Add("C|" + cBox.isTrigger + "|" + V(cBoxC) + "|" + V(cBoxS));
        sb.AppendLine("hooks: " + string.Join(", ", hooks.Select(h => h.name + " " + h.position.ToString("F2"))) + " | lanterns " + roots.Count);

        // 3) 밝기
        revert.Add("B|" + string.Join(",", cyc.campBase.Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        var cb = cyc.campBase.ToArray(); cb[1] = 3.0f; cb[2] = 3.0f; cyc.campBase = cb;
        UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        void Range(string lanName, float r)
        {
            var g = Find(lanName); if (g == null) return;
            foreach (var l in g.GetComponentsInChildren<Light>(true)) { revert.Add(string.Format("L|{0}|{1}", PathOf(l.transform), l.range.ToString(System.Globalization.CultureInfo.InvariantCulture))); l.range = r; EditorUtility.SetDirty(l); }
        }
        Range("Lantern_Carry", 4.0f); Range("camp06_lantern_YLW", 3.5f); Range("Lantern_Tarp", 4.0f); Range("Lantern_Dock", 4.0f);
        var ylw = Find("camp06_lantern_YLW");
        var anim = ylw != null ? ylw.GetComponentInChildren<Animator>(true) : null;
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            var clips = anim.runtimeAnimatorController.animationClips;
            bool animatesIntensity = false; float maxI = 0f;
            foreach (var c in clips)
                foreach (var b in AnimationUtility.GetCurveBindings(c))
                    if (b.type == typeof(Light) && b.propertyName.ToLower().Contains("intensity"))
                    {
                        animatesIntensity = true;
                        var curve = AnimationUtility.GetEditorCurve(c, b);
                        maxI = Mathf.Max(maxI, curve.keys.Max(k => k.value));
                    }
            sb.AppendLine(string.Format("ylw lantern animator: clips {0}, animates intensity {1} (max {2:0.00})", clips.Length, animatesIntensity, maxI));
            if (animatesIntensity) { anim.enabled = false; revert.Add("N|" + PathOf(anim.transform)); EditorUtility.SetDirty(anim); sb.AppendLine("  → Animator 끔 (DayCycle 이 세기를 정한다)"); }
        }
        sb.AppendLine("campBase " + string.Join("/", cyc.campBase) + " (fire / yellow lantern / carry lantern / dock root)");

        // 4) ProTV 자동 재생 + 영상 빔 기본 켬
        var tvm = Object.FindObjectsOfType<UdonSharpBehaviour>(true).FirstOrDefault(u => u.GetType().Name == "TVManager");
        if (tvm != null)
        {
            var so = new SerializedObject(tvm);
            var url = so.FindProperty("autoplayMainUrl").FindPropertyRelative("url");
            revert.Add("U|" + url.stringValue + "|" + so.FindProperty("autoplayLoop").boolValue);
            url.stringValue = VIDEO;
            so.FindProperty("autoplayLoop").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            UdonSharpEditorUtility.CopyProxyToUdon(tvm); EditorUtility.SetDirty(tvm);
            sb.AppendLine("TV autoplay " + VIDEO + " loop true");
        }
        else sb.AppendLine("TVManager 없음");
        var vp = Object.FindObjectOfType<PyriteVideoProjector>(true);
        if (vp != null) { vp.isOn = true; UdonSharpEditorUtility.CopyProxyToUdon(vp); EditorUtility.SetDirty(vp); revert.Add("V|false"); sb.AppendLine("video projector default ON"); }

        Directory.CreateDirectory("Logs"); File.WriteAllLines(REVERT, revert);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Shots(cyc, sb);
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z29c. Lanterns + TV Revert", false, 32)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z29c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        if (!File.Exists(REVERT)) { sb.AppendLine("revert 파일 없음"); Flush(sb); return; }
        var lines = File.ReadAllLines(REVERT);
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        foreach (var line in lines.OrderBy(l => l.StartsWith("L|") ? 0 : 1))   // 조명 경로는 감싸진 상태 기준 → 먼저
        {
            var p = line.Split('|');
            if (p[0] == "P")
            {
                var lan = Find(p[1]); if (lan == null) continue;
                var parent = string.IsNullOrEmpty(p[2]) ? null : Object.FindObjectsOfType<Transform>(true).FirstOrDefault(x => PathOf(x) == p[2]);
                lan.transform.SetParent(parent, true);
                lan.transform.SetPositionAndRotation(PV(p[3]), PQ(p[4])); lan.transform.localScale = PV(p[5]);
            }
            if (p[0] == "L") { var t = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(x => PathOf(x) == p[1]); if (t) t.GetComponent<Light>().range = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture); }
            if (p[0] == "B" && cyc != null) { cyc.campBase = p[1].Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(cyc); }
            if (p[0] == "N") { var t = Object.FindObjectsOfType<Transform>(true).FirstOrDefault(x => PathOf(x) == p[1]); if (t) t.GetComponent<Animator>().enabled = true; }
            if (p[0] == "C") { var c = Find("Lantern_Carry"); if (c) { var b = c.GetComponent<BoxCollider>(); b.isTrigger = bool.Parse(p[1]); b.center = PV(p[2]); b.size = PV(p[3]); } }
            if (p[0] == "U")
            {
                var tvm = Object.FindObjectsOfType<UdonSharpBehaviour>(true).FirstOrDefault(u => u.GetType().Name == "TVManager");
                if (tvm != null) { var so = new SerializedObject(tvm); so.FindProperty("autoplayMainUrl").FindPropertyRelative("url").stringValue = p[1]; so.FindProperty("autoplayLoop").boolValue = bool.Parse(p[2]); so.ApplyModifiedPropertiesWithoutUndo(); UdonSharpEditorUtility.CopyProxyToUdon(tvm); }
            }
            if (p[0] == "V") { var vp = Object.FindObjectOfType<PyriteVideoProjector>(true); if (vp) { vp.isOn = false; UdonSharpEditorUtility.CopyProxyToUdon(vp); } }
        }
        foreach (var line in lines.Where(l => l.StartsWith("W|"))) { var g = Find(line.Substring(2)); if (g) Object.DestroyImmediate(g); sb.AppendLine("  삭제 " + line.Substring(2)); }
        var carry = Find("Lantern_Carry");
        if (carry) { var lh = carry.GetComponent<PyriteLanternHook>(); lh.hooks = new Transform[0]; lh.others = new Transform[0]; UdonSharpEditorUtility.CopyProxyToUdon(lh); }
        File.Delete(REVERT);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Shots(PyriteDayCycle cyc, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        Directory.CreateDirectory("Assets/_preview/camp/");
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(21f);
            Shot(cam, new Vector3(-10.4f, 3.8f, 57.5f), new Vector3(-10.5f, 1.9f, 52.5f), 75f, "Assets/_preview/camp/lanterns_21_camp.png");
            Shot(cam, new Vector3(-12.5f, 2.6f, 36.0f), new Vector3(-10.2f, 1.0f, 40.0f), 65f, "Assets/_preview/camp/lanterns_21_dock.png");
            sb.AppendLine("  shots lanterns_21_{camp,dock}");
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
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
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static string V(Vector3 v) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2}", v.x, v.y, v.z);
    static string Q(Quaternion q) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1},{2},{3}", q.x, q.y, q.z, q.w);
    static float[] Fs(string s) => s.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
    static Vector3 PV(string s) { var f = Fs(s); return new Vector3(f[0], f[1], f[2]); }
    static Quaternion PQ(string s) { var f = Fs(s); return new Quaternion(f[0], f[1], f[2], f[3]); }
    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_lantern.txt", sb.ToString()); }
}
#endif
