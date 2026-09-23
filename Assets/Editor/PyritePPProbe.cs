// Tools ▸ Pyrite ▸ Z19b. PP Render Probe
//  인게임 12:00 스크린샷은 절벽 밝기 24~43 인데 에디터 렌더는 54 — 에디터 렌더에 후처리가 빠졌는지 확인.
//   1) Main Camera 컴포넌트 / PostProcessLayer 설정 / 볼륨 레이어·프로필 내용 기록
//   2) 12:00 camp_lake 를 (a) 그대로 (b) PostProcessLayer 를 확실히 켠 임시 카메라로 렌더
//  로그 Logs/pyrite_day.txt, 렌더 Assets/_preview/day/pp_*.png
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class PyritePPProbe
{
    [MenuItem("Tools/Pyrite/Z19b. PP Render Probe", false, 46)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z19b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main;
        sb.AppendLine("Main Camera: " + string.Join(", ", cam.GetComponents<Component>().Select(c => c.GetType().Name)) + "  hdr " + cam.allowHDR);
        var ppl = cam.GetComponent<PostProcessLayer>();
        if (ppl != null) sb.AppendLine(string.Format("  PostProcessLayer enabled {0} volumeLayer {1} trigger {2} AA {3}", ppl.enabled, ppl.volumeLayer.value, ppl.volumeTrigger ? ppl.volumeTrigger.name : "null", ppl.antialiasingMode));
        foreach (var v in Object.FindObjectsOfType<PostProcessVolume>(true))
        {
            sb.AppendLine(string.Format("  volume {0} layer {1} global {2} weight {3} priority {4} profile {5}", v.name, v.gameObject.layer, v.isGlobal, v.weight, v.priority, v.sharedProfile ? AssetDatabase.GetAssetPath(v.sharedProfile) : "null"));
            if (v.sharedProfile != null)
                foreach (var s in v.sharedProfile.settings)
                {
                    sb.Append("    " + s.GetType().Name + " active " + s.active);
                    if (s is ColorGrading cg) sb.Append(string.Format(" tonemap {0} postExp {1} temp {2} tint {3} contrast {4} sat {5} lift {6} gamma {7} gain {8}", cg.tonemapper.value, cg.postExposure.value, cg.temperature.value, cg.tint.value, cg.contrast.value, cg.saturation.value, cg.lift.value, cg.gamma.value, cg.gain.value));
                    if (s is Bloom b) sb.Append(string.Format(" int {0} thr {1}", b.intensity.value, b.threshold.value));
                    sb.AppendLine();
                }
        }

        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        GameObject tmp = null;
        try
        {
            if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            var t = Terrain.activeTerrain;
            var eye = new Vector3(-10f, 1.7f, 47f); if (t != null) eye.y += Mathf.Max(0f, t.SampleHeight(eye) + t.transform.position.y);
            var look = new Vector3(-10f, 3f, -40f);
            Directory.CreateDirectory("Assets/_preview/day/");
            var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
            Shot(cam, eye, look, "Assets/_preview/day/pp_a_maincam.png");
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0;

            tmp = new GameObject("__ppcam");
            var c2 = tmp.AddComponent<Camera>(); c2.CopyFrom(cam); c2.allowHDR = true;
            var l2 = tmp.AddComponent<PostProcessLayer>();
            l2.volumeTrigger = tmp.transform;
            l2.volumeLayer = ppl != null ? ppl.volumeLayer : ~0;
            var res = ppl != null ? typeof(PostProcessLayer).GetField("m_Resources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(ppl) : null;
            if (res != null) l2.Init((PostProcessResources)res);
            Shot(c2, eye, look, "Assets/_preview/day/pp_b_ppcam.png");
        }
        finally
        {
            if (tmp != null) Object.DestroyImmediate(tmp);
            foreach (var r in psr) r.enabled = true;
            cam.targetTexture = null;
            if (cyc != null) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_day.txt", sb.ToString());
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 70f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
