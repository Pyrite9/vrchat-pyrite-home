// PyriteBedroomLight.cs — 텐트 침실 조명 보정 (Z49o 적용 / Z49p 되돌리기). 재실행 안전
//  실측(Z49m/Z49n, 21시): 방 조명 전부 꺼도 PP 끈 화면 평균 20.7 = 월드 라이트 프로브(캠프 쪽에서 외삽된 회색 SH)가 방을 채움
//                          밤 PP(온도 -6, lift 파랑 1.08, ACES) 가 랜턴 빛을 식혀 바닥이 청흑(RGB 32/20/20)
//  보정 1: 침실 렌더러 lightProbeUsage Off → 환경광은 밤 하늘 SH(아주 어두운 파랑)만
//  보정 2: 침실 전용 로컬 PP 볼륨(박스 트리거, 레이어 PostProcessing, priority 5) — 시간대 볼륨(0~2) 위, 사용자 볼륨(10) 아래
//  보정 3: 걸이 랜턴 1.4/6.5 → 2.4/8 m, 천장 반사광(Fill) 추가 0.45/7.5 m, 촛불 0.55/2.8 → 0.9/3.2 m. 색은 덜 주황(캠프 원본 불빛 b 0.31 → 0.60)
//  1차(온도 +6, gain 따뜻) 는 파랑 채널이 3~5 로 무너져 전부 주황 단색 → 온도 0, lift 약한 파랑, 채도 -5
//  보정 4: 바닥 재질 밝게, 담요 선·가장자리 완화 (되돌리기 Z49p 는 재질은 안 건드림)
//  ⚠ Z49i(v3 재빌드) 뒤에는 Z49o 다시
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class PyriteBedroomLight
{
    const string DIR = "Assets/Bedroom";
    const string PROFILE = DIR + "/PP_TentBedroom.asset";
    const string PREV = "Assets/_preview/bedroom/";
    const string LOG = "Logs/pyrite_bedroom_light.txt";
    const float HANG_I = 2.4f, HANG_R = 8f, FILL_I = 0.45f, FILL_R = 7.5f, CANDLE_I = 0.9f, CANDLE_R = 3.2f;
    static readonly Color HANG_C = new Color(1f, 0.82f, 0.60f), FILL_C = new Color(1f, 0.86f, 0.72f), CANDLE_C = new Color(1f, 0.78f, 0.55f);
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite3/Z49o. Bedroom Light Apply", false, 4914)]
    public static void Apply()
    {
        sb = new StringBuilder("[Z49o] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49p. Bedroom Light Revert", false, 4915)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49p] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom");
        if (room)
        {
            var o = room.transform;
            foreach (var r in room.GetComponentsInChildren<Renderer>(true)) if (!(r is ParticleSystemRenderer)) r.lightProbeUsage = LightProbeUsage.BlendProbes;
            var v = o.Find("BedroomPP"); if (v) Object.DestroyImmediate(v.gameObject);
            var f = o.Find("Lights/Fill"); if (f) Object.DestroyImmediate(f.gameObject);
            foreach (var l in room.GetComponentsInChildren<Light>(true))
            {
                if (l.transform.IsChildOf(o.Find("Lights/HangLantern") ?? o)) { l.intensity = 1.4f; l.range = 6.5f; l.color = new Color(1f, 0.743f, 0.307f); }
                else if (l.transform.parent && l.transform.parent.name.StartsWith("CandleLantern")) { l.intensity = 0.55f; l.range = 2.8f; l.color = new Color(1f, 0.72f, 0.42f); }
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
            sb.AppendLine("프로브 BlendProbes 복구, BedroomPP·Fill 삭제, 광원 원래 값");
        }
        sb.AppendLine("RESULT: DONE");
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    static void Inner()
    {
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var o = room.transform;
        var lightsT = o.Find("Lights"); if (lightsT == null) { sb.AppendLine("!! Lights 없음 → Z49i 먼저"); return; }
        var hang = o.Find("Lights/HangLantern");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled && !r.transform.IsChildOf(o)).ToArray();
        foreach (var r in psr) r.enabled = false;
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 75f;
            Shots(cam, o, "s0");

            // 1) 프로브 끔
            int np = 0;
            foreach (var r in room.GetComponentsInChildren<Renderer>(true))
                if (!(r is ParticleSystemRenderer) && r.gameObject.layer == PyriteBedroomV3.LAYER && r.transform.parent?.name != "Backdrop" && r.name != "Backdrop")
                { r.lightProbeUsage = LightProbeUsage.Off; np++; }
            sb.AppendLine("1) lightProbeUsage Off: 렌더러 " + np);
            Shots(cam, o, "s1");

            // 2) 로컬 PP 볼륨
            var prof = BuildProfile();
            var vt = o.Find("BedroomPP"); if (vt) Object.DestroyImmediate(vt.gameObject);
            var vg = new GameObject("BedroomPP"); vg.transform.SetParent(o, false);
            vg.layer = LayerMask.NameToLayer("PostProcessing") >= 0 ? LayerMask.NameToLayer("PostProcessing") : 23;
            vg.transform.localPosition = new Vector3(0, 1.7f, 0);
            var bc = vg.AddComponent<BoxCollider>(); bc.isTrigger = true; bc.size = new Vector3(PyriteBedroomBuild.A * 2f + 0.6f, 3.6f, PyriteBedroomBuild.B * 2f + 0.6f);
            var vol = vg.AddComponent<PostProcessVolume>(); vol.isGlobal = false; vol.priority = 5; vol.blendDistance = 0.3f; vol.weight = 1f; vol.sharedProfile = prof;
            sb.AppendLine("2) BedroomPP: 로컬 볼륨 layer " + vg.layer + " prio 5 박스 " + bc.size.ToString("F1") + " profile " + PROFILE);
            Shots(cam, o, "s2");

            // 3) 광원
            foreach (var l in room.GetComponentsInChildren<Light>(true))
            {
                if (hang && l.transform.IsChildOf(hang)) { l.intensity = HANG_I; l.range = HANG_R; l.color = HANG_C; }
                else if (l.transform.parent && l.transform.parent.name.StartsWith("CandleLantern")) { l.intensity = CANDLE_I; l.range = CANDLE_R; l.color = CANDLE_C; }
                EditorUtility.SetDirty(l);
            }
            var ft = lightsT.Find("Fill"); if (ft) Object.DestroyImmediate(ft.gameObject);
            var fg = new GameObject("Fill"); fg.transform.SetParent(lightsT, false); fg.transform.localPosition = new Vector3(0f, 2.9f, 0.2f);
            var fl = fg.AddComponent<Light>(); fl.type = LightType.Point; fl.color = FILL_C; fl.intensity = FILL_I; fl.range = FILL_R;
            fl.shadows = LightShadows.None; fl.lightmapBakeType = LightmapBakeType.Realtime; fl.renderMode = LightRenderMode.Auto;
            sb.AppendLine("3) 걸이 " + HANG_I + "/" + HANG_R + " m, 촛불 " + CANDLE_I + "/" + CANDLE_R + " m, Fill " + FILL_I + "/" + FILL_R + " m (y 2.9, 그림자 없음)");
            // 4) 재질: 바닥 밝게(청흑 공백처럼 보임), 담요 선 덜 튀게·몸 둘레 완만하게
            var mf = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_TentFloor.mat");
            if (mf) { mf.SetColor("_Color", new Color(0.40f, 0.36f, 0.30f)); EditorUtility.SetDirty(mf); }
            var mb = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/M_BlanketCover.mat");
            if (mb) { mb.SetFloat("_Skirt", 0.20f); mb.SetColor("_ColA", new Color(0.46f, 0.13f, 0.11f)); mb.SetColor("_ColC", new Color(0.74f, 0.64f, 0.44f)); EditorUtility.SetDirty(mb); }
            AssetDatabase.SaveAssets();
            sb.AppendLine("4) 바닥 (0.22,0.20,0.17)→(0.40,0.36,0.30), 담요 _Skirt 0.14→0.20, 선색 (0.86,0.76,0.52)→(0.74,0.64,0.44) " + (mf != null) + "/" + (mb != null));
            Shots(cam, o, "s3");
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    static PostProcessProfile BuildProfile()
    {
        if (AssetDatabase.LoadAssetAtPath<PostProcessProfile>(PROFILE) != null) AssetDatabase.DeleteAsset(PROFILE);
        var p = ScriptableObject.CreateInstance<PostProcessProfile>();
        AssetDatabase.CreateAsset(p, PROFILE);
        var bl = p.AddSettings<Bloom>(); bl.enabled.Override(true);
        bl.intensity.Override(1.2f); bl.threshold.Override(0.9f); bl.softKnee.Override(0.6f); bl.diffusion.Override(7f); bl.color.Override(new Color(1f, 0.88f, 0.72f));
        var cg = p.AddSettings<ColorGrading>(); cg.enabled.Override(true);
        cg.gradingMode.Override(GradingMode.HighDefinitionRange); cg.tonemapper.Override(Tonemapper.ACES);
        cg.temperature.Override(0f); cg.tint.Override(0f); cg.saturation.Override(-5f); cg.postExposure.Override(1.3f); cg.contrast.Override(6f);
        cg.lift.Override(new Vector4(0.98f, 1f, 1.05f, 0f)); cg.gamma.Override(new Vector4(1f, 1f, 1f, 0f)); cg.gain.Override(new Vector4(1f, 1f, 1f, 0f));
        var vi = p.AddSettings<Vignette>(); vi.enabled.Override(true);
        vi.mode.Override(VignetteMode.Classic); vi.color.Override(Color.black); vi.intensity.Override(0.30f); vi.smoothness.Override(0.4f);
        foreach (var s in p.settings) { s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy; AssetDatabase.AddObjectToAsset(s, p); }
        EditorUtility.SetDirty(p); AssetDatabase.SaveAssets();
        return p;
    }

    static void Shots(Camera cam, Transform o, string st)
    {
        Shot(cam, o, new Vector3(1.9f, 1.75f, 2.1f), new Vector3(-0.3f, 0.3f, -1.3f), "lt_" + st + "_room");
        Shot(cam, o, new Vector3(-0.28f, 0.42f, -2.05f), new Vector3(0f, 1.05f, PyriteBedroomBuild.B), "lt_" + st + "_lie");
        Shot(cam, o, new Vector3(2.4f, 0.5f, 0.5f), new Vector3(2.4f, 0f, 1.5f), "lt_" + st + "_floor");
    }

    static void Shot(Camera cam, Transform o, Vector3 e, Vector3 a2, string tag)
    {
        var eye = o.TransformPoint(e); var at = o.TransformPoint(a2);
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double r = 0, g = 0, b = 0;
        foreach (var p in px) { r += p.r; g += p.g; b += p.b; }
        int n = px.Length;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(82));
        sb.AppendLine("  shot " + tag + " mean " + ((r + g + b) / 3 / n).ToString("F1") + " RGB " + (r / n).ToString("F1") + "/" + (g / n).ToString("F1") + "/" + (b / n).ToString("F1"));
        Object.DestroyImmediate(tex);
    }
}
#endif
