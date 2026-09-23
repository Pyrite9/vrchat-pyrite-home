// Tools ▸ Pyrite ▸ Z25a. Build Settings UI  /  Z25b. Settings UI Revert
//  프로젝터 패널(3.0×1.69 m) 앞에 월드 캔버스(1920×1080 px, 1 px = 1.5625 mm)를 띄우고 설정 6칸을 만든다
//   시간(분 슬라이더·자동 흐름, 모두에게 공유) · 화면(밝기 ±1 EV, 블룸) · 반사(호수·캠프 거울) · 소리(자연 소리 0~150%) · 성능(꽃·반딧불·해 그림자) · 정보
//   EN / KO — 글꼴 Noto Sans KR Medium (OFL, ASCII + KS X 1001 한글 2350자 서브셋) → 정적 TMP 폰트(사용 글자만)
//   후처리: SettingsPP 루트에 전역 볼륨 3개 (layer 23, priority 10, weight 0) — 밝게(노출 2.0)/어둡게(0.0)/블룸 끔(강도 0). 기존 프로필 노출 1.0 기준 ±1 EV
//   LakeMirrorSwitch 는 렌더러·콜라이더만 끈다(스크립트는 살아서 호수 거울 상태를 쥔다) → 설정의 호수 반사 토글이 SetOn 으로 조작
//   패널 queue 2990 / 빛줄기 2980 → UI(3000) 가 패널 유리 위에 그려진다
//  렌더 Assets/_preview/projector/ui_*.png (테이블에 선 눈높이 1.6 m, FOV 60 = VRChat 데스크톱 기본)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

public static class PyriteSettingsUI
{
    const string FONT_DIR = "Assets/Fonts/NotoSansKR/";
    const string TTF = FONT_DIR + "NotoSansKR-Medium-KS.ttf";
    const string FA = FONT_DIR + "NotoSansKR-UI SDF.asset";
    const string PP_BRIGHT = "Assets/TerrainAssets/PP_UserBright.asset";
    const string PP_DARK = "Assets/TerrainAssets/PP_UserDark.asset";
    const string PP_NOBLOOM = "Assets/TerrainAssets/PP_UserNoBloom.asset";
    const string HIDDEN_LOG = "Logs/pyrite_settings_hidden.txt";
    const int PP_LAYER = 23;
    const float W = 1920f, H = 1080f;

    static readonly Color GOLD = new Color(0.96f, 0.80f, 0.45f, 1f);
    static readonly Color WHITE = new Color(0.94f, 0.94f, 0.92f, 1f);
    static readonly Color GREY = new Color(0.64f, 0.64f, 0.62f, 1f);
    static readonly Color BOX = new Color(1f, 1f, 1f, 0.10f);
    static readonly Color CARD = new Color(1f, 1f, 1f, 0.05f);

    static TMP_FontAsset font;
    static Sprite spr, knob;
    static UdonBehaviour ub;
    static List<(TextMeshProUGUI t, string en, string ko)> loc;

    [MenuItem("Tools/Pyrite/Z25a. Build Settings UI", false, 60)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z25a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var root = GameObject.Find("SettingsProjector");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (root == null || cyc == null) { sb.AppendLine("SettingsProjector/DayCycle 없음 (Z24b 먼저)"); Flush(sb); return; }
        var pj = root.GetComponent<PyriteProjector>();
        var panel = root.transform.Find("Panel");
        var beam = root.transform.Find("Beam");
        if (pj == null || panel == null) { sb.AppendLine("PyriteProjector/Panel 없음"); Flush(sb); return; }

        var old = root.transform.Find("SettingsUI"); if (old != null) Object.DestroyImmediate(old.gameObject);
        pj.onObjects = pj.onObjects.Where(o => o != null).ToArray();

        // 1) 문구 (en, ko)
        var S = Strings();

        // 2) 글꼴
        font = BuildFont(S, sb);
        if (font == null) { Flush(sb); return; }

        // 3) 후처리 볼륨
        var (vBright, vDark, vNoBloom) = BuildPP(sb);

        // 4) 그리기 순서
        panel.GetComponent<Renderer>().sharedMaterial.renderQueue = 2990;
        // 유리 불투명도 0.78 → 0.93: 정오에 뒤 배경(부두·결정)이 비쳐 글자가 묻혔다
        panel.GetComponent<Renderer>().sharedMaterial.SetColor("_Glass", new Color(0.02f, 0.025f, 0.032f, 0.93f));
        if (beam != null) beam.GetComponent<Renderer>().sharedMaterial.renderQueue = 2980;

        // 5) 캔버스
        var go = new GameObject("SettingsUI", typeof(RectTransform));
        go.layer = 0;
        go.transform.SetParent(root.transform, false);
        var rt = go.GetComponent<RectTransform>();
        var dir = panel.forward;
        rt.position = panel.position - dir * 0.01f;
        rt.rotation = panel.rotation;
        rt.sizeDelta = new Vector2(W, H);
        rt.localScale = Vector3.one * (panel.lossyScale.x / W);
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var cs = go.AddComponent<CanvasScaler>(); cs.dynamicPixelsPerUnit = 1f; cs.referencePixelsPerUnit = 100f;
        go.AddComponent<GraphicRaycaster>();
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(W, H, 2f);
        go.AddComponent<VRCUiShape>();

        PyriteSettings st;
        try { st = UdonSharpUndo.AddComponent<PyriteSettings>(go); }
        catch (System.Exception e) { sb.AppendLine("AddComponent 실패 (H 로 프로그램 에셋 먼저): " + e.Message); Flush(sb); return; }
        ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(st);

        spr = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        loc = new List<(TextMeshProUGUI, string, string)>();
        var t = go.transform;

        // 제목 줄
        L(Txt(t, "Title", 90, 52, 1000, 76, "", 62, GOLD), S["title"]);
        L(Txt(t, "Subtitle", 94, 128, 700, 42, "", 30, GREY), S["subtitle"]);
        var (bEn, iEn, _) = Btn(t, "LangEN", 1464, 66, 116, 66, "EN", "SetEN");
        var (bKo, iKo, _) = Btn(t, "LangKO", 1590, 66, 116, 66, "KO", "SetKO");
        Btn(t, "Close", 1740, 66, 90, 66, "×", "Close");
        Img(t, "Divider", 90, 185, 1740, 3, new Color(GOLD.r, GOLD.g, GOLD.b, 0.35f), null);

        // 칸 (3 × 2)
        const float cw = 553f, ch = 370f, gx = 40f, top = 215f, gy = 40f;
        Transform Card(string n, int c, int r, string key)
        {
            float x = 90f + c * (cw + gx), y = top + r * (ch + gy);
            var card = Img(t, n, x, y, cw, ch, CARD, spr).transform;
            L(Txt(card, "Head", 32, 24, cw - 64, 44, "", 30, GOLD, spacing: 6f), S[key]);
            return card;
        }

        // 시간
        var cTime = Card("CardTime", 0, 0, "time");
        var timeText = Txt(cTime, "Clock", 32, 70, 489, 130, "21:00", 108, WHITE);
        var timeSlider = Sld(cTime, "TimeSlider", 32, 212, 489, 0f, 1439f, 1260f, true, "OnTime");
        Txt(cTime, "Min", 32, 254, 150, 28, "00:00", 20, GREY);
        Txt(cTime, "Max", 371, 254, 150, 28, "24:00", 20, GREY, TextAlignmentOptions.Right);
        var (autoT, autoL) = Tgl(cTime, "Auto", 32, 282, 489, false, "OnAuto"); L(autoL, S["auto"]);
        L(Txt(cTime, "Note", 32, 334, 489, 28, "", 20, GREY), S["timeNote"]);

        // 화면
        var cView = Card("CardView", 1, 0, "view");
        L(Txt(cView, "BrightLabel", 32, 82, 489, 44, "", 30, WHITE), S["bright"]);
        var brightSlider = Sld(cView, "BrightSlider", 32, 136, 489, -1f, 1f, 0f, false, "OnBright");
        L(Txt(cView, "Darker", 32, 178, 240, 28, "", 20, GREY), S["darker"]);
        L(Txt(cView, "Brighter", 281, 178, 240, 28, "", 20, GREY, TextAlignmentOptions.Right), S["brighter"]);
        var (bloomT, bloomL) = Tgl(cView, "Bloom", 32, 240, 489, true, "OnBloom"); L(bloomL, S["bloom"]);

        // 반사
        var cRefl = Card("CardReflect", 2, 0, "refl");
        var (lakeT, lakeL) = Tgl(cRefl, "Lake", 32, 90, 489, true, "OnLake"); L(lakeL, S["lake"]);
        var (campT, campL) = Tgl(cRefl, "CampMirror", 32, 160, 489, false, "OnCampMirror"); L(campL, S["campMirror"]);
        L(Txt(cRefl, "Note", 32, 250, 489, 90, "", 21, GREY, TextAlignmentOptions.TopLeft), S["reflNote"]);

        // 소리
        var cSound = Card("CardSound", 0, 1, "sound");
        L(Txt(cSound, "SoundLabel", 32, 82, 360, 44, "", 30, WHITE), S["nature"]);
        var soundText = Txt(cSound, "SoundValue", 381, 82, 140, 44, "100%", 30, GOLD, TextAlignmentOptions.Right);
        var soundSlider = Sld(cSound, "SoundSlider", 32, 136, 489, 0f, 1.5f, 1f, false, "OnSound");
        L(Txt(cSound, "Note", 32, 184, 489, 28, "", 20, GREY), S["soundNote"]);

        // 성능
        var cPerf = Card("CardPerf", 1, 1, "perf");
        var (flT, flL) = Tgl(cPerf, "Flowers", 32, 86, 489, true, "OnFlowers"); L(flL, S["flowers"]);
        var (ffT, ffL) = Tgl(cPerf, "Fireflies", 32, 152, 489, true, "OnFireflies"); L(ffL, S["fireflies"]);
        var (shT, shL) = Tgl(cPerf, "Shadows", 32, 218, 489, true, "OnShadows"); L(shL, S["shadows"]);
        L(Txt(cPerf, "Note", 32, 300, 489, 50, "", 20, GREY, TextAlignmentOptions.TopLeft), S["perfNote"]);

        // 정보
        var cAbout = Card("CardAbout", 2, 1, "about");
        L(Txt(cAbout, "World", 32, 80, 489, 56, "", 40, WHITE), S["world"]);
        L(Txt(cAbout, "Body", 32, 146, 489, 150, "", 22, GREY, TextAlignmentOptions.TopLeft), S["aboutBody"]);
        L(Txt(cAbout, "Credit", 32, 312, 489, 32, "", 22, GOLD), S["credit"]);

        foreach (var x in loc) x.t.text = x.en;

        // 6) 연결
        var toggles = Object.FindObjectsOfType<PyriteMirrorToggle>(true);
        var lakeMirror = toggles.FirstOrDefault(m => m.name.Contains("Lake") || (m.mirror != null && m.mirror.name.Contains("Lake")));
        var campMirror = toggles.FirstOrDefault(m => m != lakeMirror);
        sb.AppendLine("mirror toggles: " + string.Join(", ", toggles.Select(m => m.name + "→" + (m.mirror ? m.mirror.name : "null") + " startOn " + m.startOn)));

        st.cycle = cyc; st.projector = pj;
        st.timeSlider = timeSlider; st.timeText = timeText; st.autoToggle = autoT;
        st.brightSlider = brightSlider; st.ppBright = vBright; st.ppDark = vDark; st.bloomToggle = bloomT; st.ppNoBloom = vNoBloom;
        st.lakeToggle = lakeT; st.campMirrorToggle = campT; st.lakeMirror = lakeMirror; st.campMirror = campMirror;
        st.soundSlider = soundSlider; st.soundText = soundText;
        st.flowersToggle = flT; st.firefliesToggle = ffT; st.shadowsToggle = shT;
        st.flowerRenderers = (cyc.flowerRenderers ?? new Renderer[0]).Where(r => r != null).ToArray();
        st.fireflyRenderers = (cyc.fireflies ?? new ParticleSystem[0]).Where(p => p != null).Select(p => (Renderer)p.GetComponent<ParticleSystemRenderer>()).Where(r => r != null).ToArray();
        st.sunLight = cyc.sun;
        st.texts = loc.Select(x => x.t).ToArray();
        st.textEn = loc.Select(x => x.en).ToArray();
        st.textKo = loc.Select(x => x.ko).ToArray();
        st.langEnBg = iEn; st.langKoBg = iKo; st.lang = 0;
        UdonSharpEditorUtility.CopyProxyToUdon(st); EditorUtility.SetDirty(st);
        ub.interactText = "Settings"; EditorUtility.SetDirty(ub);
        iEn.color = st.langOn; iKo.color = st.langOff;

        pj.onObjects = pj.onObjects.Append(go).ToArray();
        UdonSharpEditorUtility.CopyProxyToUdon(pj); EditorUtility.SetDirty(pj);
        sb.AppendLine(string.Format("flowers {0} fireflies {1} sun {2} (shadows {3}) | texts {4} | lake {5} camp {6}",
            st.flowerRenderers.Length, st.fireflyRenderers.Length, st.sunLight ? st.sunLight.name : "null", st.sunLight ? st.sunLight.shadows.ToString() : "-",
            loc.Count, lakeMirror ? lakeMirror.name : "null", campMirror ? campMirror.name : "null"));

        // 7) 호수 반사 스위치 숨김 (스크립트는 유지)
        HideLakeSwitch(lakeMirror, sb);

        // 8) 렌더
        go.SetActive(false);
        Shots(pj, go, cyc, sb);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.Refresh();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite/Z25b. Settings UI Revert", false, 61)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z25b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var root = GameObject.Find("SettingsProjector");
        if (root != null)
        {
            var ui = root.transform.Find("SettingsUI");
            var pj = root.GetComponent<PyriteProjector>();
            if (pj != null) { pj.onObjects = pj.onObjects.Where(o => o != null && (ui == null || o != ui.gameObject)).ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(pj); EditorUtility.SetDirty(pj); }
            if (ui != null) { Object.DestroyImmediate(ui.gameObject); sb.AppendLine("SettingsUI 삭제"); }
        }
        var pp = Find("SettingsPP"); if (pp != null) { Object.DestroyImmediate(pp); sb.AppendLine("SettingsPP 삭제"); }
        if (File.Exists(HIDDEN_LOG))
        {
            var all = Object.FindObjectsOfType<Transform>(true);
            foreach (var line in File.ReadAllLines(HIDDEN_LOG))
            {
                var p = line.Split('|'); if (p.Length != 2) continue;
                var tr = all.FirstOrDefault(x => PathOf(x) == p[1]); if (tr == null) continue;
                if (p[0] == "R") { var r = tr.GetComponent<Renderer>(); if (r) r.enabled = true; }
                if (p[0] == "C") { var c = tr.GetComponent<Collider>(); if (c) c.enabled = true; }
                sb.AppendLine("  restore " + line);
            }
            File.Delete(HIDDEN_LOG);
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    // ── 문구 ──
    static Dictionary<string, (string en, string ko)> Strings() => new Dictionary<string, (string, string)>
    {
        ["title"] = ("PYRITE HOME", "파이라이트 홈"),
        ["subtitle"] = ("Settings", "설정"),
        ["time"] = ("TIME", "시간"),
        ["auto"] = ("Auto flow", "자동 흐름"),
        ["timeNote"] = ("Shared with everyone · 1 day = 12 min", "모두에게 공유 · 하루 = 12분"),
        ["view"] = ("VIEW", "화면"),
        ["bright"] = ("Brightness", "밝기"),
        ["darker"] = ("Darker", "어둡게"),
        ["brighter"] = ("Brighter", "밝게"),
        ["bloom"] = ("Bloom", "빛 번짐"),
        ["refl"] = ("REFLECTIONS", "반사"),
        ["lake"] = ("Lake reflection", "호수 반사"),
        ["campMirror"] = ("Camp mirror", "캠프 거울"),
        ["reflNote"] = ("Mirrors are only visible to you\nand cost a lot of performance.", "거울은 나에게만 보이며\n성능 부담이 큽니다."),
        ["sound"] = ("SOUND", "소리"),
        ["nature"] = ("Nature sounds", "자연 소리"),
        ["soundNote"] = ("Campfire · lake · crickets", "모닥불 · 호수 · 풀벌레"),
        ["perf"] = ("PERFORMANCE", "성능"),
        ["flowers"] = ("Flowers", "꽃"),
        ["fireflies"] = ("Fireflies", "반딧불이"),
        ["shadows"] = ("Sun shadows", "햇빛 그림자"),
        ["perfNote"] = ("Turn these off if your frame rate drops.", "프레임이 떨어지면 꺼 보세요."),
        ["about"] = ("ABOUT", "정보"),
        ["world"] = ("Pyrite Home", "파이라이트 홈"),
        ["aboutBody"] = ("A quiet camp in a pyrite basin by the lake.\nEverything here applies only to you,\nexcept the time of day.", "호숫가 황철석 분지의 조용한 캠프입니다.\n시간을 제외한 설정은\n나에게만 적용됩니다."),
        ["credit"] = ("Made by Pyrite9", "제작 Pyrite9"),
    };

    static void L(TextMeshProUGUI t, (string en, string ko) s) { loc.Add((t, s.en, s.ko)); }

    // ── 글꼴 ──
    static TMP_FontAsset BuildFont(Dictionary<string, (string en, string ko)> S, StringBuilder sb)
    {
        var src = AssetDatabase.LoadAssetAtPath<Font>(TTF);
        if (src == null) { sb.AppendLine("글꼴 없음: " + TTF); return null; }
        var set = new HashSet<char>();
        for (int c = 32; c < 127; c++) set.Add((char)c);
        foreach (var kv in S) { foreach (var c in kv.Value.en) set.Add(c); foreach (var c in kv.Value.ko) set.Add(c); }
        foreach (var c in "×·%:0123456789") set.Add(c);
        set.Remove('\n');
        var chars = new string(set.OrderBy(c => c).ToArray());

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FA) != null) AssetDatabase.DeleteAsset(FA);
        var fa = TMP_FontAsset.CreateFontAsset(src, 72, 8, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        fa.name = "NotoSansKR-UI SDF";
        AssetDatabase.CreateAsset(fa, FA);
        fa.atlasTexture.name = "NotoSansKR-UI Atlas";
        AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa);
        fa.material.name = "NotoSansKR-UI Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        bool ok = fa.TryAddCharacters(chars, out string missing);
        fa.atlasPopulationMode = AtlasPopulationMode.Static;
        EditorUtility.SetDirty(fa.atlasTexture); EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FA);
        fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FA);
        sb.AppendLine(string.Format("font: {0} chars (hangul {1}) ok {2} missing '{3}' atlas {4}x{5} glyphs {6}",
            chars.Length, chars.Count(c => c >= 0xAC00 && c <= 0xD7A3), ok, missing, fa.atlasTexture.width, fa.atlasTexture.height, fa.glyphTable.Count));
        return fa;
    }

    // ── 후처리 ──
    static T AddS<T>(PostProcessProfile p) where T : PostProcessEffectSettings
    {
        var s = p.AddSettings<T>();
        s.name = typeof(T).Name;
        s.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
        AssetDatabase.AddObjectToAsset(s, p);
        return s;
    }

    static PostProcessProfile Fresh(string path)
    {
        var p = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(path);
        if (p == null) { p = ScriptableObject.CreateInstance<PostProcessProfile>(); AssetDatabase.CreateAsset(p, path); }
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path).Where(o => o is PostProcessEffectSettings).ToArray()) Object.DestroyImmediate(o, true);
        p.settings.Clear();
        return p;
    }

    static (PostProcessVolume, PostProcessVolume, PostProcessVolume) BuildPP(StringBuilder sb)
    {
        var bright = Fresh(PP_BRIGHT);
        var cg = AddS<ColorGrading>(bright); cg.enabled.Override(true); cg.postExposure.Override(2.0f);
        var dark = Fresh(PP_DARK);
        var cd = AddS<ColorGrading>(dark); cd.enabled.Override(true); cd.postExposure.Override(0.0f);
        var nb = Fresh(PP_NOBLOOM);
        var bl = AddS<Bloom>(nb); bl.enabled.Override(true); bl.intensity.Override(0f);
        foreach (var p in new[] { bright, dark, nb }) EditorUtility.SetDirty(p);
        AssetDatabase.SaveAssets();

        var old = Find("SettingsPP"); if (old != null) Object.DestroyImmediate(old);
        var rootPP = new GameObject("SettingsPP"); rootPP.layer = PP_LAYER;
        PostProcessVolume V(string n, PostProcessProfile prof)
        {
            var g = new GameObject(n); g.layer = PP_LAYER; g.transform.SetParent(rootPP.transform, false);
            var v = g.AddComponent<PostProcessVolume>(); v.isGlobal = true; v.priority = 10; v.weight = 0f; v.sharedProfile = prof;
            return v;
        }
        var r = (V("PP_UserBright", bright), V("PP_UserDark", dark), V("PP_UserNoBloom", nb));
        sb.AppendLine("pp volumes: bright(exp 2.0) dark(exp 0.0) nobloom(int 0), priority 10, weight 0");
        return r;
    }

    // ── 호수 스위치 ──
    static void HideLakeSwitch(PyriteMirrorToggle lake, StringBuilder sb)
    {
        var sw = Find("LakeMirrorSwitch");
        if (sw == null) { sb.AppendLine("LakeMirrorSwitch 없음"); return; }
        var lines = new List<string>();
        if (File.Exists(HIDDEN_LOG)) lines.AddRange(File.ReadAllLines(HIDDEN_LOG));
        foreach (var r in sw.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; lines.Add("R|" + PathOf(r.transform)); EditorUtility.SetDirty(r); }
        foreach (var c in sw.GetComponentsInChildren<Collider>(true)) if (c.enabled) { c.enabled = false; lines.Add("C|" + PathOf(c.transform)); EditorUtility.SetDirty(c); }
        Directory.CreateDirectory("Logs"); File.WriteAllLines(HIDDEN_LOG, lines.Distinct());
        sb.AppendLine("LakeMirrorSwitch hidden: " + lines.Count + " (toggle " + (lake ? lake.name : "null") + " 유지)");
    }

    // ── 위젯 ──
    static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var g = new GameObject(name, typeof(RectTransform)); g.layer = 0;
        g.transform.SetParent(parent, false);
        var r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f); r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
        return r;
    }

    static Image Img(Transform parent, string name, float x, float y, float w, float h, Color col, Sprite s)
    {
        var r = Rect(parent, name, x, y, w, h);
        var i = r.gameObject.AddComponent<Image>(); i.color = col; i.raycastTarget = false;
        if (s != null) { i.sprite = s; i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 0.35f; }
        return i;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, float x, float y, float w, float h, string text, float size, Color col,
        TextAlignmentOptions align = TextAlignmentOptions.Left, float spacing = 0f)
    {
        var r = Rect(parent, name, x, y, w, h);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = font.material;
        t.text = text; t.fontSize = size; t.color = col; t.characterSpacing = spacing;
        t.alignment = align == TextAlignmentOptions.Left ? TextAlignmentOptions.MidlineLeft : align == TextAlignmentOptions.Right ? TextAlignmentOptions.MidlineRight : align;
        t.enableWordWrapping = true; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
        return t;
    }

    static void Wire(UnityEventBase ev, string method)
    {
        UnityEventTools.AddStringPersistentListener(ev, new UnityAction<string>(ub.SendCustomEvent), method);
    }

    static Slider Sld(Transform parent, string name, float x, float y, float w, float min, float max, float val, bool whole, string method)
    {
        const float hh = 40f;
        var r = Rect(parent, name, x, y, w, hh);
        var bg = Img(r, "Track", 0, 15, w, 10, new Color(1f, 1f, 1f, 0.16f), spr);
        var fillArea = Rect(r, "Fill Area", 0, 15, w, 10);
        var fill = Img(fillArea, "Fill", 0, 0, 0, 10, GOLD, spr);
        var fr = fill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0.5f, 0.5f); fr.sizeDelta = Vector2.zero; fr.anchoredPosition = Vector2.zero;
        var handleArea = Rect(r, "Handle Slide Area", 18, 0, w - 36, hh);
        var handle = Img(handleArea, "Handle", 0, 0, 38, 0, new Color(1f, 0.93f, 0.78f, 1f), null);
        handle.sprite = knob; handle.type = Image.Type.Simple; handle.raycastTarget = true;
        var hr = handle.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(0, 1); hr.pivot = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(38f, 0f); hr.anchoredPosition = Vector2.zero;
        bg.raycastTarget = true;
        var s = r.gameObject.AddComponent<Slider>();
        s.fillRect = fr; s.handleRect = hr; s.targetGraphic = handle; s.direction = Slider.Direction.LeftToRight;
        s.minValue = min; s.maxValue = max; s.wholeNumbers = whole; s.value = val;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        Wire(s.onValueChanged, method);
        return s;
    }

    static (Toggle, TextMeshProUGUI) Tgl(Transform parent, string name, float x, float y, float w, bool on, string method)
    {
        var r = Rect(parent, name, x, y, w, 56);
        var box = Img(r, "Box", 0, 6, 44, 44, BOX, spr); box.raycastTarget = true;
        var chk = Img(box.transform, "Check", 9, 9, 26, 26, GOLD, spr);
        var lab = Txt(r, "Label", 64, 0, w - 64, 56, "", 30, WHITE); lab.raycastTarget = true;
        var tg = r.gameObject.AddComponent<Toggle>();
        tg.targetGraphic = box; tg.graphic = chk; tg.isOn = on;
        tg.navigation = new Navigation { mode = Navigation.Mode.None };
        Wire(tg.onValueChanged, method);
        return (tg, lab);
    }

    static (Button, Image, TextMeshProUGUI) Btn(Transform parent, string name, float x, float y, float w, float h, string label, string method)
    {
        var bg = Img(parent, name, x, y, w, h, new Color(1f, 1f, 1f, 0.08f), spr); bg.raycastTarget = true;
        var t = Txt(bg.transform, "Label", 0, 0, w, h, label, 30, WHITE, TextAlignmentOptions.Center);
        var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        Wire(b.onClick, method);
        return (b, bg, t);
    }

    // ── 렌더 ──
    static void Shots(PyriteProjector pj, GameObject ui, PyriteDayCycle cyc, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var root = pj.transform; var panel = root.Find("Panel");
        var dir = panel.forward; dir.y = 0; dir.Normalize();
        var terr = Terrain.activeTerrain;
        var stand = root.position - dir * 1.0f;
        stand.y = (terr ? terr.SampleHeight(stand) + terr.transform.position.y : root.position.y - 0.4f) + 1.6f;
        var near = panel.position - panel.forward * 2.4f;
        Directory.CreateDirectory("Assets/_preview/projector/");
        foreach (var o in pj.onObjects) if (o != null) o.SetActive(true);
        pj.lensRenderer.sharedMaterial = pj.lensOn;
        try
        {
            cyc.ResetCache();
            foreach (var (h, lang) in new[] { (21f, 0), (21f, 1), (12f, 0) })
            {
                cyc.EvaluateAt(h);
                foreach (var x in loc) x.t.text = lang == 1 ? x.ko : x.en;
                var st = ui.GetComponent<PyriteSettings>();
                if (st != null) { st.timeText.text = string.Format("{0:00}:00", (int)h); st.timeSlider.SetValueWithoutNotify(h * 60f); }
                string tag = string.Format("{0:00}_{1}", (int)h, lang == 1 ? "ko" : "en");
                Shot(cam, stand, panel.position, 60f, "Assets/_preview/projector/ui_" + tag + "_table.png");
                Shot(cam, near, panel.position, 40f, "Assets/_preview/projector/ui_" + tag + "_panel.png");
            }
            sb.AppendLine("  shots ui_{21_en,21_ko,12_en}_{table,panel}");
        }
        finally
        {
            foreach (var x in loc) x.t.text = x.en;
            var st0 = ui.GetComponent<PyriteSettings>();
            if (st0 != null) { st0.timeText.text = "21:00"; st0.timeSlider.SetValueWithoutNotify(1260f); }
            foreach (var o in pj.onObjects) if (o != null) o.SetActive(false);
            pj.lensRenderer.sharedMaterial = pj.lensOff;
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int SW = 1280, SH = 720;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1000f;
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(SW, SH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(SW, SH, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, SW, SH), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static GameObject Find(string n) => GameObject.Find(n) ?? Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(x => x.name == n && x.scene.IsValid());
    static string PathOf(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_settings.txt", sb.ToString()); }
}
#endif
