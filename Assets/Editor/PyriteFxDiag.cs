// Tools ▸ Pyrite2 ▸ Z34e. FX Diag (Ctrl+Alt+Shift+7) — 물안개·별똥별이 렌더에 안 보이는 원인 실측. 씬은 바꾸지 않는다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteFxDiag
{
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite2/Z34e. FX Diag %&#7", false, 84)]
    public static void Run()
    {
        sb = new StringBuilder(); sb.AppendLine("=== FX Diag " + System.DateTime.Now.ToString("HH:mm:ss") + " ===");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        File.WriteAllText("Logs/pyrite_fxdiag.txt", sb.ToString()); AssetDatabase.Refresh(); Debug.Log(sb.ToString());
    }

    static void Inner()
    {
        var root = GameObject.Find("AmbientFX"); if (root == null) { sb.AppendLine("AmbientFX 없음"); return; }
        var cam = Camera.main;
        sb.AppendLine("Camera.main " + cam.name + " near " + cam.nearClipPlane + " far " + cam.farClipPlane + " clear " + cam.clearFlags + " hdr " + cam.allowHDR + " cullMask " + cam.cullingMask);
        foreach (var d in Object.FindObjectsOfType<VRC.SDK3.Components.VRCSceneDescriptor>())
        {
            var rc = d.ReferenceCamera != null ? d.ReferenceCamera.GetComponent<Camera>() : null;
            sb.AppendLine("SceneDescriptor refCam " + (rc != null ? rc.name + " near " + rc.nearClipPlane + " far " + rc.farClipPlane : "none"));
        }
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView, far0 = cam.farClipPlane;

        // 물안개
        var mistPs = root.transform.Find("LakeMist").GetComponent<ParticleSystem>();
        var mistR = mistPs.GetComponent<ParticleSystemRenderer>(); var mat = mistR.sharedMaterial;
        var c0 = mat.GetColor("_Color"); float a0 = mat.GetFloat("_Alpha");
        sb.AppendLine("mist layer " + LayerMask.LayerToName(mistPs.gameObject.layer) + " inCull " + ((cam.cullingMask >> mistPs.gameObject.layer) & 1) + " shader " + mat.shader.name + " supported " + mat.shader.isSupported + " tex " + (mat.mainTexture ? mat.mainTexture.name : "null") + " queue " + mat.renderQueue);
        mistPs.Simulate(90f, true, true);
        var parts = new ParticleSystem.Particle[mistPs.main.maxParticles]; int n = mistPs.GetParticles(parts);
        sb.AppendLine("mist particles " + n + " isPlaying " + mistPs.isPlaying + " isPaused " + mistPs.isPaused);
        if (n > 0)
        {
            var ps = parts.Take(n).ToArray();
            sb.AppendLine(string.Format("  pos x {0:0.0}..{1:0.0} y {2:0.00}..{3:0.00} z {4:0.0}..{5:0.0}", ps.Min(p => p.position.x), ps.Max(p => p.position.x), ps.Min(p => p.position.y), ps.Max(p => p.position.y), ps.Min(p => p.position.z), ps.Max(p => p.position.z)));
            sb.AppendLine(string.Format("  size {0:0.0}..{1:0.0} col.a {2:0.00}..{3:0.00}", ps.Min(p => p.GetCurrentSize(mistPs)), ps.Max(p => p.GetCurrentSize(mistPs)), ps.Min(p => p.GetCurrentColor(mistPs).a / 255f), ps.Max(p => p.GetCurrentColor(mistPs).a / 255f)));
        }
        mistR.enabled = true;
        sb.AppendLine("  renderer bounds " + mistR.bounds);
        cyc.ResetCache(); cyc.EvaluateAt(6.6f);
        mat.SetColor("_Color", new Color(1f, 0f, 1f, 1f)); mat.SetFloat("_Alpha", 1f);
        Shot(cam, new Vector3(-10.6f, 3.4f, 55f), new Vector3(-6f, 0.5f, 10f), 60f, "diag_mist_magenta.png");
        Shot(cam, new Vector3(0f, 40f, 30f), new Vector3(0f, 0f, -14f), 60f, "diag_mist_top.png");
        mat.SetColor("_Color", c0); mat.SetFloat("_Alpha", a0); mistR.enabled = false; mistPs.Clear();

        // 별똥별
        var met = root.GetComponentInChildren<PyriteMeteors>();
        var head = root.transform.Find("Meteors/Head"); var tr = head.GetComponent<TrailRenderer>();
        float w0 = tr.widthMultiplier; bool em0 = tr.emitting;
        sb.AppendLine("meteor layer " + LayerMask.LayerToName(head.gameObject.layer) + " shader " + tr.sharedMaterial.shader.name + " supported " + tr.sharedMaterial.shader.isSupported);
        cyc.ResetCache(); cyc.EvaluateAt(22f);
        head.gameObject.SetActive(true);
        System.Func<float, float, Vector3> D = (el, az) => { float e = el * Mathf.Deg2Rad, a2 = az * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a2) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a2) * Mathf.Cos(e)); };
        var pts = Enumerable.Range(0, 24).Select(i => met.center + Vector3.Slerp(D(52f, 190f), D(44f, 204f), i / 23f) * met.radius).ToArray();
        var eye = new Vector3(-10.6f, 3.4f, 53.8f);
        tr.Clear(); tr.AddPositions(pts); head.position = pts[pts.Length - 1];
        sb.AppendLine("trail positionCount " + tr.positionCount + " dist eye→pt0 " + Vector3.Distance(eye, pts[0]).ToString("0") + " m, bounds " + tr.bounds);
        Shot(cam, eye, eye + D(45f, 198f), 60f, "diag_meteor_asis.png");
        tr.widthMultiplier = 30f; tr.Clear(); tr.AddPositions(pts);
        Shot(cam, eye, eye + D(45f, 198f), 60f, "diag_meteor_w30.png");
        cam.farClipPlane = 3000f; tr.Clear(); tr.AddPositions(pts);
        Shot(cam, eye, eye + D(45f, 198f), 60f, "diag_meteor_w30_far3000.png");
        tr.widthMultiplier = w0; tr.emitting = true; tr.Clear(); tr.AddPositions(pts);
        Shot(cam, eye, eye + D(45f, 198f), 60f, "diag_meteor_far3000.png");
        sb.AppendLine("trail positionCount(after far) " + tr.positionCount);
        tr.emitting = em0; tr.widthMultiplier = w0; tr.Clear(); head.gameObject.SetActive(false);

        // 지평선(절벽 윗선) 앙각 — 방위 5° 마다. 별똥별을 어디에 그을지 정하려고
        Physics.SyncTransforms();
        foreach (var (label, e0) in new[] { ("camp", new Vector3(-10.5f, 3.6f, 53f)), ("dock", new Vector3(-9.4f, 2.1f, 31f)), ("lakeC", new Vector3(0f, 1.7f, -14f)) })
        {
            var line = new StringBuilder("horizon " + label + " " + e0 + ":");
            var hitNames = new System.Collections.Generic.HashSet<string>();
            for (int az = 0; az < 360; az += 10)
            {
                float top = -1f;
                for (float el = 0f; el <= 70f; el += 0.5f)
                {
                    RaycastHit hit;
                    if (Physics.Raycast(e0, D(el, az), out hit, 2000f, ~0, QueryTriggerInteraction.Ignore) && hit.distance > 25f) { top = el; if (hitNames.Count < 12) hitNames.Add(hit.collider.name); }
                }
                line.Append(" " + az + ":" + top.ToString("0.0"));
            }
            sb.AppendLine(line.ToString());
            sb.AppendLine("  hit colliders " + string.Join(", ", hitNames));
        }

        cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.farClipPlane = far0; cam.targetTexture = null;
        cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        sb.AppendLine("  shots fx/diag_*");
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string name)
    {
        cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        const int W = 960, H = 540;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        Directory.CreateDirectory("Assets/_preview/fx/"); File.WriteAllBytes("Assets/_preview/fx/" + name, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
