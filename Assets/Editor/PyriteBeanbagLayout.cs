// PyriteBeanbagLayout.cs — 빈백 2개를 V 자로 (Z52g 적용 / Z52h 되돌림). 재실행 안전
//  2026-09-30 관리자: "빈백 배치를 좀 수정 — 어느 정도 마주본다던가"
//  원래(Z51l): Beanbag_1 (−1.05, 0.95) · Beanbag_2 (−0.05, 1.30), 둘 다 TV(−2.67, 1.20, −0.90) 정면
//  바꿈: 두 빈백의 가운데 MID 에서 TV 쪽 선을 축으로 양옆 SEP 씩 벌리고, 각자 TV 방향에서 상대 쪽으로 INWARD° 돌림
//   → TV 쪽으로 열린 V (서로 비스듬히 마주보면서 TV 도 보임). 빈백 트랜스폼만 옮김 — 앉기 SitPoint·ExitPoint·판정은 자식이라 같이 감
//  ⚠ Z51l(빈백 재빌드)은 원래 자리로 되돌린다 → 그 뒤 Z52g 다시
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBeanbagLayout
{
    static readonly Vector3 TV = new Vector3(-2.67f, 1.20f, -0.90f);
    static readonly Vector3[] OLD = { new Vector3(-1.05f, 0f, 0.95f), new Vector3(-0.05f, 0f, 1.30f) };
    static readonly Vector3 MID = new Vector3(-0.45f, 0f, 1.10f);   // 러그(x ±1.18, z 0.30~1.90) 가운데보다 TV 쪽
    const float SEP = 0.72f;      // 가운데에서 양옆으로 (중심 간 1.44 m)
    const float INWARD = 35f;     // TV 정면에서 상대 쪽으로 돌리는 각
    const float BAG_R = 0.60f;    // 빈백 반경(Z51q bounds 1.19 m 폭)
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z52g. Beanbag Layout Apply", false, 5133)]
    public static void Apply() => Run("Z52g", true);

    [MenuItem("Tools/Pyrite3/Z52h. Beanbag Layout Revert", false, 5134)]
    public static void Revert() => Run("Z52h", false);

    static void Run(string tag, bool apply)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
            var b1 = room ? room.transform.Find("Beanbags/Beanbag_1") : null; var b2 = room ? room.transform.Find("Beanbags/Beanbag_2") : null;
            if (b1 == null || b2 == null) { sb.AppendLine("!! Beanbags/Beanbag_1·2 없음"); return; }
            var bags = new[] { b1, b2 };
            if (apply) { Shots(room.transform, bags, "bl_before"); CopyTop("bl_before_top"); }

            Vector3[] pos; Vector3[] fwd = new Vector3[2];
            if (apply)
            {
                var d = TV - MID; d.y = 0f; d.Normalize();
                var p = new Vector3(d.z, 0f, -d.x);                    // TV 선에 수직
                // Beanbag_1 은 원래 TV 에서 먼 쪽(−x·−z 쪽에 가까운 쪽) 그대로 두려고, 원래 위치와 가까운 쪽을 고름
                var a = MID + p * SEP; var b = MID - p * SEP;
                pos = (OLD[0] - a).sqrMagnitude + (OLD[1] - b).sqrMagnitude <= (OLD[0] - b).sqrMagnitude + (OLD[1] - a).sqrMagnitude ? new[] { a, b } : new[] { b, a };
                for (int i = 0; i < 2; i++)
                {
                    var toTV = TV - pos[i]; toTV.y = 0f; toTV.Normalize();
                    var toOther = pos[1 - i] - pos[i]; toOther.y = 0f; toOther.Normalize();
                    fwd[i] = Vector3.RotateTowards(toTV, toOther, INWARD * Mathf.Deg2Rad, 0f);
                }
            }
            else
            {
                pos = OLD;
                for (int i = 0; i < 2; i++) { var t = TV - pos[i]; t.y = 0f; fwd[i] = t.normalized; }
            }
            for (int i = 0; i < 2; i++)
            {
                Undo.RecordObject(bags[i], tag);
                sb.AppendLine(bags[i].name + " " + V(bags[i].localPosition) + " yaw " + bags[i].localEulerAngles.y.ToString("F0") + " → " + V(pos[i]) + " yaw " + Quaternion.LookRotation(fwd[i]).eulerAngles.y.ToString("F0"));
                bags[i].localPosition = pos[i]; bags[i].localRotation = Quaternion.LookRotation(fwd[i], Vector3.up);
            }
            // 점검: 서로 틈 · TV 가 정면에서 몇 도 · 마주보는 정도 · 침대·러그·낮은 테이블 · ExitPoint 가 침대 위인지
            float gap = (pos[0] - pos[1]).magnitude - 2f * BAG_R;
            sb.AppendLine("중심 간 " + (pos[0] - pos[1]).magnitude.ToString("F2") + " m · 틈(반경 " + BAG_R + " 가정) " + gap.ToString("F2") + " m");
            for (int i = 0; i < 2; i++)
            {
                var t = TV - pos[i]; t.y = 0f; var o = pos[1 - i] - pos[i]; o.y = 0f;
                float tvOff = Vector3.Angle(fwd[i], t), face = Vector3.Angle(fwd[i], o);
                var ex = bags[i].Find("Seat/ExitPoint"); var exL = ex ? room.transform.InverseTransformPoint(ex.position) : Vector3.zero;
                bool exOnBed = exL.x > -1.13f && exL.x < 1.14f && exL.z > -2.40f && exL.z < -0.20f;
                sb.AppendLine("  " + bags[i].name + ": TV 가 정면에서 " + tvOff.ToString("F0") + "° · 상대 빈백이 정면에서 " + face.ToString("F0") + "° · TV 까지 " + t.magnitude.ToString("F2") + " m"
                              + " · 침대 앞끝(z −0.20)까지 " + (pos[i].z - BAG_R + 0.20f).ToString("F2") + " m · 러그 안 " + (Mathf.Abs(pos[i].x) < 1.18f && pos[i].z > 0.30f && pos[i].z < 1.90f)
                              + " · ExitPoint " + V(exL) + (exOnBed ? " 🔴 침대 위" : ""));
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            if (apply) { Shots(room.transform, bags, "bl_after"); CopyTop("bl_after_top"); }
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/pyrite_beanbag_layout.txt", sb.ToString(), new UTF8Encoding(false));
        }
    }

    static void CopyTop(string name)
    {
        try { PyriteBedroomLayoutProbe.Run(); File.Copy(PREV + "lay_top.jpg", PREV + name + ".jpg", true); sb.AppendLine("  top " + name); }
        catch (System.Exception e) { sb.AppendLine("  (위에서 본 렌더 실패 " + e.Message + ")"); }
    }

    // 방 전경 + 빈백마다 앉은 눈높이에서 정면 (TV 켠 상태)
    static void Shots(Transform room, Transform[] bags, string tag)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tvT = room.Find("BedroomPanel/BedroomTV"); bool tv0 = tvT && tvT.gameObject.activeSelf;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            if (tvT) tvT.gameObject.SetActive(true);
            cam.fieldOfView = 55f;
            Shot(cam, room.TransformPoint(new Vector3(1.7f, 1.7f, 2.4f)), room.TransformPoint(new Vector3(-0.9f, 0.3f, 0.6f)), tag + "_room");
            Shot(cam, room.TransformPoint(new Vector3(-2.3f, 1.4f, -0.4f)), room.TransformPoint(new Vector3(-0.4f, 0.3f, 1.1f)), tag + "_tvside");
            cam.fieldOfView = 60f;
            for (int i = 0; i < bags.Length; i++)
            {
                var eye = bags[i].TransformPoint(new Vector3(0f, 0.95f, -0.12f));
                Shot(cam, eye, eye + bags[i].forward * 2f + Vector3.down * 0.15f, tag + "_eye" + (i + 1));
            }
        }
        finally
        {
            if (tvT) tvT.gameObject.SetActive(tv0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
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

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
}
#endif
