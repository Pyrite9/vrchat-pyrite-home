// Tools ▸ Pyrite ▸ Z25a. Build Settings UI  /  Z25b. Settings UI Revert
//  프로젝터 패널(3.0×1.69 m) 앞에 월드 캔버스(1920×1080 px, 1 px = 1.5625 mm)를 띄우고 설정 6칸을 만든다
//   시간(분 슬라이더·자동 흐름, 모두에게 공유) · 화면(밝기 ±1 EV, 블룸) · 반사(호수·캠프 거울) · 소리(자연 소리 0~150%) · 성능(꽃·반딧불·해 그림자) · 정보(환경)
//   거울: 반사 칸 '거울' 토글 = 타프 줄 손거울(Z27b)과 같은 거울. 예전 캠프 거울 기둥은 끔
//   머리줄: 상호작용(팝업, 2026-09-24) · 사용한 에셋(팝업, 바깥·× 로 닫힘, 메인이 꺼지면 같이 꺼짐) · EN/KO · 닫기
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
    [MenuItem("Tools/Pyrite2/Z25d. Build Settings UI (= Z25a) %&#2", false, 93)]
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
        L(Txt(t, "Title", 90, 52, 820, 76, "", 62, GOLD), S["title"]);
        L(Txt(t, "Subtitle", 94, 128, 700, 42, "", 30, GREY), S["subtitle"]);
        var (_, _, guideLabel) = Btn(t, "GuideButton", 936, 66, 240, 66, "", "OpenGuide"); L(guideLabel, S["guide"]);
        var (_, _, assetsLabel) = Btn(t, "AssetsButton", 1196, 66, 240, 66, "", "OpenAssets"); L(assetsLabel, S["assets"]);
        var (bEn, iEn, _) = Btn(t, "LangEN", 1464, 66, 116, 66, "EN", "SetEN");
        var (bKo, iKo, _) = Btn(t, "LangKO", 1590, 66, 116, 66, "KO", "SetKO");
        Btn(t, "Close", 1740, 66, 90, 66, "×", "Close");
        Img(t, "Divider", 90, 185, 1740, 3, new Color(GOLD.r, GOLD.g, GOLD.b, 0.35f), null);

        // 칸 (3 × 2) — Content 아래 (거울 모드에서 통째로 숨김)
        var content = Rect(t, "Content", 0, 0, W, H);
        const float cw = 553f, ch = 370f, gx = 40f, top = 215f, gy = 40f;
        Transform Card(string n, int c, int r, string key)
        {
            float x = 90f + c * (cw + gx), y = top + r * (ch + gy);
            var card = Img(content, n, x, y, cw, ch, CARD, spr).transform;
            L(Txt(card, "Head", 32, 24, cw - 64, 44, "", 30, GOLD, spacing: 6f), S[key]);
            return card;
        }

        // 시간
        var cTime = Card("CardTime", 0, 0, "time");
        var timeText = Txt(cTime, "Clock", 32, 70, 489, 130, "21:00", 108, WHITE);
        var timeSlider = Sld(cTime, "TimeSlider", 32, 212, 489, 0f, 1439f, 1260f, true, "OnTime");
        Txt(cTime, "Min", 32, 254, 150, 28, "00:00", 20, GREY);
        Txt(cTime, "Max", 371, 254, 150, 28, "24:00", 20, GREY, TextAlignmentOptions.Right);
        var (autoT, autoL) = Tgl(cTime, "Auto", 32, 282, 489, true, "OnAuto"); L(autoL, S["auto"]);
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
        var (mirT, mirL) = Tgl(cRefl, "MirrorToggle", 32, 160, 489, false, "OnCampMirror"); L(mirL, S["mirror"]);

        // 소리
        var cSound = Card("CardSound", 0, 1, "sound");
        L(Txt(cSound, "SoundLabel", 32, 82, 360, 44, "", 30, WHITE), S["nature"]);
        var soundText = Txt(cSound, "SoundValue", 381, 82, 140, 44, "100%", 30, GOLD, TextAlignmentOptions.Right);
        var soundSlider = Sld(cSound, "SoundSlider", 32, 136, 489, 0f, 1.5f, 1f, false, "OnSound");
        L(Txt(cSound, "Note", 32, 184, 489, 28, "", 20, GREY), S["soundNote"]);

        // 성능
        var cPerf = Card("CardPerf", 1, 1, "perf");
        // 성능 칸 (2026-09-24 관리자): 꽃 켬/끔 → 꽃 보이는 거리 바, 불빛(점광) 그림자 켬/끔 추가
        L(Txt(cPerf, "FlowerDistLabel", 32, 76, 360, 44, "", 30, WHITE), S["flowerDist"]);
        var flDistText = Txt(cPerf, "FlowerDistValue", 380, 76, 141, 44, "All", 28, GOLD, TextAlignmentOptions.Right);
        var flDist = Sld(cPerf, "FlowerDist", 32, 122, 489, 0f, 16f, 16f, true, "OnFlowerDist");
        var (ffT, ffL) = Tgl(cPerf, "Fireflies", 32, 170, 489, true, "OnFireflies"); L(ffL, S["fireflies"]);
        var (shT, shL) = Tgl(cPerf, "Shadows", 32, 224, 489, true, "OnShadows"); L(shL, S["shadows"]);
        var (lsT, lsL) = Tgl(cPerf, "LightShadows", 32, 278, 489, true, "OnLightShadows"); L(lsL, S["lightShadows"]);
        L(Txt(cPerf, "Note", 32, 336, 489, 28, "", 18, GREY, TextAlignmentOptions.TopLeft), S["perfNote"]);
        Toggle flT = null;

        // 정보
        var cAbout = Card("CardAbout", 2, 1, "about");
        L(Txt(cAbout, "World", 32, 80, 489, 56, "", 40, WHITE), S["world"]);
        L(Txt(cAbout, "Body", 32, 146, 489, 150, "", 22, GREY, TextAlignmentOptions.TopLeft), S["aboutBody"]);
        L(Txt(cAbout, "Credit", 32, 312, 489, 32, "", 22, GOLD), S["credit"]);

        // (거울 모드 폐기 2026-09-24 — 거울은 타프 줄 손거울 Z27b. 반사 칸 '거울' 토글은 그 거울을 켠다)
        var oldPm = root.transform.Find("PanelMirror"); if (oldPm != null) Object.DestroyImmediate(oldPm.gameObject);

        // 사용한 에셋 팝업 (캔버스 자식 → 메인이 꺼지면 같이 꺼짐. 바깥 어둠을 누르면 닫힘)
        var popup = Rect(t, "AssetsPopup", 0, 0, W, H).gameObject;
        var (_, dim, _) = Btn(popup.transform, "Dim", 0, 0, W, H, "", "CloseAssets");
        dim.sprite = null; dim.type = Image.Type.Simple; dim.color = new Color(0f, 0f, 0f, 0.8f);   // 선형 색공간이라 알파 0.55 는 거의 안 어두워 보였다(아래 글자 밝기 0.70)
        var cb = dim.GetComponent<Button>().colors; cb.highlightedColor = Color.white; cb.pressedColor = Color.white; dim.GetComponent<Button>().colors = cb;
        const float bx = 300f, by = 150f, bw = 1320f, bh = 790f;
        var box = Img(popup.transform, "Box", bx, by, bw, bh, new Color(0.045f, 0.05f, 0.06f, 1f), spr); box.raycastTarget = true;
        Img(box.transform, "Edge", 0, 0, bw, 3, new Color(GOLD.r, GOLD.g, GOLD.b, 0.6f), null);
        L(Txt(box.transform, "Title", 60, 40, 900, 60, "", 42, GOLD, spacing: 6f), S["assetsTitle"]);
        Btn(box.transform, "Close", bw - 60 - 90, 38, 90, 66, "×", "CloseAssets");
        Img(box.transform, "Divider", 60, 124, bw - 120, 2, new Color(GOLD.r, GOLD.g, GOLD.b, 0.3f), null);
        for (int i = 0; i < ASSETS.Length; i++)
        {
            float ry = 146f + i * 64f;
            Txt(box.transform, "Name" + i, 60, ry, 560, 60, ASSETS[i].name, 30, WHITE);
            Txt(box.transform, "Author" + i, 640, ry, 330, 60, ASSETS[i].author, 26, GOLD);
            L(Txt(box.transform, "Use" + i, 990, ry, 270, 60, "", 24, GREY, TextAlignmentOptions.Right), S[ASSETS[i].use]);
        }
        L(Txt(box.transform, "Foot", 60, 680, bw - 120, 50, "", 24, GREY), S["assetsFoot"]);
        popup.SetActive(false);

        // 상호작용 가능한 사물 팝업 (2026-09-24 관리자 요청) — 같은 방식: 캔버스 자식, 바깥 어둠·× 로 닫힘
        var guide = Rect(t, "GuidePopup", 0, 0, W, H).gameObject;
        {
            var (_, gdim, _) = Btn(guide.transform, "Dim", 0, 0, W, H, "", "CloseGuide");
            gdim.sprite = null; gdim.type = Image.Type.Simple; gdim.color = new Color(0f, 0f, 0f, 0.8f);
            var gcb = gdim.GetComponent<Button>().colors; gcb.highlightedColor = Color.white; gcb.pressedColor = Color.white; gdim.GetComponent<Button>().colors = gcb;
            const float gx0 = 200f, gy0 = 80f, gw = 1520f, gh = 920f;
            var gbox = Img(guide.transform, "Box", gx0, gy0, gw, gh, new Color(0.045f, 0.05f, 0.06f, 1f), spr); gbox.raycastTarget = true;
            Img(gbox.transform, "Edge", 0, 0, gw, 3, new Color(GOLD.r, GOLD.g, GOLD.b, 0.6f), null);
            L(Txt(gbox.transform, "Title", 60, 36, 1100, 60, "", 42, GOLD, spacing: 6f), S["guideTitle"]);
            Btn(gbox.transform, "Close", gw - 60 - 90, 34, 90, 66, "×", "CloseGuide");
            Img(gbox.transform, "Divider", 60, 116, gw - 120, 2, new Color(GOLD.r, GOLD.g, GOLD.b, 0.3f), null);
            for (int i = 0; i < GUIDE.Length; i++)
            {
                int col = i / 7, row = i % 7;
                float ix = 60f + col * 720f, iy = 136f + row * 104f;   // 두 줄 설명이 다음 이름에 붙지 않게 104
                L(Txt(gbox.transform, "Name" + i, ix, iy, 680, 36, "", 28, GOLD), S[GUIDE[i] + "N"]);
                var d = Txt(gbox.transform, "Desc" + i, ix, iy + 36, 680, 54, "", 21, GREY); d.alignment = TextAlignmentOptions.TopLeft;
                L(d, S[GUIDE[i] + "D"]);
            }
            L(Txt(gbox.transform, "Foot", 60, 860, gw - 120, 40, "", 22, GREY), S["guideFoot"]);
        }
        guide.SetActive(false);

        foreach (var x in loc) x.t.text = x.en;

        // 6) 연결
        var toggles = Object.FindObjectsOfType<PyriteMirrorToggle>(true);
        var lakeMirror = toggles.FirstOrDefault(m => m.name.Contains("Lake") || (m.mirror != null && m.mirror.name.Contains("Lake")));
        var campMirror = toggles.FirstOrDefault(m => m.name == "TarpMirror");
        if (campMirror == null) sb.AppendLine("TarpMirror 없음 — Z27b 먼저 (거울 토글은 동작 안 함)");
        sb.AppendLine("mirror toggles: " + string.Join(", ", toggles.Select(m => m.name + "→" + (m.mirror ? m.mirror.name : "null") + " startOn " + m.startOn)));

        // 자동 흐름 기본 켬 (2026-09-24 관리자) — 동기화 필드 기본값. 방장 입장 시 21:00 부터 흐른다
        cyc.autoFlow = true; UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc);
        st.cycle = cyc; st.projector = pj;
        st.timeSlider = timeSlider; st.timeText = timeText; st.autoToggle = autoT;
        st.brightSlider = brightSlider; st.ppBright = vBright; st.ppDark = vDark; st.bloomToggle = bloomT; st.ppNoBloom = vNoBloom;
        st.lakeToggle = lakeT; st.lakeMirror = lakeMirror; st.campMirrorToggle = mirT; st.campMirror = campMirror;
        st.soundSlider = soundSlider; st.soundText = soundText;
        st.flowersToggle = flT; st.firefliesToggle = ffT; st.shadowsToggle = shT;
        st.flowerDistSlider = flDist; st.flowerDistText = flDistText; st.lightShadowsToggle = lsT;
        st.shadowLights = Object.FindObjectsOfType<Light>(true).Where(l => l.type == LightType.Point && l.shadows != LightShadows.None).ToArray();
        {   // 꽃밭 거리 컬링 — 항상 켜져 있는 꽃밭 루트에
            var ff = GameObject.Find("FlowerField");
            var fc = ff != null ? ff.GetComponent<PyriteFlowerCull>() : null;
            if (ff != null && fc == null) fc = UdonSharpUndo.AddComponent<PyriteFlowerCull>(ff);
            if (fc != null)
            {
                fc.renderers = (cyc.flowerRenderers ?? new Renderer[0]).Where(r => r != null).ToArray();
                fc.maxDistance = 160f; fc.distance = 160f;
                UdonSharpEditorUtility.CopyProxyToUdon(fc); EditorUtility.SetDirty(fc);
            }
            st.flowerCull = fc;
            sb.AppendLine("flower cull: " + (fc != null ? fc.renderers.Length + " renderers on " + ff.name : "FlowerField 없음") + ", light shadows: " + string.Join(", ", st.shadowLights.Select(l => l.name + " " + l.shadows)));
        }
        st.flowerRenderers = (cyc.flowerRenderers ?? new Renderer[0]).Where(r => r != null).ToArray();
        st.fireflyRenderers = (cyc.fireflies ?? new ParticleSystem[0]).Where(p => p != null).Select(p => (Renderer)p.GetComponent<ParticleSystemRenderer>()).Where(r => r != null).ToArray();
        st.sunLight = cyc.sun;
        st.texts = loc.Select(x => x.t).ToArray();
        st.textEn = loc.Select(x => x.en).ToArray();
        st.textKo = loc.Select(x => x.ko).ToArray();
        st.langEnBg = iEn; st.langKoBg = iKo; st.lang = 0;
        st.assetsPopup = popup; st.guidePopup = guide;
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
        // 예전 캠프 거울(기둥·스위치·거울면) 통째로 끔 — 설정 빔 거울로 대체
        // 🔴 Find("Mirror") 는 설정 칸의 'Mirror' 토글을 잡을 수 있다 → 씬 루트에서만 찾는다
        var oldStand = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Mirror");
        if (oldStand != null && oldStand.activeSelf)
        {
            oldStand.SetActive(false);
            var lines = File.Exists(HIDDEN_LOG) ? File.ReadAllLines(HIDDEN_LOG).Where(l => !l.Contains("SettingsUI")).ToList() : new List<string>();
            lines.Add("A|" + PathOf(oldStand.transform));
            File.WriteAllLines(HIDDEN_LOG, lines.Distinct());
            sb.AppendLine("camp mirror stand 'Mirror' off");
        }

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
            var pm = root.transform.Find("PanelMirror"); if (pm != null) { Object.DestroyImmediate(pm.gameObject); sb.AppendLine("PanelMirror 삭제"); }
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
                if (p[0] == "A") tr.gameObject.SetActive(true);
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
        ["title"] = ("PYRITE LAKE", "파이라이트 호수"),
        ["subtitle"] = ("Settings", "설정"),
        ["time"] = ("TIME", "시간"),
        ["auto"] = ("Time flows", "시간 흐름"),
        ["timeNote"] = ("Shared with everyone · 1 day = 12 min", "모두 함께 흐름 · 하루 = 12분"),
        ["view"] = ("VIEW", "화면"),
        ["bright"] = ("Brightness", "밝기"),
        ["darker"] = ("Darker", "어둡게"),
        ["brighter"] = ("Brighter", "밝게"),
        ["bloom"] = ("Bloom", "블룸 효과"),
        ["refl"] = ("REFLECTIONS", "반사"),
        ["lake"] = ("Lake reflection", "호수 반사"),
        ["mirror"] = ("Mirror", "거울"),
        ["sound"] = ("SOUND", "소리"),
        ["nature"] = ("Nature sounds", "자연 소리"),
        ["soundNote"] = ("Campfire · lake · crickets", "모닥불 · 호수 · 풀벌레"),
        ["perf"] = ("PERFORMANCE", "성능"),
        ["flowers"] = ("Flowers", "꽃"),
        ["fireflies"] = ("Fireflies", "반딧불이"),
        ["shadows"] = ("Sun shadows", "햇빛 그림자"),
        ["perfNote"] = ("Turn these off if your frame rate drops.", "프레임이 떨어지면 꺼 보세요."),
        ["flowerDist"] = ("Flower distance", "꽃 보이는 거리"),
        ["flowerOff"] = ("Off m", "끔"),
        ["flowerAll"] = ("All", "전부"),
        ["lightShadows"] = ("Firelight shadows", "불빛 그림자"),
        ["about"] = ("ABOUT", "정보"),
        ["world"] = ("Pyrite Lake", "파이라이트 호수"),
        ["aboutBody"] = ("Pyrite, lake, and basalt columns.\nA lakeside camp ringed by columnar cliffs,\nnemophila fields, and fireflies.\nA full day passes every 12 minutes.", "황철석과 호수와 주상절리.\n기둥 절벽에 둘러싸인 호숫가 캠프,\n네모필라 꽃밭과 반딧불이.\n12분마다 하루가 흐릅니다."),
        ["assets"] = ("Used assets", "사용한 에셋"),
        ["assetsTitle"] = ("USED ASSETS", "사용한 에셋"),
        ["aFlowers"] = ("Flowers & grass", "꽃 · 풀"),
        ["aCamp"] = ("Camp props & campfire", "캠프 소품 · 모닥불"),
        ["aWater"] = ("Lake water", "호수 물"),
        ["aBoat"] = ("Rowboat", "나룻배"),
        ["aSky"] = ("Sky", "하늘"),
        ["aTV"] = ("Video player", "영상 플레이어"),
        ["aFont"] = ("Font", "글꼴"),
        ["aCode"] = ("Scripting", "스크립트"),
        ["assetsFoot"] = ("Terrain, crystals, dock, and shaders by Pyrite9.", "지형 · 결정 · 부두 · 셰이더는 Pyrite9 가 직접 만들었습니다."),
        ["credit"] = ("Made by Pyrite9", "제작 Pyrite9"),
        ["guide"] = ("Interactions", "상호작용"),
        ["guideTitle"] = ("THINGS YOU CAN USE", "상호작용 가능한 사물"),
        ["gSetN"] = ("Settings projector", "설정 프로젝터"),
        ["gSetD"] = ("On the table. Press to open this panel. Only you see it.", "테이블 위. 누르면 이 패널이 열립니다. 나에게만 보입니다."),
        ["gVidN"] = ("Video projector", "영상 프로젝터"),
        ["gVidD"] = ("Next to it. Press to turn the screen on or off for everyone.", "그 옆. 누르면 모두의 스크린이 켜지고 꺼집니다."),
        ["gMirN"] = ("Hand mirror", "손거울"),
        ["gMirD"] = ("Hangs on the tarp rope. Press for a mirror. Only you see it.", "타프 줄에 걸려 있습니다. 누르면 거울이 켜집니다. 나에게만 보입니다."),
        ["gLanN"] = ("Lanterns", "랜턴"),
        ["gLanD"] = ("Grab to carry. Drop near a hook to hang it.", "들고 다닐 수 있습니다. 걸이 근처에서 놓으면 걸립니다."),
        ["gChrN"] = ("Chairs", "의자"),
        ["gChrD"] = ("Grab the backrest to move. Press the seat to sit.", "등받이를 잡아 옮기고, 앉는 자리를 누르면 앉습니다."),
        ["gCotN"] = ("Cot", "야전침대"),
        ["gCotD"] = ("Press to lie down.", "누르면 눕습니다."),
        ["gMatN"] = ("Picnic mat", "돗자리"),
        ["gMatD"] = ("Press to lie down. Grab the foot end to move it.", "누르면 눕습니다. 발치 쪽을 잡으면 옮길 수 있습니다."),
        ["gSkwN"] = ("Marshmallows", "마시멜로"),
        ["gSkwD"] = ("Hold over the fire to roast. Use to eat, use again for a new one.", "불에 대면 익습니다. 사용하면 먹고, 다시 사용하면 새로 꽂힙니다."),
        ["gKetN"] = ("Kettle & stove", "주전자 · 스토브"),
        ["gKetD"] = ("Steams on the stove. Use over a mug to pour.", "스토브 위에서 김이 납니다. 머그 위에서 사용하면 따릅니다."),
        ["gMugN"] = ("Mugs", "머그"),
        ["gMugD"] = ("Use to take a sip. Six sips empty it.", "사용하면 한 모금. 여섯 모금이면 비웁니다."),
        ["gScpN"] = ("Telescope", "망원경"),
        ["gScpD"] = ("Bring your face to the eyepiece. The tube follows your view.", "접안렌즈에 얼굴을 대면 경통이 시선을 따라 돕니다."),
        ["gStnN"] = ("Skipping stones", "물수제비 돌"),
        ["gStnD"] = ("On the dock tray. Throw low and fast across the water.", "부두 끝 쟁반에 있습니다. 물 위로 낮고 빠르게 던지세요."),
        ["gBtN"] = ("Rowboat", "나룻배"),
        ["gBtD"] = ("Press a seat to sit.", "자리를 누르면 앉습니다."),
        ["guideFoot"] = ("Grab, Use, and Drop are your usual VRChat pickup controls.", "잡기 · 사용 · 놓기는 VRChat 기본 조작입니다."),
    };

    // 상호작용 사물 목록 (문구 키 앞부분 — N 이름 / D 설명). 왼쪽 열 7개 → 오른쪽 열
    static readonly string[] GUIDE = { "gSet", "gVid", "gMir", "gLan", "gChr", "gCot", "gMat", "gSkw", "gKet", "gMug", "gScp", "gStn", "gBt" };

    static void L(TextMeshProUGUI t, (string en, string ko) s) { loc.Add((t, s.en, s.ko)); }

    // 사용한 에셋: (이름, 제작자, 용도 키)
    static readonly (string name, string author, string use)[] ASSETS =
    {
        ("FlowersGrassland", "©つきのすとあ", "aFlowers"),
        ("キャンプ＆焚火", "のあがみ", "aCamp"),
        ("水面シェーダー", "サカナ", "aWater"),
        ("木製ボート", "ootwn", "aBoat"),
        ("Sorafield Atmosphere Sky", "Sorafield", "aSky"),
        ("ProTV", "ArchiTech", "aTV"),
        ("Noto Sans KR", "Google Fonts · OFL", "aFont"),
        ("UdonSharp · VRChat SDK", "Merlin · VRChat", "aCode"),
    };

    // ── 글꼴 ──
    static TMP_FontAsset BuildFont(Dictionary<string, (string en, string ko)> S, StringBuilder sb)
    {
        // TTF 를 에디터가 켜진 채 교체하면 FreeType 이 옛 파일 핸들로 새 바이트를 읽어 글리프가 깨진다 → 강제 재임포트 + 네이티브 face 캐시 비우기
        AssetDatabase.ImportAsset(TTF, ImportAssetOptions.ForceUpdate);
        UnityEngine.TextCore.LowLevel.FontEngine.UnloadAllFontFaces();
        var src = AssetDatabase.LoadAssetAtPath<Font>(TTF);
        if (src == null) { sb.AppendLine("글꼴 없음: " + TTF); return null; }
        var set = new HashSet<char>();
        for (int c = 32; c < 127; c++) set.Add((char)c);
        foreach (var kv in S) { foreach (var c in kv.Value.en) set.Add(c); foreach (var c in kv.Value.ko) set.Add(c); }
        foreach (var c in "×·%:0123456789") set.Add(c);
        foreach (var a in ASSETS) { foreach (var c in a.name) set.Add(c); foreach (var c in a.author) set.Add(c); }
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
                if (h > 20f && st != null && st.assetsPopup != null)
                {
                    st.assetsPopup.SetActive(true);
                    Shot(cam, near, panel.position, 40f, "Assets/_preview/projector/ui_" + tag + "_assets.png");
                    st.assetsPopup.SetActive(false);
                }
                if (h > 20f && st != null && st.guidePopup != null)
                {
                    st.guidePopup.SetActive(true);
                    Shot(cam, near, panel.position, 40f, "Assets/_preview/projector/ui_" + tag + "_guide.png");
                    st.guidePopup.SetActive(false);
                }
            }
            sb.AppendLine("  shots ui_{21_en,21_ko,12_en}_{table,panel} + ui_21_{en,ko}_{assets,guide}");
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
