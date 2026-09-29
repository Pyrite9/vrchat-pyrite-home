// PyriteBedroomQvPenButtons.cs — 침실 QvPen 의 Respawn/Clear 버튼 없애기 (Z51x 숨김 / Z51y 복구). 재실행 안전
//  2026-09-30 관리자: 수면 100% 인데 펜 버튼(Unlit 흰 점선 사각)이 너무 빛남 → A = 버튼 완전 제거
//  🔴 오브젝트는 지우지 않는다: QvPen_EraserManager 는 respawnButton.SetActive 를 null 검사 없이 부름 → 지우면 지우개 잡을 때 Udon 정지.
//     그래서 버튼 오브젝트 아래 Renderer · Collider 만 끔 (보이지도 눌리지도 않음, Udon 의 SetActive 는 그대로 동작)
//  대상: TentBedroom/BedroomQvPen 아래 PenManager·EraserManager 의 respawnButton / clearButton (U# 필드에서 읽음) + 세트 설정 패널 Bureau/UI
//   (Canvas Image 라 Renderer 가 아니라 UI Graphic 을 꺼야 안 보임 — 첫 시도에서 Renderer 0 개로 헛돎)
//  ⚠ Z51f(QvPen 세트 재빌드) 뒤에는 다시 Z51x
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomQvPenButtons
{
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z51x. Bedroom QvPen Buttons Hide", false, 5124)]
    public static void Hide() { Run(false); }

    [MenuItem("Tools/Pyrite3/Z51y. Bedroom QvPen Buttons Restore", false, 5125)]
    public static void Restore() { Run(true); }

    static void Run(bool on)
    {
        sb = new StringBuilder("[" + (on ? "Z51y" : "Z51x") + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
            var qv = room ? room.transform.Find("BedroomQvPen") : null;
            if (qv == null) { sb.AppendLine("!! TentBedroom/BedroomQvPen 없음"); return; }
            if (!on) Shots(room.transform, "qb_before");

            var buttons = new List<GameObject>();
            foreach (var mb in qv.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tn = mb.GetType().Name;
                if (tn != "QvPen_PenManager" && tn != "QvPen_EraserManager") continue;
                var so = new SerializedObject(mb);
                foreach (var f in new[] { "respawnButton", "clearButton" })
                {
                    var p = so.FindProperty(f);
                    var go = p != null ? p.objectReferenceValue as GameObject : null;
                    sb.AppendLine("  " + Path(mb.transform, qv) + " [" + tn + "] ." + f + " = " + (go ? Path(go.transform, qv) : (p == null ? "(필드 없음)" : "null")));
                    if (go && !buttons.Contains(go)) buttons.Add(go);
                }
            }
            // 세트 전체 설정 패널 (로고 · ShowOrHide · ResetAll · ClearAll · 모드 토글 3) — 흰 세로 줄. 같이 숨김 (모드는 현재 기본값 고정: DoubleClick 켬 · LateSync 켬 · Surftrace 끔)
            var panelUI = qv.Find("Bureau/UI");
            if (panelUI && !buttons.Contains(panelUI.gameObject)) buttons.Add(panelUI.gameObject);
            sb.AppendLine("  설정 패널 Bureau/UI " + (panelUI ? "포함" : "없음"));
            // 버튼 말고도 보이는 UI 전부 (켜진 것만): 경로 · 방 로컬 위치 · 스프라이트
            foreach (var g in qv.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Where(g => g.enabled && g.gameObject.activeInHierarchy))
            {
                var img = g as UnityEngine.UI.Image;
                sb.AppendLine("    [UI] " + Path(g.transform, qv) + " " + g.GetType().Name + (img && img.sprite ? " sprite " + img.sprite.name : "") + " 위치 " + room.transform.InverseTransformPoint(g.transform.position).ToString("F2") + " 색 " + g.color);
            }
            foreach (var r in qv.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy))
                sb.AppendLine("    [R] " + Path(r.transform, qv) + " " + r.GetType().Name + " " + string.Join(",", r.sharedMaterials.Where(m => m).Select(m => m.name + "(" + (m.shader ? m.shader.name : "?") + ")")));
            // 구조 덤프 (버튼 하나) — 무엇으로 그려지는지 확인용
            if (buttons.Count > 0)
                foreach (var t in buttons[0].GetComponentsInChildren<Transform>(true))
                    sb.AppendLine("    · " + Path(t, qv) + " [" + string.Join(",", t.GetComponents<Component>().Where(c => c).Select(c => c.GetType().Name)) + "] active " + t.gameObject.activeSelf);
            int nr = 0, nc = 0, ng = 0;
            foreach (var b in buttons)
            {
                foreach (var r in b.GetComponentsInChildren<Renderer>(true)) { Undo.RecordObject(r, "qvpen btn"); r.enabled = on; nr++; }
                foreach (var c in b.GetComponentsInChildren<Collider>(true)) { Undo.RecordObject(c, "qvpen btn"); c.enabled = on; nc++; }
                foreach (var g in b.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) { Undo.RecordObject(g, "qvpen btn"); g.enabled = on; ng++; }   // Canvas UI 이미지·텍스트
                var mats = b.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m).Select(m => m.name + "(" + (m.shader ? m.shader.name : "?") + ")").Distinct();
                sb.AppendLine("  " + (on ? "복구" : "숨김") + " " + Path(b.transform, qv) + " · 재질 " + string.Join(", ", mats));
            }
            sb.AppendLine("버튼 " + buttons.Count + "개 · Renderer " + nr + " · UI Graphic " + ng + " · Collider " + nc + " → " + (on ? "켬" : "끔") + " (오브젝트는 남김)");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            if (!on) Shots(room.transform, "qb_after");
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/pyrite_qvpen_buttons.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }

    static string Path(Transform t, Transform stop) { var s = t.name; while (t.parent && t != stop && t.parent != stop) { t = t.parent; s = t.name + "/" + s; } return s; }

    // 21시 · 수면 100% 흉내(침실 광원 × 0.05, 창 _Dim 0.4 — PyriteBedroomMood.Renders 와 같은 방식)에서 침대 쪽 → 펜 세트
    static void Shots(Transform room, string tag)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var lights = room.Find("Lights") ? room.Find("Lights").GetComponentsInChildren<Light>(true) : new Light[0]; var baseI = lights.Select(x => x.intensity).ToArray();
        var bd = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat"); float dim0 = bd && bd.HasProperty("_Dim") ? bd.GetFloat("_Dim") : 1f;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i] * 0.05f;
            if (bd && bd.HasProperty("_Dim")) bd.SetFloat("_Dim", 0.4f);
            cam.fieldOfView = 60f;
            var eye = room.TransformPoint(new Vector3(-0.2f, 0.95f, -1.0f)); var at = room.TransformPoint(new Vector3(3.1f, 0.8f, 1.45f));
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
            int w = 960, h = 540;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
            var a = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
            var px = tex.GetPixels32(); int bright = px.Count(q => q.r > 200 && q.g > 200 && q.b > 200);
            File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(88));
            sb.AppendLine("  shot " + tag + " (수면 100% 흉내) 흰 픽셀(>200) " + bright);
            Object.DestroyImmediate(tex);
        }
        finally
        {
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i];
            if (bd && bd.HasProperty("_Dim")) bd.SetFloat("_Dim", dim0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }
}
#endif
