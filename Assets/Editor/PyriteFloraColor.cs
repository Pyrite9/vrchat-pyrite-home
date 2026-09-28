// Tools ▸ Pyrite3 ▸ Z47a. Flower Color Sweep — 꽃이 참고 월드보다 쨍하다(관리자 2026-09-28) 원인 실측. 씬·머티리얼은 끝나면 원래대로
//  참고(관리자 스샷 픽셀, 꽃 부분): 채도 중앙값 0.72, G/B 0.71, R/B 0.31 / 원본 텍스처 위쪽 꽃: 채도 0.61, G/B 0.77, R/B 0.39
//  우리(관리자 스샷, 낮): 채도 1.00, G/B 0.42, R/B 0.09 → 빨강이 0 에 붙은 짙은 파랑
//  의심: ① 후처리 ACES + 채도(+15 바탕 볼륨) ② 라이트 프로브(벤더 머티리얼은 끔, 우리는 켬 — 파란 하늘빛을 곱함)
//        ③ 색 편차 _VarColor(0.38, 0.62, 1) 곱 ④ 구름 그림자 _ShaColor(0.57, 0.55, 1) 곱
//  변형마다 같은 시점을 찍어 꽃 픽셀(파랑 우세, 밝기 > 0.12)의 채도·G/B·R/B·밝기를 잰다 → Logs/pyrite_flowercolor.txt, Assets/_preview/flowercolor/
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class PyriteFloraColor
{
    const string LOG = "Logs/pyrite_flowercolor.txt";
    const string OUT = "Assets/_preview/flowercolor/";

    static void Kw(Material m, string prop, string kw, bool on) { m.SetFloat(prop, on ? 1f : 0f); if (on) m.EnableKeyword(kw); else m.DisableKeyword(kw); }

    [MenuItem("Tools/Pyrite3/Z47a. Flower Color Sweep", false, 170)]
    public static void Sweep() { Run(false); }

    // Z47b: 밤 원인 확인 — DayCycle 꽃 키프레임 색이 파랗게 물들어 있다(밤 0.145/0.165/0.24 × 1.8). 같은 밝기(루마)의 회색으로 바꾼 변형 + 채도 0.7
    [MenuItem("Tools/Pyrite3/Z47b. Flower Color Sweep (neutral keys)", false, 171)]
    public static void Sweep2() { Run(true); }

    static Color[] Neutral(Color[] src)
    {
        var o = new Color[src.Length];
        for (int i = 0; i < src.Length; i++) { float y = 0.2126f * src[i].r + 0.7152f * src[i].g + 0.0722f * src[i].b; o[i] = new Color(y, y, y, 1f); }
        return o;
    }

    static void Run(bool neutral)
    {
        var sb = new StringBuilder((neutral ? "[Z47b] " : "[Z47a] ") + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>(true);
        var m = cyc != null ? cyc.flowerMat : null;
        var ppl = cam.GetComponent<PostProcessLayer>();
        if (m == null) { File.AppendAllText(LOG, "!! DayCycle.flowerMat 없음\n"); return; }
        var props = new Material(m); var kws = m.shaderKeywords;
        Directory.CreateDirectory(OUT);
        sb.AppendLine(string.Format("  mat {0}: lightprobe {1} var {2} shadow {3} coladj {4} (sat {5}) mainColor {6} emiInt {7}",
            m.name, m.IsKeywordEnabled("ENABLE_LIGHTPROBE"), m.IsKeywordEnabled("ENABLE_COLOR_VARIATION"), m.IsKeywordEnabled("ENABLE_SHADOW"),
            m.IsKeywordEnabled("ENABLE_COLOR_ADJUST"), m.GetFloat("_Sat"), m.GetColor("_MainColor"), m.GetFloat("_EmiInt")));
        var views = new[]
        {
            ("down",  new Vector3(-2f, 4.10f, 62f), new Vector3(-2f - 3f * 0.6157f, 2.65f, 62f - 3f * 0.7880f)),
            ("spawn", new Vector3(-2f, 4.23f, 62f), new Vector3(-2f - 30f * 0.6157f, 2.4f, 62f - 30f * 0.7880f)),
        };
        // (이름, 적용)
        var keys0 = cyc.flowerColor; var keysN = Neutral(keys0);
        sb.AppendLine("  flower keys: " + string.Join(" ", keys0.Select(c => "(" + c.r.ToString("0.00") + "," + c.g.ToString("0.00") + "," + c.b.ToString("0.00") + ")")));
        System.Action sat07 = () => { Kw(m, "_EnaColAdj", "ENABLE_COLOR_ADJUST", true); m.SetFloat("_Sat", 0.7f); m.SetFloat("_Con", 1f); m.SetFloat("_Hue", 0f); };
        var variants2 = new List<(string n, System.Action a)>
        {
            ("current",       () => { }),
            ("neutral",       () => { cyc.flowerColor = keysN; }),
            ("neutral+sat.7", () => { cyc.flowerColor = keysN; sat07(); }),
            ("neutral+sat.6", () => { cyc.flowerColor = keysN; Kw(m, "_EnaColAdj", "ENABLE_COLOR_ADJUST", true); m.SetFloat("_Sat", 0.6f); }),
            ("sat.7",         () => sat07()),
            ("neutral+ppOff", () => { cyc.flowerColor = keysN; if (ppl != null) ppl.enabled = false; }),
        };
        var variants = neutral ? variants2 : new List<(string n, System.Action a)>
        {
            ("current",      () => { }),
            ("ppOff",        () => { if (ppl != null) ppl.enabled = false; }),
            ("probeOff",     () => Kw(m, "_EnaLgtPrb", "ENABLE_LIGHTPROBE", false)),
            ("varOff",       () => Kw(m, "_EnaVar", "ENABLE_COLOR_VARIATION", false)),
            ("shadowOff",    () => Kw(m, "_EnaSha", "ENABLE_SHADOW", false)),
            ("sat0.7",       () => { Kw(m, "_EnaColAdj", "ENABLE_COLOR_ADJUST", true); m.SetFloat("_Sat", 0.7f); m.SetFloat("_Con", 1f); m.SetFloat("_Hue", 0f); }),
            ("probe+varOff", () => { Kw(m, "_EnaLgtPrb", "ENABLE_LIGHTPROBE", false); Kw(m, "_EnaVar", "ENABLE_COLOR_VARIATION", false); }),
        };
        try
        {
            foreach (var h in neutral ? new[] { 12f, 18.33f, 21f } : new[] { 12f, 21f })
            {
                foreach (var v in views)
                    foreach (var va in variants)
                    {
                        m.CopyPropertiesFromMaterial(props); m.shaderKeywords = kws; if (ppl != null) ppl.enabled = true; cyc.flowerColor = keys0;
                        va.a();                                   // 키프레임 색을 바꾸는 변형은 EvaluateAt 전에
                        cyc.ResetCache(); cyc.EvaluateAt(h);
                        var px = Shot(cam, v.Item2, v.Item3, OUT + (neutral ? "b_" : "") + v.Item1 + "_" + h.ToString("00.00") + "_" + va.n + ".png");
                        sb.AppendLine(string.Format("  {0:00}h {1,-6} {2,-13} {3}", h, v.Item1, va.n, Stats(px)));
                    }
            }
            sb.AppendLine("  target ref (night, glow): sat 0.72 G/B 0.71 R/B 0.31 | raw texture: sat 0.61 G/B 0.77 R/B 0.39");
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            m.CopyPropertiesFromMaterial(props); m.shaderKeywords = kws; EditorUtility.SetDirty(m); Object.DestroyImmediate(props);
            cyc.flowerColor = keys0;
            if (ppl != null) ppl.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
            Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, sb.ToString());
            AssetDatabase.Refresh();
        }
    }

    static Color32[] Shot(Camera cam, Vector3 eye, Vector3 at, string path)
    {
        cam.fieldOfView = 60f; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        var px = tx.GetPixels32();
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
        return px;
    }

    // 꽃 픽셀 = 파랑 우세(B > R + 25, B ≥ G) 이고 밝기(max) > 30. 화면 아래 2/3 (ReadPixels 는 y=0 이 아래)
    static string Stats(Color32[] px)
    {
        const int W = 1280, H = 720;
        var sats = new List<float>(); var vals = new List<float>(); double sr = 0, sg = 0, sbl = 0; int n = 0;
        for (int y = 0; y < H * 2 / 3; y++)
            for (int x = 0; x < W; x++)
            {
                var c = px[y * W + x];
                int mx = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), mn = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
                if (c.b <= c.r + 25 || c.b < c.g || mx <= 30) continue;
                sats.Add((mx - mn) / (float)mx); vals.Add(mx / 255f);
                sr += c.r; sg += c.g; sbl += c.b; n++;
            }
        if (n == 0) return "no flower px";
        sats.Sort(); vals.Sort();
        return string.Format("px {0,5:0.0}%  sat {1:0.00}  val {2:0.00}  G/B {3:0.00}  R/B {4:0.00}  mean ({5:0},{6:0},{7:0})",
            100f * n / (W * H * 2 / 3f), sats[n / 2], vals[n / 2], sg / sbl, sr / sbl, sr / n, sg / n, sbl / n);
    }
}
#endif
