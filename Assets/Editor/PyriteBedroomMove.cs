// PyriteBedroomMove.cs — 텐트 침실을 (2000, 0, 0) → 상공 (0, 1200, 0) 으로 옮김 (Z55b) / 되돌림 (Z55c). 재실행 안전
//  2026-10-05 관리자: 머리맡 슬라이더가 침실에서만 튐. Z55a 실측: 떨림은 패널 가로축 방향 좌표 크기를 따라간다
//   (2000,0,0) 머리맡 가로 13.8 mm → (0,1200,0) 0.01 mm. 소리·보임·월드 렌더는 그대로 0
//  침실 438 오브젝트 전부 정적 플래그 없음 · 텔레포트(Spawn)·창밖 카메라·빈백·이불 전부 침실 Transform 기준 → 루트 위치만 바꾼다
//  PyriteBedroomBuild.ORIGIN 도 같은 값(Z49b 를 다시 돌려도 새 자리). Z55c 로 되돌리면 ORIGIN 도 손으로 되돌릴 것
//  결과 Logs/pyrite_bedroom_move.txt, 렌더 Assets/_preview/bedroom/move_*.jpg
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomMove
{
    static readonly Vector3 OLD = new Vector3(2000f, 0f, 0f);
    static readonly Vector3 NEW = new Vector3(0f, 1200f, 0f);
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;
    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    [MenuItem("Tools/Pyrite4/Z55b. Bedroom Move To Sky (0,1200,0)", false, 6102)]
    public static void Move() { Do("Z55b", NEW); }

    [MenuItem("Tools/Pyrite4/Z55c. Bedroom Move Revert (2000,0,0)", false, 6103)]
    public static void Revert() { Do("Z55c", OLD); }

    static void Do(string tag, Vector3 to)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var temps = new List<Object>();
        try { Inner(tag, to, temps); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally { foreach (var t in temps) if (t != null) Object.DestroyImmediate(t); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_move.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    static void Inner(string tag, Vector3 to, List<Object> temps)
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var rt = room.transform;
        // 안전 검사: 정적 플래그가 있으면 중단 (오클루전·배칭이 옛 자리에 묶임)
        int statics = 0;
        foreach (var t in room.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0) statics++;
        sb.AppendLine("침실 오브젝트 " + room.GetComponentsInChildren<Transform>(true).Length + "개, 정적 플래그 " + statics + "개");
        if (statics > 0) { sb.AppendLine("!! 정적 오브젝트가 있어 중단"); return; }

        var panel = rt.Find("BedroomPanel/PanelCanvas");
        var spawn = rt.Find("Spawn");
        Vector3 from = rt.position;
        sb.AppendLine("이동 전 " + from.ToString("F2") + (spawn ? ", Spawn " + spawn.position.ToString("F2") : ""));

        // 임시 카메라
        var camGo = new GameObject("__moveCam"); temps.Add(camGo); camGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 60f; cam.farClipPlane = 1000f;
        var rtBig = new RenderTexture(1920, 1080, 24); temps.Add(rtBig);
        var rtSmall = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); temps.Add(rtSmall);
        var texA = new Texture2D(480, 270, TextureFormat.RGB24, false); temps.Add(texA);
        var texB = new Texture2D(480, 270, TextureFormat.RGB24, false); temps.Add(texB);

        if (panel != null)
        {
            Vector3 pl = rt.InverseTransformPoint(panel.position); Quaternion pr = Quaternion.Inverse(rt.rotation) * panel.rotation;
            Vector2 j0 = PyriteBedroomMoveProbe.Jitter(cam, rtBig, from, rt.rotation, pl, pr, 0.02f);
            sb.AppendLine("이동 전 머리맡 포인터 떨림(near 0.02) 가로 " + j0.x.ToString("F3") + " · 세로 " + j0.y.ToString("F3") + " mm");
        }

        Undo.RecordObject(rt, tag + " bedroom move");
        rt.position = to;
        EditorUtility.SetDirty(rt);
        Physics.SyncTransforms();
        sb.AppendLine("이동 후 " + rt.position.ToString("F2") + (spawn ? ", Spawn " + spawn.position.ToString("F2") : ""));

        if (panel != null)
        {
            Vector3 pl = rt.InverseTransformPoint(panel.position); Quaternion pr = Quaternion.Inverse(rt.rotation) * panel.rotation;
            foreach (float near in new float[] { 0.05f, 0.02f, 0.01f })
            {
                Vector2 j1 = PyriteBedroomMoveProbe.Jitter(cam, rtBig, rt.position, rt.rotation, pl, pr, near);
                Vector2 jt = PyriteBedroomMoveProbe.Jitter(cam, rtBig, rt.position, rt.rotation, new Vector3(-2.66f, 0.55f, -0.90f), Quaternion.LookRotation(Vector3.left, Vector3.up), near);
                sb.AppendLine("이동 후 떨림 near " + near.ToString("F2") + ": 머리맡 가로 " + j1.x.ToString("F3") + " · 세로 " + j1.y.ToString("F3") + " mm | TV 벽 가로 " + jt.x.ToString("F3") + " · 세로 " + jt.y.ToString("F3") + " mm");
            }
        }

        // 월드 시점에서 침실이 보이는지: 침실 켬/끔 두 장의 다른 픽셀 수
        bool fog0 = RenderSettings.fog;
        bool act0 = room.activeSelf;
        try
        {
            cam.targetTexture = rtSmall; cam.nearClipPlane = 0.05f; cam.cullingMask = ~0; cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false;
            var terr = Terrain.activeTerrain;
            var vn = new List<string>(); var vp = new List<Vector3>();
            var sp = Root("VRCWorld"); if (sp) { vn.Add("스폰"); vp.Add(sp.transform.position + Vector3.up * 1.6f); }
            vn.Add("캠프"); vp.Add(new Vector3(-10f, (terr ? terr.SampleHeight(new Vector3(-10f, 0f, 53.5f)) + terr.transform.position.y : 1.8f) + 1.6f, 53.5f));
            vn.Add("부두 끝"); vp.Add(new Vector3(-10f, 2.1f, 31.5f));
            vn.Add("호수 가운데"); vp.Add(new Vector3(0f, 1.6f, -14f));
            Vector3 center = rt.position + new Vector3(0f, 1.6f, 0f);
            int worst = 0; string worstAt = "-";
            for (int i = 0; i < vp.Count; i++)
            {
                cam.transform.SetPositionAndRotation(vp[i], Quaternion.LookRotation(center - vp[i], Vector3.forward));
                room.SetActive(true); Grab(cam, rtSmall, texA);
                room.SetActive(false); Grab(cam, rtSmall, texB);
                var a = texA.GetPixels32(); var b = texB.GetPixels32(); int n = 0;
                for (int k = 0; k < a.Length; k++) if (Mathf.Abs(a[k].r - b[k].r) + Mathf.Abs(a[k].g - b[k].g) + Mathf.Abs(a[k].b - b[k].b) > 12) n++;
                sb.AppendLine("  월드 시점 " + vn[i] + " → 침실 방향: 침실 켬/끔 차이 " + n + " px / 129600, 거리 " + Vector3.Distance(vp[i], center).ToString("F0") + " m");
                if (n > worst) { worst = n; worstAt = vn[i]; }
            }
            room.SetActive(act0);
            sb.AppendLine("월드에서 보이는 최대 " + worst + " px (" + worstAt + ")");
            // 침실 안 렌더 2장 (머리맡 패널 쪽 / 창 쪽) — 옮긴 뒤에도 같은 모습인지 눈으로 확인
            Directory.CreateDirectory(PREV);
            var rtShot = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); temps.Add(rtShot);
            var texS = new Texture2D(960, 540, TextureFormat.RGB24, false); temps.Add(texS);
            cam.targetTexture = rtShot; cam.nearClipPlane = 0.02f;
            Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(1.2f, 1.5f, 2.0f)), rt.TransformPoint(new Vector3(-0.3f, 0.6f, -2.2f)), "move_" + tag + "_room");
            Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(0.3f, 1.3f, -1.6f)), rt.TransformPoint(new Vector3(0f, 1.2f, 2.6f)), "move_" + tag + "_window");
            cam.targetTexture = null;
        }
        finally { room.SetActive(act0); RenderSettings.fog = fog0; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (되돌리기 = " + (tag == "Z55b" ? "Z55c" : "Z55b") + ")");
    }

    static void Grab(Camera cam, RenderTexture rt, Texture2D tex)
    {
        cam.Render();
        var act = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = act;
    }

    static void Shot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 eye, Vector3 at, string name)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        Grab(cam, rt, tex);
        File.WriteAllBytes(PREV + name + ".jpg", tex.EncodeToJPG(85));
        var px = tex.GetPixels32(); double sum = 0; foreach (var q in px) sum += q.r + q.g + q.b;
        sb.AppendLine("  shot " + name + " 평균 밝기 " + (sum / px.Length / 3).ToString("F1"));
    }
}
#endif
