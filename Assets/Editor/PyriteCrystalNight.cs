// Tools ▸ Pyrite ▸ Z21a. Crystal Night Variants
//  밤 결정이 "색칠한 깍두기"(청회색·보라·적갈 상자)로 보인다 — 21:00 에서 변형 4개를 렌더(에셋은 끝나면 원래대로)
//   a 현재 / b 따뜻한 금속(색조·MatCap 약화) / c b + 림 + 달 반짝임 / d c + 미세 반짝이
//  렌더 Assets/_preview/crystal/, 로그 Logs/pyrite_crystal.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteCrystalNight
{
    struct Var { public string n; public Color tint; public float mc, mcN, rim, glint, sparkle, rimP, cells; }

    [MenuItem("Tools/Pyrite/Z21a. Crystal Night Variants", false, 49)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z21a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var mats = cyc.crystalMats.Where(m => m != null).ToArray();
        sb.AppendLine("mats: " + string.Join(", ", mats.Select(m => m.name + "(" + m.shader.name + ")")));
        var lm = Object.FindObjectsOfType<Transform>(true).Where(t => t.name.StartsWith("Landmark_")).OrderBy(t => t.name).ToArray();
        foreach (var t in lm) sb.AppendLine("  " + t.name + " " + t.position);
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var save = mats.Select(m => new { m, mcN = m.GetFloat("_MatCapNormal") }).ToArray();
        Directory.CreateDirectory("Assets/_preview/crystal/");
        var terr = Terrain.activeTerrain;
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(21f);
            float mEl = cyc.MoonEl(21f), mAz = cyc.MoonAz(21f);
            var moon = new Vector3(Mathf.Sin(mAz * Mathf.Deg2Rad) * Mathf.Cos(mEl * Mathf.Deg2Rad), Mathf.Sin(mEl * Mathf.Deg2Rad), Mathf.Cos(mAz * Mathf.Deg2Rad) * Mathf.Cos(mEl * Mathf.Deg2Rad));
            sb.AppendLine("moon dir " + moon);
            var baseTint = mats[0].GetColor("_MatCapTint"); float baseMc = mats[0].GetFloat("_MatCapStrength");
            // 2차: c(림 0.9, p3)는 결정이 등불처럼 통째로 빛남, d(반짝이 6셀/m)는 네모 조각이 보임 → 약하게·잘게
            var vars = new[]
            {
                new Var{ n="a_now",       tint=baseTint,                     mc=baseMc, mcN=-1f,  rim=0f,    glint=0f,   sparkle=0f, rimP=3f, cells=6f },
                new Var{ n="e_rimsoft",   tint=new Color(0.62f,0.52f,0.38f), mc=0.30f,  mcN=0.45f, rim=0.35f, glint=2.5f, sparkle=0f, rimP=5f, cells=6f },
                new Var{ n="f_rimsparkle",tint=new Color(0.62f,0.52f,0.38f), mc=0.30f,  mcN=0.45f, rim=0.35f, glint=2.5f, sparkle=4f, rimP=5f, cells=28f },
            };
            foreach (var v in vars)
            {
                foreach (var s in save)
                {
                    var m = s.m;
                    m.SetColor("_MatCapTint", v.tint); m.SetFloat("_MatCapStrength", v.mc);
                    m.SetFloat("_MatCapNormal", v.mcN < 0 ? s.mcN : v.mcN);
                    m.SetColor("_RimColor", new Color(1f, 0.78f, 0.42f) * v.rim); m.SetFloat("_RimPower", v.rimP);
                    m.SetVector("_GlintDir", moon); m.SetColor("_GlintColor", new Color(0.85f, 0.9f, 1f) * v.glint); m.SetFloat("_GlintSharp", 40f);
                    m.SetFloat("_SparkleStrength", v.sparkle); m.SetFloat("_SparkleScale", v.cells);
                }
                int k = 0;
                foreach (var t in lm.Take(2))
                {
                    var tgt = t.position + Vector3.up * 1.2f;
                    var eye = t.position + new Vector3(0f, 0f, 9f); if (terr != null) eye.y = terr.SampleHeight(eye) + terr.transform.position.y + 1.7f;
                    Shot(cam, eye, tgt, 60f, string.Format("Assets/_preview/crystal/{0}_near{1}.png", v.n, ++k));
                }
                // 마요(옛 구리) 결정 — 캠프에서 가장 가까운 것
                var tr = Object.FindObjectsOfType<Renderer>().Where(r => r.sharedMaterials.Any(mm => mm != null && mm.name == "M_Pyrite_Tarnish"))
                    .OrderBy(r => (r.bounds.center - new Vector3(-10f, 2f, 53f)).sqrMagnitude).FirstOrDefault();
                if (tr != null)
                {
                    var c0 = tr.bounds.center; var dir = (new Vector3(-10f, c0.y, 53f) - c0).normalized;
                    var eye3 = c0 + dir * (tr.bounds.extents.magnitude * 3.2f + 2f); if (terr != null) eye3.y = Mathf.Max(eye3.y, terr.SampleHeight(eye3) + terr.transform.position.y + 1.7f);
                    Shot(cam, eye3, c0, 55f, string.Format("Assets/_preview/crystal/{0}_mayo.png", v.n));
                    if (v.n == "a_now") sb.AppendLine("  mayo crystal " + tr.name + " at " + c0);
                }
                var e2 = new Vector3(-10f, 0f, 47f); if (terr != null) e2.y = terr.SampleHeight(e2) + terr.transform.position.y + 1.7f;
                Shot(cam, e2, new Vector3(-10f, 3f, -40f), 70f, string.Format("Assets/_preview/crystal/{0}_far.png", v.n));
                sb.AppendLine("  " + v.n);
            }
            // 마요 결정 낮·노을 (현재 머티리얼 그대로)
            foreach (var s in save)
            {
                s.m.SetFloat("_MatCapNormal", s.mcN);
                s.m.SetColor("_RimColor", Color.black); s.m.SetColor("_GlintColor", Color.black); s.m.SetFloat("_SparkleStrength", 0f);
            }
            var trD = Object.FindObjectsOfType<Renderer>().Where(r => r.sharedMaterials.Any(mm => mm != null && mm.name == "M_Pyrite_Tarnish"))
                .OrderBy(r => (r.bounds.center - new Vector3(-10f, 2f, 53f)).sqrMagnitude).FirstOrDefault();
            if (trD != null)
                foreach (var h in new[] { 12f, PyriteDayCycleSetup.EDITOR_HOUR })
                {
                    cyc.EvaluateAt(h);
                    var c0 = trD.bounds.center; var dir = (new Vector3(-10f, c0.y, 53f) - c0).normalized;
                    var eye3 = c0 + dir * (trD.bounds.extents.magnitude * 3.2f + 2f); if (terr != null) eye3.y = Mathf.Max(eye3.y, terr.SampleHeight(eye3) + terr.transform.position.y + 1.7f);
                    Shot(cam, eye3, c0, 55f, string.Format("Assets/_preview/crystal/mayo_day_{0:00}.png", (int)h));
                }
        }
        finally
        {
            foreach (var s in save)
            {
                s.m.SetFloat("_MatCapNormal", s.mcN);
                s.m.SetColor("_RimColor", Color.black); s.m.SetColor("_GlintColor", Color.black); s.m.SetFloat("_SparkleStrength", 0f);
            }
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            AssetDatabase.SaveAssets();
        }
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_crystal.txt", sb.ToString());
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 960, H = 540;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
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
}
#endif
