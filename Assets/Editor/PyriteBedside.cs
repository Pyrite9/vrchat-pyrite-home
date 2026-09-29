// PyriteBedside.cs — 침대 양옆 가구 (Z51r 빌드 / Z51s 되돌림 / Z51t 렌더). 재실행 안전
//  2026-09-30 관리자: 발치는 비우고, 침대 오른편(+x, 누웠을 때 오른손 쪽)에 트렁크와 협탁. 별 조명은 형태를 바꾸고 작은 협탁 위로
//  Z51q 실측(방 로컬): 매트 x −1.13~1.14 · z −2.40~−0.20 (머리 −z). +x 쪽 매트 끝 ~ 거울(x 2.67) 1.53 m 빔
//  루트 TentBedroom/Bedside
//   NightstandR (1.50, −2.05): 협탁 + 탁상시계(TMP 3D, PyriteBedroomPanel.deskClock 이 PC 시각·알람 표시) + 물병(Props/WaterBottle 이동)
//   NightstandL (−1.55, −2.05): 작은 협탁. Mood/StarLamp 를 위로 옮기고 외형 교체(나무 받침 + 별이 박힌 발광 구). 광원·쿠키·패널 연결은 그대로
//   Trunk (1.52, −1.05): 긴 축 z. 뚜껑 = 벽 쪽 경첩, 누르면 열림(PyriteTrunkLid, 동기화). 안에 접힌 담요
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteBedside
{
    public const string ROOT = "Bedside";
    public const string CLOCK_PATH = "Bedside/NightstandR/Clock/Digits";
    const string DIR = "Assets/Bedroom/Bedside/";
    const string PREV = "Assets/_preview/bedroom/";
    const string FA = "Assets/Fonts/NotoSansKR/NotoSansKR-UI SDF.asset";

    static readonly Vector3 NS_R = new Vector3(1.50f, 0f, -2.05f);
    static readonly Vector3 NS_L = new Vector3(-1.55f, 0f, -2.05f);
    static readonly Vector3 TRUNK = new Vector3(1.52f, 0f, -1.05f);
    static readonly Vector3 CLOCK_AIM = new Vector3(0.55f, 0f, -1.30f);          // 누운 자리(Lie_3·4)와 방 쪽
    static readonly Vector3 LAMP_OLD = new Vector3(-1.50f, 0f, -2.10f);          // PyriteBedroomMood.LAMP_POS
    static readonly Vector3 BOTTLE_OLD = new Vector3(1.30f, 0f, -2.05f);         // PyriteBedroomProps 물병
    const float NS_W = 0.38f, NS_H = 0.40f, NS_W_L = 0.32f, NS_H_L = 0.36f;
    const float TR_W = 0.46f, TR_L = 0.85f, TR_H = 0.36f, LID_H = 0.08f;
    static readonly Color CLOCK_COL = new Color(1f, 0.58f, 0.22f, 1f);
    static StringBuilder sb;
    static int tris;

    [MenuItem("Tools/Pyrite3/Z51r. Bedside Furniture Build", false, 5118)]
    public static void Build()
    {
        sb = new StringBuilder("[Z51r] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
        // 후속 확인: 위에서 본 배치(Z51q) + 별 조명 렌더(Z51k) — 각자 로그를 쓴다
        try { PyriteBedroomLayoutProbe.Run(); PyriteBedroomMood.RenderOnly(); } catch (System.Exception e) { Debug.LogError("[Z51r] 후속 렌더 " + e); }
    }

    [MenuItem("Tools/Pyrite3/Z51s. Bedside Furniture Revert", false, 5119)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z51s] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom");
        if (room)
        {
            var o = room.transform;
            var t = o.Find(ROOT); if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제"); }
            RestoreLamp(o);
            var bottle = o.Find("Props/WaterBottle"); if (bottle) { bottle.localPosition = BOTTLE_OLD; bottle.localRotation = Quaternion.identity; sb.AppendLine("물병 → " + V(BOTTLE_OLD)); }
            WireClock(o, null);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    [MenuItem("Tools/Pyrite3/Z51t. Bedside Renders", false, 5120)]
    public static void RenderOnly()
    {
        sb = new StringBuilder("[Z51t] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Assets ▸ Refresh 후 Z51r 다시");
        return false;
    }

    // ═════════════════════════════════════════════════════════════
    static bool Inner()
    {
        if (!EnsureProgram("PyriteTrunkLid")) return false;
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        var o = room.transform;
        Directory.CreateDirectory(DIR);
        var old = o.Find(ROOT); if (old) Object.DestroyImmediate(old.gameObject);
        RestoreLamp(o);   // 재실행 안전: 먼저 원래 상태로
        var root = new GameObject(ROOT).transform; root.SetParent(o, false);
        tris = 0;

        // 재질: 협탁 = 앞 낮은 테이블(Furniture/LowTable)과 같은 나무
        var lt = o.Find("Furniture/LowTable"); var ltr = lt ? lt.GetComponentsInChildren<Renderer>(true).FirstOrDefault() : null;
        var wood = ltr ? ltr.sharedMaterial : Mat("M_BedsideWood", new Color(0.42f, 0.27f, 0.15f), 0.25f);
        sb.AppendLine("나무 재질: " + (wood ? wood.name + " (" + (wood.shader ? wood.shader.name : "?") + ")" : "없음") + (ltr ? " ← LowTable 재사용" : " (새로 만듦)"));
        var trunkWood = Mat("M_TrunkWood", new Color(0.30f, 0.17f, 0.09f), 0.30f);
        if (wood && wood.HasProperty("_MainTex") && wood.mainTexture) { trunkWood.mainTexture = wood.mainTexture; trunkWood.color = wood.color * new Color(0.62f, 0.55f, 0.50f, 1f); }
        var leather = Mat("M_TrunkLeather", new Color(0.13f, 0.08f, 0.05f), 0.38f);
        var brass = Mat("M_Brass", new Color(0.72f, 0.54f, 0.26f), 0.62f, 0.85f);

        // ── 오른쪽 협탁 + 시계 + 물병 ──
        var nsR = Nightstand(root, "NightstandR", NS_R, NS_W, NS_H, wood, brass);
        var clock = BuildClock(nsR, new Vector3(-0.05f, NS_H, 0.04f), o);
        var bottle = o.Find("Props/WaterBottle");
        if (bottle) { bottle.localPosition = NS_R + new Vector3(0.10f, NS_H, -0.10f); sb.AppendLine("물병 " + V(BOTTLE_OLD) + " → " + V(bottle.localPosition) + " (협탁 위)"); }
        else sb.AppendLine("  (Props/WaterBottle 없음)");

        // ── 왼쪽 작은 협탁 + 별 조명 ──
        var nsL = Nightstand(root, "NightstandL", NS_L, NS_W_L, NS_H_L, wood, brass);
        MoveLamp(o, NS_L + new Vector3(0f, NS_H_L, 0f), wood);

        // ── 트렁크 ──
        BuildTrunk(root, o, trunkWood, leather, brass);

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.gameObject.layer = PyriteBedroomV3.LAYER; r.lightProbeUsage = LightProbeUsage.Off;
            if (r is MeshRenderer) r.shadowCastingMode = ShadowCastingMode.On;
        }
        WireClock(o, clock);
        sb.AppendLine("삼각형 합계 " + tris);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    // ── 협탁: 상판 · 다리 4 · 서랍 · 아래 선반 · 손잡이. 앞 = +z(발치 쪽) ──
    static Transform Nightstand(Transform root, string name, Vector3 pos, float w, float h, Material wood, Material brass)
    {
        var t = new GameObject(name).transform; t.SetParent(root, false); t.localPosition = pos;
        Box(t, "Top", new Vector3(0, h - 0.015f, 0), new Vector3(w, 0.03f, w), wood);
        float lx = w / 2f - 0.028f;
        foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            Box(t, "Leg", new Vector3(sx * lx, (h - 0.03f) / 2f, sz * lx), new Vector3(0.034f, h - 0.03f, 0.034f), wood);
        Box(t, "Drawer", new Vector3(0, h - 0.03f - 0.055f, 0), new Vector3(w - 0.05f, 0.11f, w - 0.05f), wood);
        Box(t, "Shelf", new Vector3(0, 0.10f, 0), new Vector3(w - 0.05f, 0.018f, w - 0.05f), wood);
        Box(t, "Knob", new Vector3(0, h - 0.03f - 0.055f, (w - 0.05f) / 2f + 0.008f), new Vector3(0.03f, 0.018f, 0.016f), brass);
        Col(t, new Vector3(0, h / 2f, 0), new Vector3(w, h, w));
        sb.AppendLine(name + " " + V(pos) + " · " + w + " × " + w + " × 높이 " + h + " m");
        return t;
    }

    // ── 탁상시계: 몸통 + 유리면 + TMP 3D 숫자 (호박색). 앞(+z)을 CLOCK_AIM 쪽으로 ──
    static TextMeshPro BuildClock(Transform ns, Vector3 local, Transform room)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FA);
        if (font == null) { sb.AppendLine("!! 글꼴 없음 " + FA); return null; }
        foreach (var c in "0123456789:APMLR ") if (!font.HasCharacter(c)) sb.AppendLine("!! 글꼴에 없는 글자 '" + c + "'");
        var t = new GameObject("Clock").transform; t.SetParent(ns, false); t.localPosition = local;
        var wp = ns.TransformPoint(local); var aim = room.TransformPoint(CLOCK_AIM) - wp; aim.y = 0f;
        t.rotation = Quaternion.LookRotation(aim.normalized, Vector3.up);
        var body = Mat("M_ClockBody", new Color(0.07f, 0.06f, 0.055f), 0.55f);
        var glass = Mat("M_ClockGlass", new Color(0.01f, 0.01f, 0.012f), 0.95f);
        Box(t, "Body", new Vector3(0, 0.040f, 0), new Vector3(0.16f, 0.080f, 0.065f), body);
        Box(t, "Foot", new Vector3(0, 0.004f, 0.005f), new Vector3(0.15f, 0.008f, 0.06f), body);
        Box(t, "Glass", new Vector3(0, 0.042f, 0.0326f), new Vector3(0.138f, 0.058f, 0.002f), glass);
        var d = new GameObject("Digits"); d.transform.SetParent(t, false);
        d.transform.localPosition = new Vector3(0, 0.042f, 0.0342f);
        d.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);   // TMP 3D 는 −z 에서 읽힘 → 앞(+z)에서 읽히게
        var tmp = d.AddComponent<TextMeshPro>();
        tmp.font = font; tmp.fontSharedMaterial = font.material;
        tmp.rectTransform.sizeDelta = new Vector2(0.128f, 0.050f);
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 0.02f; tmp.fontSizeMax = 3f;
        tmp.alignment = TextAlignmentOptions.Center; tmp.enableWordWrapping = false; tmp.richText = true;
        tmp.color = CLOCK_COL; tmp.lineSpacing = -20f;
        var now = System.DateTime.Now; int h12 = now.Hour % 12; if (h12 == 0) h12 = 12;
        tmp.text = h12 + ":" + now.Minute.ToString("00") + "<size=40%> " + (now.Hour < 12 ? "AM" : "PM");
        sb.AppendLine("탁상시계 " + V(room.InverseTransformPoint(t.position)) + " yaw " + t.eulerAngles.y.ToString("F0") + "° → " + V(CLOCK_AIM) + " 쪽, 글자 " + tmp.text);
        return tmp;
    }

    static void WireClock(Transform room, TextMeshPro tmp)
    {
        var p = room.Find("BedroomPanel"); var pb = p ? p.GetComponent<PyriteBedroomPanel>() : null;
        if (pb == null) { sb.AppendLine("  (BedroomPanel 없음 — 시계 연결 안 함)"); return; }
        pb.deskClock = tmp; pb.deskClockColor = CLOCK_COL;
        UdonSharpEditorUtility.CopyProxyToUdon(pb); EditorUtility.SetDirty(pb);
        sb.AppendLine("  패널 deskClock " + (tmp != null ? "연결" : "해제"));
    }

    // ── 별 조명: 위치를 작은 협탁 위로, 외형 교체 (옛 Base·Dome 은 끄고 EditorOnly) ──
    static void MoveLamp(Transform room, Vector3 pos, Material wood)
    {
        var lamp = room.Find("Mood/StarLamp"); if (lamp == null) { sb.AppendLine("!! Mood/StarLamp 없음 (Z51i 먼저)"); return; }
        foreach (var n in new[] { "Base", "Dome" }) { var c = lamp.Find(n); if (c) { c.gameObject.SetActive(false); c.gameObject.tag = "EditorOnly"; } }
        lamp.localPosition = pos;
        var baseMat = Mat("M_GlobeBase", new Color(0.36f, 0.23f, 0.13f), 0.40f);
        if (wood && wood.mainTexture) { baseMat.mainTexture = wood.mainTexture; baseMat.color = wood.color; }
        var prof = new[] { new Vector2(0.001f, 0f), new Vector2(0.066f, 0f), new Vector2(0.068f, 0.006f), new Vector2(0.064f, 0.030f), new Vector2(0.050f, 0.036f), new Vector2(0.030f, 0.040f), new Vector2(0.001f, 0.040f) };
        MeshObj(lamp, "BS_GlobeBase", SaveMesh(Lathe(prof, 36), "GlobeBase"), baseMat);
        var globe = Mat("M_StarGlobe", new Color(0.035f, 0.045f, 0.10f), 0.92f, 0f, Color.white * 1.1f);
        globe.SetTexture("_EmissionMap", StarGlobeTex(512, 256));
        var g = MeshObj(lamp, "BS_Globe", SaveMesh(UVSphere(0.072f, 28, 18), "StarGlobe"), globe);
        g.transform.localPosition = new Vector3(0f, 0.040f + 0.068f, 0f);
        var light = lamp.Find("StarLight");
        if (light) light.localPosition = new Vector3(0f, 0.040f + 0.068f, 0f);
        foreach (var r in lamp.GetComponentsInChildren<Renderer>(true)) { r.gameObject.layer = PyriteBedroomV3.LAYER; r.lightProbeUsage = LightProbeUsage.Off; r.shadowCastingMode = ShadowCastingMode.Off; }
        sb.AppendLine("별 조명 " + V(LAMP_OLD) + " → " + V(pos) + " · 나무 받침(지름 0.14) + 별 발광 구(지름 0.144) · 높이 약 0.18 m · 광원 높이 " + (pos.y + 0.108f).ToString("F2") + " m (전 0.10)");
    }

    static void RestoreLamp(Transform room)
    {
        var lamp = room.Find("Mood/StarLamp"); if (lamp == null) return;
        foreach (Transform c in lamp.Cast<Transform>().ToArray()) if (c.name.StartsWith("BS_")) Object.DestroyImmediate(c.gameObject);
        foreach (var n in new[] { "Base", "Dome" }) { var c = lamp.Find(n); if (c) { c.gameObject.SetActive(true); c.gameObject.tag = "Untagged"; } }
        lamp.localPosition = LAMP_OLD;
        var light = lamp.Find("StarLight"); if (light) light.localPosition = new Vector3(0f, 0.10f, 0f);
        if (sb != null) sb.AppendLine("  별 조명 원래 자리·외형 " + V(LAMP_OLD));
    }

    // 발광 구 텍스처: 짙은 남색 바탕(은은한 빛) + 별 점. 적도 가로 2:1
    static Texture2D StarGlobeTex(int w, int h)
    {
        var px = new Color[w * h];
        var bg = new Color(0.10f, 0.13f, 0.30f);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        var rnd = new System.Random(7);
        for (int s = 0; s < 300; s++)
        {
            float cx = (float)rnd.NextDouble() * w, cy = h * (0.12f + 0.76f * (float)rnd.NextDouble());
            float big = (float)rnd.NextDouble(); float r = big > 0.93f ? 2.2f : big > 0.7f ? 1.5f : 1.0f; float a = big > 0.93f ? 1.6f : 0.7f + 0.5f * (float)rnd.NextDouble();
            float sx = 1f / Mathf.Max(0.25f, Mathf.Sin(Mathf.PI * cy / h));   // 극 쪽은 가로로 늘여 구에서 둥글게
            for (int y = (int)(cy - r * 3); y <= (int)(cy + r * 3); y++)
                for (int x = (int)(cx - r * 3 * sx); x <= (int)(cx + r * 3 * sx); x++)
                {
                    if (y < 0 || y >= h) continue; int xx = (x % w + w) % w;
                    float dx = (x + 0.5f - cx) / sx, dy = y + 0.5f - cy; float k = a * Mathf.Exp(-(dx * dx + dy * dy) / (r * r) * 1.6f);
                    var c = px[y * w + xx]; px[y * w + xx] = new Color(Mathf.Max(c.r, k * 0.92f), Mathf.Max(c.g, k * 0.96f), Mathf.Max(c.b, k));
                }
        }
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels(px); t.Apply();
        string path = DIR + "T_StarGlobe.png"; File.WriteAllBytes(path, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path); imp.wrapModeU = TextureWrapMode.Repeat; imp.wrapModeV = TextureWrapMode.Clamp; imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ── 트렁크: 몸통 + 가죽띠 + 황동 모서리 + 손잡이, 뚜껑은 벽 쪽(+x) 경첩 ──
    static void BuildTrunk(Transform root, Transform room, Material wood, Material leather, Material brass)
    {
        var t = new GameObject("Trunk").transform; t.SetParent(root, false); t.localPosition = TRUNK;
        // 속이 빈 몸통: 바닥 + 벽 4 (두께 2 cm) + 안감(짙은 붉은 펠트)
        const float WT = 0.02f;
        var lining = Mat("M_TrunkLining", new Color(0.26f, 0.06f, 0.05f), 0.05f);
        Box(t, "Bottom", new Vector3(0, 0.025f, 0), new Vector3(TR_W, 0.03f, TR_L), wood);
        Box(t, "WallFront", new Vector3(-TR_W / 2f + WT / 2f, TR_H / 2f, 0), new Vector3(WT, TR_H, TR_L), wood);
        Box(t, "WallBack", new Vector3(TR_W / 2f - WT / 2f, TR_H / 2f, 0), new Vector3(WT, TR_H, TR_L), wood);
        foreach (var sz in new[] { -1, 1 }) Box(t, "WallEnd", new Vector3(0, TR_H / 2f, sz * (TR_L / 2f - WT / 2f)), new Vector3(TR_W - 2f * WT, TR_H, WT), wood);
        Box(t, "Lining", new Vector3(0, 0.041f, 0), new Vector3(TR_W - 2f * WT, 0.002f, TR_L - 2f * WT), lining);
        // 가죽띠: 바깥면에만 (앞·뒤·바닥 띠) — 통짜 상자로 하면 속을 가로지름
        foreach (var z in new[] { -0.24f, 0.24f })
        {
            Box(t, "StrapF", new Vector3(-TR_W / 2f - 0.003f, TR_H / 2f, z), new Vector3(0.006f, TR_H, 0.05f), leather);
            Box(t, "StrapB", new Vector3(TR_W / 2f + 0.003f, TR_H / 2f, z), new Vector3(0.006f, TR_H, 0.05f), leather);
        }
        foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
        {
            Box(t, "Corner", new Vector3(sx * TR_W / 2f, TR_H / 2f, sz * TR_L / 2f), new Vector3(0.036f, TR_H + 0.004f, 0.036f), brass);
            Box(t, "Foot", new Vector3(sx * (TR_W / 2f - 0.04f), 0.006f, sz * (TR_L / 2f - 0.05f)), new Vector3(0.05f, 0.012f, 0.05f), brass);
        }
        foreach (var sz in new[] { -1, 1 }) Box(t, "Handle", new Vector3(0, TR_H * 0.62f, sz * (TR_L / 2f + 0.014f)), new Vector3(0.13f, 0.026f, 0.02f), leather);
        Col(t, new Vector3(0, TR_H / 2f, 0), new Vector3(TR_W, TR_H, TR_L));

        // 안: 접힌 예비 담요 2장 (침대 발치 담요 메시 재사용, 색은 표준 담요 재질)
        var bf = room.Find("Beds/BlanketFold"); var bmf = bf ? bf.GetComponent<MeshFilter>() : null; var fold = bmf ? bmf.sharedMesh : null;
        var bms = new[] { AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Blanket_1.mat"), AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Blanket_2.mat") };
        if (fold && bms[0])
        {
            float[] ys = { 0.235f, 0.312f };
            for (int i = 0; i < 2; i++)
            {
                var b = MeshObj(t, "SpareBlanket", fold, bms[i] ? bms[i] : bms[0]);
                b.transform.localPosition = new Vector3(0f, ys[i], 0.01f * (i * 2 - 1)); b.transform.localRotation = Quaternion.Euler(0f, 90f + i * 3f, 0f);
                var bs = fold.bounds.size; b.transform.localScale = new Vector3(0.76f / bs.x, 0.072f / bs.y, 0.38f / bs.z);   // 안쪽 0.42 × 0.81 에 맞춤 (메시 x → 트렁크 z)
            }
            sb.AppendLine("  예비 담요 2장 (메시 " + fold.name + " " + fold.bounds.size.ToString("F2") + ")");
        }
        else sb.AppendLine("  (예비 담요 메시/재질 없음 — 생략: Beds/BlanketFold " + (bf != null) + ")");

        // 뚜껑 (경첩 = 벽 쪽 윗모서리)
        var lid = new GameObject("Lid"); lid.transform.SetParent(t, false); lid.transform.localPosition = new Vector3(TR_W / 2f, TR_H, 0f);
        var lt = lid.transform; float cx = -TR_W / 2f;
        Box(lt, "LidTop", new Vector3(cx, LID_H / 2f, 0), new Vector3(TR_W, LID_H, TR_L), wood);
        foreach (var z in new[] { -0.24f, 0.24f }) Box(lt, "Strap", new Vector3(cx, LID_H / 2f, z), new Vector3(TR_W + 0.008f, LID_H + 0.004f, 0.05f), leather);
        foreach (var (sx, sz) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
            Box(lt, "Cap", new Vector3(cx + sx * TR_W / 2f, LID_H / 2f, sz * TR_L / 2f), new Vector3(0.038f, LID_H + 0.006f, 0.038f), brass);
        Box(lt, "Latch", new Vector3(-TR_W - 0.006f, LID_H * 0.35f, 0f), new Vector3(0.012f, 0.07f, 0.06f), brass);
        var bc = lid.AddComponent<BoxCollider>(); bc.center = new Vector3(cx, LID_H / 2f, 0); bc.size = new Vector3(TR_W, LID_H, TR_L);
        var ul = UdonSharpUndo.AddComponent<PyriteTrunkLid>(lid);
        UdonSharpEditorUtility.CopyProxyToUdon(ul);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(ul);
        if (ub != null) { ub.interactText = "Open"; ub.proximity = 2f; EditorUtility.SetDirty(ub); }
        sb.AppendLine("트렁크 " + V(TRUNK) + " · " + TR_W + " × " + TR_L + " × 높이 " + (TR_H + LID_H) + " m, 뚜껑 경첩 +x(벽 쪽), 열림 −100°, 거울(x 2.67)까지 통로 " + (2.67f - TRUNK.x - TR_W / 2f).ToString("F2") + " m");
    }

    // ───────────── 공통 ─────────────
    static Transform Box(Transform p, string n, Vector3 center, Vector3 size, Material m)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.name = n; g.transform.SetParent(p, false); g.transform.localPosition = center; g.transform.localScale = size;
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        tris += 12;
        return g.transform;
    }

    static void Col(Transform p, Vector3 center, Vector3 size)
    {
        var g = new GameObject("Col"); g.transform.SetParent(p, false); g.layer = 0;
        var bc = g.AddComponent<BoxCollider>(); bc.center = center; bc.size = size;
    }

    static GameObject MeshObj(Transform parent, string name, Mesh mesh, Material mat)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        tris += mesh.triangles.Length / 3;
        return g;
    }

    static Material Mat(string name, Color c, float gloss, float metal = 0f, Color? emit = null)
    {
        string path = DIR + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.color = c; m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", metal);
        if (emit.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emit.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
        else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Mesh Lathe(Vector2[] prof, int seg)
    {
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        for (int i = 0; i < prof.Length; i++)
            for (int s = 0; s <= seg; s++)
            {
                float a = 2f * Mathf.PI * s / seg;
                vs.Add(new Vector3(Mathf.Cos(a) * prof[i].x, prof[i].y, Mathf.Sin(a) * prof[i].x));
                uv.Add(new Vector2((float)s / seg, (float)i / (prof.Length - 1)));
            }
        int row = seg + 1;
        for (int i = 0; i < prof.Length - 1; i++)
            for (int s = 0; s < seg; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                ts.Add(a); ts.Add(c); ts.Add(b); ts.Add(b); ts.Add(c); ts.Add(d);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateNormals(); m.RecalculateBounds();
        return m;
    }

    static Mesh UVSphere(float r, int nu, int nv)
    {
        var vs = new List<Vector3>(); var ns = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        for (int j = 0; j <= nv; j++)
        {
            float th = Mathf.PI * j / nv;
            for (int i = 0; i <= nu; i++)
            {
                float ph = 2f * Mathf.PI * i / nu;
                var n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                vs.Add(n * r); ns.Add(n); uv.Add(new Vector2((float)i / nu, 1f - (float)j / nv));
            }
        }
        int row = nu + 1;
        for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                int a = j * row + i, b = a + 1, c = a + row, d = c + 1;
                ts.Add(a); ts.Add(b); ts.Add(c); ts.Add(b); ts.Add(d); ts.Add(c);
            }
        var m = new Mesh(); m.SetVertices(vs); m.SetNormals(ns); m.SetUVs(0, uv); m.SetTriangles(ts, 0); m.RecalculateBounds();
        return m;
    }

    // ───────────── 렌더 (21시) ─────────────
    static void Renders()
    {
        var room = Root("TentBedroom").transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var lid = room.Find(ROOT + "/Trunk/Lid");
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => room.TransformPoint(new Vector3(x, y, z));
            cam.fieldOfView = 60f;
            Shot(cam, W(0.2f, 1.45f, 0.4f), W(1.45f, 0.30f, -1.45f), "bd_right");
            Shot(cam, W(-0.35f, 1.05f, -0.85f), W(-1.55f, 0.42f, -2.05f), "bd_left");
            Shot(cam, W(1.05f, 0.70f, -1.55f), W(1.45f, 0.44f, -2.01f), "bd_clock");
            Shot(cam, W(-0.84f, 0.45f, -1.72f), W(0.1f, 2.6f, 0.6f), "bd_lie_up");
            Shot(cam, W(1.6f, 1.55f, 2.1f), W(0f, 0.35f, -1.4f), "bd_room");
            if (lid)
            {
                var r = lid.localRotation; lid.localRotation = Quaternion.Euler(0, 0, -100f);
                Shot(cam, W(0.55f, 1.35f, -0.45f), W(1.52f, 0.36f, -1.05f), "bd_trunk_open");
                lid.localRotation = r;
            }
        }
        finally
        {
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
        var px = tex.GetPixels32(); double sum = 0; foreach (var q in px) sum += q.r + q.g + q.b;
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag + " 평균 " + (sum / px.Length / 3).ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedside.txt", sb.ToString(), new UTF8Encoding(false));
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
