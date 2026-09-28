// PyriteBedroomPanelBuild.cs — 텐트 침실 머리맡 팝업 UI + 오른쪽 벽 전신거울 + 알람 (Z50a 빌드 / Z50b 되돌리기). 재실행 안전
//  2026-09-28 20:01 관리자: 머리맡 벽 팝업(심플, 배경 없이 흰 테두리) — 수면 모드 슬라이더 · 거울 토글(오른쪽 벽 가로로 긴 전신거울) · 알람시계(AM/PM·시·분 ▲▼, 꾹 누르면 연속)
//  결정: 벽의 흰 달 아이콘으로 열고 닫음 / 영어만 / 알람은 나만(로컬) / 수면 최대 = 실내 광원 5% + 창밖 40%
//  루트 TentBedroom/BedroomPanel: IconCanvas(0.14 m) · PanelCanvas(0.9×0.62 m, 머리맡 벽 기울기 따라) · Mirror(+X 벽, 2.4 m 폭) · U# PyriteBedroomPanel · AudioSource(알람, 2D, 로컬)
//  스프라이트는 코드로 그려 Assets/Bedroom/UI/*.png, 알람 소리는 코드로 합성 Assets/Audio/SFX/sfx_alarm_clock.wav (우리 소유)
//  ⚠ UI 기본 셰이더 → SDK 'Review Any Alerts' 의 UI 셰이더 Auto Fix 다시 필요할 수 있음
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
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VRC.SDK3.Components;
using VRC.Udon;

public static class PyriteBedroomPanelBuild
{
    const string UI_DIR = "Assets/Bedroom/UI/";
    const string FA = "Assets/Fonts/NotoSansKR/NotoSansKR-UI SDF.asset";
    const string WAV = "Assets/Audio/SFX/sfx_alarm_clock.wav";
    const string PREV = "Assets/_preview/bedroom/";
    const string ROOT = "BedroomPanel";
    const string TV_PREFAB = "Packages/dev.architech.protv/Simple (ProTV).prefab";
    const float TV_W = 2.0f, TV_Y = 1.20f, TV_Z = -0.90f, TV_GAP = 0.06f;   // 21:23 관리자: 침대 왼쪽(입구 쪽, −X 벽), 수직
    const float PW = 900f, PH = 740f, PX = 0.001f;          // 패널 캔버스 px, 1 px = 1 mm
    const float PANEL_Y = 0.86f, ICON_Y = 0.80f, ICON_X = 0.60f, WALL_GAP = 0.07f;   // 21:58 자연 소리 줄 추가로 패널 +120 px → 중심 0.86 (아래 끝 0.49 그대로)          // 20:48 관리자: 통째로 내려서 침대 조금 위 (패널 아래 끝 0.49 m, 매트 윗면 ~0.36 m). 아이콘은 패널 오른쪽
    const float MIR_W = 2.4f, MIR_Z = -0.6f, MIR_Y0 = 0.15f, MIR_Y1 = 1.75f, MIR_GAP = 0.06f;   // 20:48 관리자: 수직으로, HQ(전체 반사)
    static readonly Color W = new Color(0.96f, 0.96f, 0.95f, 1f);
    static readonly Color W2 = new Color(0.96f, 0.96f, 0.95f, 0.55f);
    static StringBuilder sb;
    static TMP_FontAsset font;
    static Sprite sOutline, sRing, sMoon, sUp, sDown, sDot, sFill, sVideo;
    static UdonBehaviour ub;

    [MenuItem("Tools/Pyrite3/Z50a. Bedroom Panel Build", false, 5001)]
    public static void Build()
    {
        sb = new StringBuilder("[Z50a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z50b. Bedroom Panel Revert", false, 5002)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z50b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom");
        var t = room ? room.transform.Find(ROOT) : null;
        if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(ROOT + " 삭제"); }
        var m = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat"); if (m && m.HasProperty("_Dim")) { m.SetFloat("_Dim", 1f); EditorUtility.SetDirty(m); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (스프라이트·소리 에셋은 남김)");
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

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
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Ctrl+R 후 Z50a 다시");
        return false;
    }

    // ───────────────────────── 빌드 ─────────────────────────
    static bool Inner()
    {
        if (!EnsureProgram("PyriteBedroomPanel")) return false;
        var room = Root("TentBedroom"); if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return false; }
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FA); if (font == null) { sb.AppendLine("!! 글꼴 없음 " + FA); return false; }
        string need = "BEDROOMSLPMIRATNUD0123456789:%× NO";
        foreach (var c in need) if (!font.HasCharacter(c)) sb.AppendLine("!! 글꼴에 없는 글자 '" + c + "'");
        var backdrop = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat");
        if (backdrop == null || !backdrop.HasProperty("_Dim")) { sb.AppendLine("!! M_Backdrop 에 _Dim 없음 (셰이더 갱신 전?)"); return false; }

        MakeSprites();
        var clip = MakeAlarmWav();

        var o = room.transform;
        var old = o.Find(ROOT); if (old) Object.DestroyImmediate(old.gameObject);
        var rootGo = new GameObject(ROOT); rootGo.transform.SetParent(o, false);

        // 머리맡 벽(−Z) 기울기
        float s0 = PyriteBedroomBuild.SurfZ(0, PANEL_Y - 0.15f), s1 = PyriteBedroomBuild.SurfZ(0, PANEL_Y + 0.15f);
        var up = new Vector3(0, 0.30f, s0 - s1).normalized;               // 위로 갈수록 방 안쪽(+Z)으로 기움
        var nRoom = new Vector3(0, -up.z, up.y);                          // 방 쪽 법선 (살짝 아래를 봄)
        Vector3 WallPt(float x, float y, float halfW, float halfH) => new Vector3(x, y, -Mathf.Min(Mathf.Min(PyriteBedroomBuild.SurfZ(x + halfW, y + halfH), PyriteBedroomBuild.SurfZ(x - halfW, y + halfH)), Mathf.Min(PyriteBedroomBuild.SurfZ(x + halfW, y - halfH), PyriteBedroomBuild.SurfZ(x - halfW, y - halfH))) + WALL_GAP);
        var rot = Quaternion.LookRotation(-nRoom, up);
        sb.AppendLine("머리맡 벽 기울기 " + (Mathf.Atan2(up.z, up.y) * Mathf.Rad2Deg).ToString("F1") + "° (위가 방 쪽)");

        // 아이콘
        var icon = MakeCanvas(rootGo.transform, "IconCanvas", 140, 140, WallPt(ICON_X, ICON_Y, 0.07f, 0f), rot);
        var iconImg = Img(icon, "Moon", 0, 0, 140, 140, W, sMoon); iconImg.raycastTarget = true;
        var iconBtn = iconImg.gameObject.AddComponent<Button>(); iconBtn.targetGraphic = iconImg; iconBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = iconBtn.colors; cb.highlightedColor = new Color(1, 1, 1, 0.8f); cb.pressedColor = new Color(1, 1, 1, 0.6f); iconBtn.colors = cb;

        // 동영상 아이콘 (달 아이콘 위)
        var vicon = MakeCanvas(rootGo.transform, "VideoIconCanvas", 140, 140, WallPt(ICON_X, ICON_Y + 0.17f, 0.07f, 0f), rot);
        var vImg = Img(vicon, "Video", 0, 0, 140, 140, W, sVideo); vImg.raycastTarget = true;
        var vBtn = vImg.gameObject.AddComponent<Button>(); vBtn.targetGraphic = vImg; vBtn.navigation = new Navigation { mode = Navigation.Mode.None };
        var vcb = vBtn.colors; vcb.highlightedColor = new Color(1, 1, 1, 0.8f); vcb.pressedColor = new Color(1, 1, 1, 0.6f); vBtn.colors = vcb;

        // 패널
        var pc = MakeCanvas(rootGo.transform, "PanelCanvas", PW, PH, WallPt(0f, PANEL_Y, PW * PX * 0.5f, 0f), rot);
        Img(pc, "Border", 0, 0, PW, PH, W, sOutline);
        Txt(pc, "Title", 40, 22, 400, 56, "BEDROOM", 30, W, TextAlignmentOptions.MidlineLeft, 10f);
        var closeB = Btn(pc, "Close", PW - 86, 22, 56, 56, "×", 36);
        Img(pc, "Div1", 40, 92, PW - 80, 2, W2, sFill);

        Txt(pc, "SleepLabel", 40, 116, 500, 48, "SLEEP MODE", 30, W, TextAlignmentOptions.MidlineLeft, 6f);
        var sleepVal = Txt(pc, "SleepValue", PW - 240, 116, 200, 48, "0%", 30, W, TextAlignmentOptions.MidlineRight);
        var sleep = Sld(pc, "SleepSlider", 40, 172, PW - 80);

        Txt(pc, "NatureLabel", 40, 236, 500, 48, "NATURE SOUNDS", 30, W, TextAlignmentOptions.MidlineLeft, 6f);
        var natureVal = Txt(pc, "NatureValue", PW - 240, 236, 200, 48, "50%", 30, W, TextAlignmentOptions.MidlineRight);
        var nature = Sld(pc, "NatureSlider", 40, 292, PW - 80); nature.value = 0.5f;
        var (mirT, _) = Tgl(pc, "MirrorToggle", 40, 356, 400, "MIRROR");
        Img(pc, "Div2", 40, 438, PW - 80, 2, W2, sFill);

        var clock = Txt(pc, "Clock", 40, 458, 420, 96, "12:00 AM", 76, W, TextAlignmentOptions.MidlineLeft);
        Txt(pc, "ClockNote", 42, 552, 420, 32, "NOW", 22, W2, TextAlignmentOptions.MidlineLeft, 6f);
        var (alarmT, _) = Tgl(pc, "AlarmToggle", 40, 610, 400, "ALARM");
        var stop = Btn(pc, "Stop", 40, 668, 380, 56, "STOP", 30);

        // 알람 시각: 세로 3칸 (AM/PM · 시 · 분), 각 칸 ▲ 값 ▼
        float colY = 460f, colW = 110f;
        float[] cx = { 480f, 620f, 760f };
        Txt(pc, "Colon", cx[1] + colW - 4, colY + 70, 40, 90, ":", 60, W, TextAlignmentOptions.Center);
        var ampm = Col(pc, "AmPm", cx[0], colY, colW, "AM", "AmPm", "AmPm", false);
        var hour = Col(pc, "Hour", cx[1], colY, colW, "07", "HourUp", "HourDown", true);
        var min = Col(pc, "Min", cx[2], colY, colW, "00", "MinUp", "MinDown", true);

        // 거울 (+X 벽)
        var mirror = BuildMirror(rootGo.transform);
        // 침실 TV (−X 벽)
        var tvRoot = BuildTV(rootGo.transform);
        // 자연 소리 (2D, 침실 안에서만)
        var (natNight, natDay) = BuildNature(rootGo.transform);

        // 소리
        var aud = rootGo.AddComponent<AudioSource>();
        aud.clip = clip; aud.loop = true; aud.playOnAwake = false; aud.spatialBlend = 0f; aud.volume = 0.55f; aud.dopplerLevel = 0f;
        var spatialType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "VRCSpatialAudioSource" && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract);
        if (spatialType != null)
        {
            var sp = rootGo.AddComponent(spatialType); var so = new SerializedObject(sp);
            void SetB(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else sb.AppendLine("  (spatial " + n + " 없음)"); }
            SetB("EnableSpatialization", false); SetB("UseAudioSourceVolumeCurve", true);
            var g = so.FindProperty("Gain"); if (g != null) g.floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // U#
        var pb = UdonSharpUndo.AddComponent<PyriteBedroomPanel>(rootGo);
        ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pb);
        pb.panel = pc.gameObject; pb.mirror = mirror; pb.tvRoot = tvRoot; pb.sleepSlider = sleep; pb.sleepValue = sleepVal; pb.natureSlider = nature; pb.natureValue = natureVal; pb.natureNight = natNight; pb.natureDay = natDay; pb.mirrorToggle = mirT;
        pb.clockText = clock; pb.alarmToggle = alarmT; pb.ampmText = ampm; pb.hourText = hour; pb.minText = min; pb.stopButton = stop.gameObject;
        pb.icon = iconImg; pb.alarmSource = aud; pb.backdrop = backdrop;
        pb.lights = o.Find("Lights") ? o.Find("Lights").GetComponentsInChildren<Light>(true) : new Light[0];
        { var st = o.Find("Mood/StarLamp/StarLight"); pb.starLight = st ? st.GetComponent<Light>() : null; }   // 무드등 (Z51i) — 있으면 연결
        pb.stringMat = o.Find("Mood/StringLights") ? AssetDatabase.LoadAssetAtPath<Material>(PyriteBedroomMood.DIR + "M_StringBulb.mat") : null;
        pb.starMax = PyriteBedroomMood.STAR_MAX; pb.stringEmit = PyriteBedroomMood.STRING_EMIT;
        UdonSharpEditorUtility.CopyProxyToUdon(pb); EditorUtility.SetDirty(pb);

        // 이벤트 연결 (U# 가 붙은 뒤)
        Wire(iconBtn.onClick, "OnIcon");
        Wire(vBtn.onClick, "OnVideo");
        Wire(closeB.onClick, "OnClose");
        Wire(stop.onClick, "StopAlarm");
        Wire(sleep.onValueChanged, "OnSleep");
        Wire(nature.onValueChanged, "OnNature");
        Wire(mirT.onValueChanged, "OnMirror");
        Wire(alarmT.onValueChanged, "OnAlarmToggle");
        foreach (var (n, m) in pending) Hook(n, m);
        pending.Clear();

        sb.AppendLine("광원 " + pb.lights.Length + " (" + string.Join(", ", pb.lights.Select(l => l.name + " " + l.intensity.ToString("F2"))) + ")");
        sb.AppendLine("아이콘 " + V(icon.position - o.position) + ", 패널 " + V(pc.position - o.position) + " 크기 " + (PW * PX) + "×" + (PH * PX) + " m");

        pc.gameObject.SetActive(false); stop.gameObject.SetActive(false); mirror.SetActive(false); if (tvRoot) tvRoot.SetActive(false);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        return true;
    }

    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";

    static GameObject BuildMirror(Transform parent)
    {
        var root = new GameObject("Mirror"); root.transform.SetParent(parent, false);
        float zA = MIR_Z - MIR_W / 2f, zB = MIR_Z + MIR_W / 2f;
        float xb = Mathf.Min(PyriteBedroomBuild.SurfX(zA, MIR_Y0), PyriteBedroomBuild.SurfX(zB, MIR_Y0)) - MIR_GAP;
        float xt = Mathf.Min(PyriteBedroomBuild.SurfX(zA, MIR_Y1 + 0.03f), PyriteBedroomBuild.SurfX(zB, MIR_Y1 + 0.03f)) - MIR_GAP - 0.03f;   // 벽이 위로 갈수록 안쪽 → 위 끝(테두리 포함)이 기준
        float h = MIR_Y1 - MIR_Y0; var up = Vector3.up;
        var n = Vector3.left;                                             // 방 쪽 (−X), 수직
        var center = new Vector3(xt, (MIR_Y0 + MIR_Y1) / 2f, MIR_Z);
        var rot = Quaternion.LookRotation(-n, up);                         // VRChat 거울: −forward = 반사면 법선 = n

        var mirrorType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "VRCMirrorReflection" && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract);
        var tarp = Root("TarpMirror");
        var src = tarp && mirrorType != null ? tarp.GetComponentInChildren(mirrorType, true) : null;
        GameObject surf;
        if (src != null)
        {
            surf = Object.Instantiate(src.gameObject, root.transform); surf.name = "Surface";
            foreach (Transform c in surf.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
            var mf = surf.GetComponent<MeshFilter>(); var b = mf && mf.sharedMesh ? mf.sharedMesh.bounds.size : Vector3.one;
            surf.transform.localPosition = center; surf.transform.localRotation = rot;
            surf.transform.localScale = new Vector3(MIR_W / Mathf.Max(b.x, 1e-3f), h / Mathf.Max(b.y, 1e-3f), 1f);
            sb.AppendLine("거울: TarpMirror 의 " + src.name + " 복제 (메시 " + (mf && mf.sharedMesh ? mf.sharedMesh.name : "?") + " " + b.ToString("F2") + ")");
            MirrorHQ(surf.GetComponent(mirrorType));
        }
        else
        {
            surf = GameObject.CreatePrimitive(PrimitiveType.Quad); surf.name = "Surface"; surf.transform.SetParent(root.transform, false);
            Object.DestroyImmediate(surf.GetComponent<Collider>());
            surf.transform.localPosition = center; surf.transform.localRotation = rot; surf.transform.localScale = new Vector3(MIR_W, h, 1f);
            if (mirrorType != null) MirrorHQ(surf.AddComponent(mirrorType));
            sb.AppendLine("!! TarpMirror 거울 원본 없음 → 새 VRCMirrorReflection (기본값)");
        }
        // 테두리·스탠드 없음 (21:13 관리자: 필요 없음)
        sb.AppendLine("거울 " + MIR_W + "×" + h.ToString("F2") + " m 수직(테두리 없음), 중심 " + V(center) + ", 위 끝 벽 틈 " + MIR_GAP + " m · 아래 끝 벽까지 " + (xb + MIR_GAP - xt).ToString("F2") + " m");
        return root;
    }

    // 자연 소리: 월드의 풀벌레(AMB_N_Crickets_A/B, 밤) · 물가(AMB_Water_ShoreN) 클립을 2D 반복으로. 볼륨은 U# 가 (슬라이더 × 기준 × 밤/낮) 로, 침실 밖이면 0
    static (AudioSource[], AudioSource[]) BuildNature(Transform parent)
    {
        var root = new GameObject("Nature"); root.transform.SetParent(parent, false);
        var all = Object.FindObjectsOfType<AudioSource>(true);
        var spType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "VRCSpatialAudioSource" && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract);
        AudioSource[] Make(string[] names)
        {
            var list = new List<AudioSource>();
            foreach (var n in names)
            {
                var src = all.FirstOrDefault(a => a.name == n && a.clip != null);
                if (src == null) { sb.AppendLine("  !! 자연 소리 원본 없음 " + n); continue; }
                var go = new GameObject(n.Replace("AMB_", "BR_")); go.transform.SetParent(root.transform, false);
                var a = go.AddComponent<AudioSource>();
                a.clip = src.clip; a.loop = true; a.playOnAwake = true; a.spatialBlend = 0f; a.volume = 0f; a.dopplerLevel = 0f; a.priority = 200;
                if (spType != null)
                {
                    var so = new SerializedObject(go.AddComponent(spType));
                    void SB(string pn, bool v) { var p = so.FindProperty(pn); if (p != null) p.boolValue = v; }
                    SB("EnableSpatialization", false); SB("UseAudioSourceVolumeCurve", true);
                    var g = so.FindProperty("Gain"); if (g != null) g.floatValue = 0f;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                sb.AppendLine("  자연 소리 " + go.name + " ← " + src.name + " (" + src.clip.name + ", " + src.clip.length.ToString("F1") + " s, 원본 볼륨 " + src.volume.ToString("F2") + ", 원본 공간 " + src.spatialBlend.ToString("F1") + ")");
                list.Add(a);
            }
            return list.ToArray();
        }
        return (Make(new[] { "AMB_N_Crickets_A" }), Make(new[] { "AMB_Water_ShoreN" }));   // Crickets_B 는 같은 클립(A_Crickets)이라 2D 로 겹치면 두 배 소리일 뿐
    }

    // 침실 전용 ProTV: Simple (ProTV) 프리팹, 관객 쪽 = 루트 로컬 −Z(유니티 Quad 앞면) → 방 안(+X)을 보게. 화면 폭 TV_W 로 균일 스케일, 화면 위·아래 모서리 중 벽에 가장 가까운 곳에서 TV_GAP
    static GameObject BuildTV(Transform parent)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TV_PREFAB);
        if (prefab == null) { sb.AppendLine("!! ProTV 프리팹 없음 " + TV_PREFAB); return null; }
        var tv = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        tv.name = "BedroomTV";
        tv.transform.localPosition = Vector3.zero; tv.transform.localRotation = Quaternion.LookRotation(Vector3.left, Vector3.up);   // 화면 쿼드 앞면 = 루트 −Z → 루트 +Z 를 벽(−X)으로 (Z50c 실측: +X 로 두면 뒷면이라 안 보임) tv.transform.localScale = Vector3.one;
        var room = parent.parent;
        var screen = tv.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0);
        if (screen == null) { sb.AppendLine("!! TV 화면 렌더러 없음"); Tree(tv.transform, 0); return tv; }
        Bounds LB(Renderer r) { var b = r.bounds; b.center = room.InverseTransformPoint(b.center); return b; }  // 방 회전 0 가정
        var b0 = LB(screen);
        float s = TV_W / Mathf.Max(b0.size.z, 1e-3f);
        tv.transform.localScale = Vector3.one * s;
        var b1 = LB(screen);
        float h = b1.size.y, zA = TV_Z - TV_W / 2f, zB = TV_Z + TV_W / 2f, y0 = TV_Y - h / 2f, y1 = TV_Y + h / 2f;
        float wall = Mathf.Min(Mathf.Min(PyriteBedroomBuild.SurfX(zA, y1), PyriteBedroomBuild.SurfX(zB, y1)), Mathf.Min(PyriteBedroomBuild.SurfX(zA, y0), PyriteBedroomBuild.SurfX(zB, y0)));
        var target = new Vector3(-wall + TV_GAP, TV_Y, TV_Z);
        tv.transform.localPosition += target - b1.center;
        SeparateAssets(tv);
        // 설정: 전역 비디오 텍스처(GSV)·F5 리로드는 캠프 TV 몫, 숨기면 멈춤
        var mgr = tv.GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().Name == "TVManager");
        if (mgr != null)
        {
            var so = new SerializedObject(mgr);
            void SB(string n, bool v) { var p = so.FindProperty(n); if (p != null) { sb.AppendLine("  TV " + n + " " + p.boolValue + " → " + v); p.boolValue = v; } else sb.AppendLine("  !! TV " + n + " 없음"); }
            SB("enableGSV", false); SB("enableReloadKeybind", false); SB("stopMediaWhenDisabled", true);
            var au = so.FindProperty("autoplayURL"); if (au != null) sb.AppendLine("  TV autoplayURL 있음(기본값 유지)");
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else sb.AppendLine("!! TVManager 없음");
        // 스피커: 캠프 TV(MediaPlayer) 같은 이름 스피커의 VRCSpatialAudioSource 설정 복사 (없으면 VRChat 에서 무음/경고 — 8절 함정)
        var spType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "VRCSpatialAudioSource" && typeof(Component).IsAssignableFrom(t) && !t.IsAbstract);
        var camp = Root("MediaPlayer");
        int copied = 0;
        if (spType != null && camp != null)
            foreach (var a in tv.GetComponentsInChildren<AudioSource>(true))
            {
                var src = camp.GetComponentsInChildren<AudioSource>(true).FirstOrDefault(x => x.name == a.name);
                var srcSp = src ? src.GetComponent(spType) : null;
                if (srcSp == null) continue;
                var dst = a.GetComponent(spType); if (dst == null) dst = a.gameObject.AddComponent(spType);   // ?? 금지(가짜 null)
                EditorUtility.CopySerialized(srcSp, dst); copied++;
            }
        sb.AppendLine("  TV 스피커 공간음향 복사 " + copied + "개 (캠프 MediaPlayer 기준)");
        sb.AppendLine("  TV 화면 셰이더 " + (screen.sharedMaterial ? screen.sharedMaterial.shader.name + " / tex " + (screen.sharedMaterial.mainTexture ? screen.sharedMaterial.mainTexture.name : "null") : "null") + ", 화면 +Z(방 기준) " + V(room.InverseTransformDirection(screen.transform.forward)));
        var b2 = LB(screen);
        sb.AppendLine("TV: 스케일 " + s.ToString("F3") + ", 화면 " + b2.size.z.ToString("F2") + "×" + b2.size.y.ToString("F2") + " m, 중심 " + V(b2.center) + ", 벽 x " + (-wall).ToString("F2"));
        Tree(tv.transform, 0);
        return tv;
    }

    // ProTV "TV Material Contamination": 캠프 TV 와 같은 머티리얼·렌더 텍스처를 쓰면 한쪽 영상이 다른 쪽 화면에 뜬다 → 침실 TV 가 참조하는 Material/RenderTexture 를 Assets/Bedroom/TV/ 에 복제해서 바꿔 끼움
    static void SeparateAssets(GameObject tv)
    {
        const string DIR = "Assets/Bedroom/TV/";
        AssetDatabase.DeleteAsset(DIR.TrimEnd('/'));   // 재실행 때 지난 복제본 정리
        Directory.CreateDirectory(DIR); AssetDatabase.Refresh();
        var map = new Dictionary<Object, Object>();
        Object Clone(Object o)
        {
            if (o == null) return null;
            if (map.TryGetValue(o, out var c)) return c;
            if (!(o is RenderTexture) && !(o is Material mv && mv.HasProperty("_VideoTex"))) return o;   // 영상이 들어가는 것만 (UI·글꼴 머티리얼은 그대로)
            string src = AssetDatabase.GetAssetPath(o);
            if (string.IsNullOrEmpty(src) || src.StartsWith(DIR)) { map[o] = o; return o; }
            string dst = DIR + o.name + "_Bedroom" + (o is Material ? ".mat" : ".renderTexture");
            AssetDatabase.DeleteAsset(dst);
            Object n = o is Material m ? new Material(m) : (Object)new RenderTexture((RenderTexture)o);
            if (n is Material nm0) nm0.name = o.name + "_Bedroom";
            AssetDatabase.CreateAsset(n, dst);
            map[o] = n; sb.AppendLine("  TV 에셋 분리 " + src + " → " + dst);
            return n;
        }
        // 1) 머티리얼 안의 렌더 텍스처 먼저, 2) 렌더러 머티리얼, 3) 모든 컴포넌트의 Material/RenderTexture 참조
        foreach (var r in tv.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials; bool ch = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                var nm = (Material)Clone(mats[i]);
                if (nm != mats[i]) { mats[i] = nm; ch = true; }
            }
            if (ch) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); }
        }
        foreach (var comp in tv.GetComponentsInChildren<Component>(true))
        {
            if (comp == null || comp is Transform || comp is Renderer) continue;
            var so = new SerializedObject(comp); var it = so.GetIterator(); bool ch = false;
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var v = it.objectReferenceValue;
                if (v is Material || v is RenderTexture) { var n = Clone(v); if (n != v) { it.objectReferenceValue = n; ch = true; } }
            }
            if (ch) { so.ApplyModifiedPropertiesWithoutUndo(); sb.AppendLine("  TV 참조 교체 " + comp.GetType().Name + " @ " + comp.name); }
        }
        // 복제된 머티리얼 속 텍스처 슬롯이 원본 렌더 텍스처를 가리키면 교체
        foreach (var kv in map.ToArray())
            if (kv.Value is Material nm2 && kv.Value != kv.Key)
                foreach (var pn in nm2.GetTexturePropertyNames())
                {
                    var tx = nm2.GetTexture(pn);
                    if (tx is RenderTexture) { var n = Clone(tx); if (n != tx) { nm2.SetTexture(pn, (Texture)n); EditorUtility.SetDirty(nm2); } }
                }
        AssetDatabase.SaveAssets();
        sb.AppendLine("  TV 분리 에셋 " + map.Count(kv => kv.Key != kv.Value) + "개");
    }

    static void Tree(Transform t, int d)
    {
        if (d > 3) return;
        var room = t.root;
        var comps = string.Join(",", t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name));
        var r = t.GetComponent<Renderer>(); var rc = t.GetComponent<RectTransform>();
        string where = r ? " bounds " + V(r.bounds.center - room.position) + " size " + r.bounds.size.ToString("F2") : " pos " + V(t.position - room.position);
        sb.AppendLine("  " + new string(' ', d * 2) + t.name + (t.gameObject.activeSelf ? "" : " (꺼짐)") + " [" + comps + "]" + where);
        foreach (Transform c in t) Tree(c, d + 1);
    }

    // HQ: 반사 레이어 = 전부(UI 5 · PlayerLocal 10 · UiMenu 12 제외), 픽셀 광원 켬. 바꾸기 전 값·전체 속성을 로그에
    static void MirrorHQ(Component m)
    {
        if (m == null) return;
        var so = new SerializedObject(m);
        var it = so.GetIterator(); var dump = new StringBuilder("  거울 속성:");
        if (it.NextVisible(true)) do { if (it.propertyType == SerializedPropertyType.Integer || it.propertyType == SerializedPropertyType.Boolean || it.propertyType == SerializedPropertyType.Enum || it.propertyType == SerializedPropertyType.LayerMask) dump.Append(" " + it.name + "=" + (it.propertyType == SerializedPropertyType.Boolean ? it.boolValue.ToString() : it.intValue.ToString())); } while (it.NextVisible(false));
        sb.AppendLine(dump.ToString());
        var rl = so.FindProperty("m_ReflectLayers");
        if (rl != null) { int b = rl.intValue; rl.intValue = ~((1 << 5) | (1 << 10) | (1 << 12)); sb.AppendLine("  m_ReflectLayers " + b + " → " + rl.intValue); }
        else sb.AppendLine("  !! m_ReflectLayers 없음");
        var dp = so.FindProperty("m_DisablePixelLights"); if (dp != null) { sb.AppendLine("  m_DisablePixelLights " + dp.boolValue + " → False"); dp.boolValue = false; }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ───────────────────────── UI 도우미 ─────────────────────────
    static RectTransform MakeCanvas(Transform parent, string name, float w, float h, Vector3 localPos, Quaternion localRot)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 0;
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.localPosition = localPos; rt.localRotation = localRot;
        rt.sizeDelta = new Vector2(w, h); rt.localScale = Vector3.one * PX;
        var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var cs = go.AddComponent<CanvasScaler>(); cs.dynamicPixelsPerUnit = 1f; cs.referencePixelsPerUnit = 100f;
        go.AddComponent<GraphicRaycaster>();
        var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(w, h, 2f);
        go.AddComponent<VRCUiShape>();
        return rt;
    }

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
        var i = r.gameObject.AddComponent<Image>(); i.color = col; i.raycastTarget = false; i.sprite = s;
        if (s == sOutline) { i.type = Image.Type.Sliced; i.pixelsPerUnitMultiplier = 1f; }
        return i;
    }

    static TextMeshProUGUI Txt(Transform parent, string name, float x, float y, float w, float h, string text, float size, Color col, TextAlignmentOptions align, float spacing = 0f)
    {
        var r = Rect(parent, name, x, y, w, h);
        var t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = font.material;
        t.text = text; t.fontSize = size; t.color = col; t.characterSpacing = spacing; t.alignment = align;
        t.enableWordWrapping = false; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;
        return t;
    }

    static void Wire(UnityEventBase ev, string method) => UnityEventTools.AddStringPersistentListener(ev, new UnityAction<string>(ub.SendCustomEvent), method);

    static Button Btn(Transform parent, string name, float x, float y, float w, float h, string label, float size)
    {
        var bg = Img(parent, name, x, y, w, h, W, sOutline); bg.raycastTarget = true;
        Txt(bg.transform, "Label", 0, 0, w, h, label, size, W, TextAlignmentOptions.Center, 4f);
        var b = bg.gameObject.AddComponent<Button>(); b.targetGraphic = bg; b.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = b.colors; cb.highlightedColor = new Color(1, 1, 1, 0.75f); cb.pressedColor = new Color(1, 1, 1, 0.5f); b.colors = cb;
        return b;
    }

    static Slider Sld(Transform parent, string name, float x, float y, float w)
    {
        const float hh = 44f;
        var r = Rect(parent, name, x, y, w, hh);
        var track = Img(r, "Track", 0, 16, w, 12, W, sOutline); track.raycastTarget = true;
        var fillArea = Rect(r, "Fill Area", 3, 19, w - 6, 6);
        var fill = Img(fillArea, "Fill", 0, 0, 0, 6, W, sFill);
        var fr = fill.rectTransform; fr.anchorMin = new Vector2(0, 0); fr.anchorMax = new Vector2(0, 1); fr.pivot = new Vector2(0.5f, 0.5f); fr.sizeDelta = Vector2.zero; fr.anchoredPosition = Vector2.zero;
        var handleArea = Rect(r, "Handle Slide Area", 18, 0, w - 36, hh);
        var handle = Img(handleArea, "Handle", 0, 0, 36, 0, W, sDot); handle.raycastTarget = true;
        var hr = handle.rectTransform; hr.anchorMin = new Vector2(0, 0); hr.anchorMax = new Vector2(0, 1); hr.pivot = new Vector2(0.5f, 0.5f); hr.sizeDelta = new Vector2(36f, -8f); hr.anchoredPosition = Vector2.zero;
        var s = r.gameObject.AddComponent<Slider>();
        s.fillRect = fr; s.handleRect = hr; s.targetGraphic = handle; s.direction = Slider.Direction.LeftToRight;
        s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false; s.value = 0f;
        s.navigation = new Navigation { mode = Navigation.Mode.None };
        return s;
    }

    static (Toggle, TextMeshProUGUI) Tgl(Transform parent, string name, float x, float y, float w, string label)
    {
        var r = Rect(parent, name, x, y, w, 56);
        var box = Img(r, "Box", 0, 6, 44, 44, W, sOutline); box.raycastTarget = true;
        var chk = Img(box.transform, "Check", 10, 10, 24, 24, W, sFill);
        var lab = Txt(r, "Label", 64, 0, w - 64, 56, label, 30, W, TextAlignmentOptions.MidlineLeft, 6f); lab.raycastTarget = true;
        var tg = r.gameObject.AddComponent<Toggle>();
        tg.targetGraphic = box; tg.graphic = chk; tg.isOn = false;
        tg.navigation = new Navigation { mode = Navigation.Mode.None };
        return (tg, lab);
    }

    static readonly List<(GameObject, string)> pending = new List<(GameObject, string)>();

    // ▲ 값 ▼ 한 칸. hold 면 누르고 있는 동안 반복 (EventTrigger: PointerDown = 메서드, PointerUp/Exit = Release)
    static TextMeshProUGUI Col(Transform parent, string name, float x, float y, float w, string val, string upM, string downM, bool hold)
    {
        var r = Rect(parent, name, x, y, w, 240);
        var upB = Img(r, "Up", 0, 0, w, 60, W, sOutline); upB.raycastTarget = true;
        Img(upB.transform, "Arrow", w / 2f - 18, 14, 36, 32, W, sUp);
        var t = Txt(r, "Value", 0, 70, w, 90, val, 60, W, TextAlignmentOptions.Center);
        var dnB = Img(r, "Down", 0, 170, w, 60, W, sOutline); dnB.raycastTarget = true;
        Img(dnB.transform, "Arrow", w / 2f - 18, 14, 36, 32, W, sDown);
        if (hold) { pending.Add((upB.gameObject, upM)); pending.Add((dnB.gameObject, downM)); }
        else
        {
            foreach (var g in new[] { upB, dnB })
            {
                var b = g.gameObject.AddComponent<Button>(); b.targetGraphic = g; b.navigation = new Navigation { mode = Navigation.Mode.None };
                pending.Add((g.gameObject, "!" + upM));   // "!" = Button onClick
            }
        }
        return t;
    }

    static void Hook(GameObject g, string method)
    {
        if (method.StartsWith("!")) { Wire(g.GetComponent<Button>().onClick, method.Substring(1)); return; }
        var et = g.AddComponent<EventTrigger>();
        void Add(EventTriggerType type, string m)
        {
            var e = new EventTrigger.Entry { eventID = type };
            UnityEventTools.AddStringPersistentListener(e.callback, new UnityAction<string>(ub.SendCustomEvent), m);
            et.triggers.Add(e);
        }
        Add(EventTriggerType.PointerDown, method);
        Add(EventTriggerType.PointerUp, "Release");
        Add(EventTriggerType.PointerExit, "Release");
        var img = g.GetComponent<Image>();
        // 누른 느낌: Button 없이 색 전환만 (Selectable 로 눌림 색)
        var sel = g.AddComponent<Selectable>(); sel.targetGraphic = img; sel.navigation = new Navigation { mode = Navigation.Mode.None };
        var cb = sel.colors; cb.highlightedColor = new Color(1, 1, 1, 0.75f); cb.pressedColor = new Color(1, 1, 1, 0.5f); sel.colors = cb;
    }

    // ───────────────────────── 스프라이트 · 소리 ─────────────────────────
    static void MakeSprites()
    {
        Directory.CreateDirectory(UI_DIR);
        sOutline = Spr("ui_outline", 64, (x, y) => RRectStroke(x, y, 64, 18f, 2.5f), 22);
        sRing = Spr("ui_ring", 128, (x, y) => Ring(x, y, 128, 60f, 4f), 0);
        sMoon = Spr("ui_moon", 128, (x, y) => Mathf.Max(Ring(x, y, 128, 60f, 4f), Moon(x, y, 128)), 0);
        sUp = Spr("ui_up", 64, (x, y) => Tri(x, y, 64, true), 0);
        sDown = Spr("ui_down", 64, (x, y) => Tri(x, y, 64, false), 0);
        sDot = Spr("ui_dot", 64, (x, y) => Mathf.Clamp01(30f - Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(32, 32)) + 0.5f), 0);
        sFill = Spr("ui_fill", 8, (x, y) => 1f, 0);
        sVideo = Spr("ui_video", 128, (x, y) => Mathf.Max(Ring(x, y, 128, 60f, 4f), Play(x, y, 128)), 0);
    }

    static float RRectStroke(int x, int y, int n, float r, float w)
    {
        var p = new Vector2(x + 0.5f - n / 2f, y + 0.5f - n / 2f); float h = n / 2f - 2f;
        var q = new Vector2(Mathf.Abs(p.x) - h + r, Mathf.Abs(p.y) - h + r);
        float d = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r;
        return Mathf.Clamp01(w * 0.5f - Mathf.Abs(d + w * 0.5f) + 0.5f);
    }
    static float Ring(int x, int y, int n, float r, float w) { float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)); return Mathf.Clamp01(w * 0.5f - Mathf.Abs(d - r + w * 0.5f) + 0.5f); }
    static float Moon(int x, int y, int n)
    {
        var p = new Vector2(x + 0.5f, y + 0.5f); var c = new Vector2(n * 0.5f, n * 0.5f);
        float a = Mathf.Clamp01(30f - Vector2.Distance(p, c) + 0.5f);
        float b = Mathf.Clamp01(26f - Vector2.Distance(p, c + new Vector2(14f, 10f)) + 0.5f);
        return Mathf.Clamp01(a - b);
    }
    static float Play(int x, int y, int n)   // 오른쪽을 향한 삼각형 (가운데, 높이 0.34 n)
    {
        float fx = (x + 0.5f) / n, fy = (y + 0.5f) / n;
        float x0 = 0.40f, x1 = 0.68f; if (fx < x0 || fx > x1) return 0f;
        float half = 0.17f * (x1 - fx) / (x1 - x0);
        return Mathf.Clamp01((half - Mathf.Abs(fy - 0.5f)) * n + 0.5f) * Mathf.Clamp01((fx - x0) * n + 0.5f);
    }
    static float Tri(int x, int y, int n, bool up)
    {
        float fx = (x + 0.5f) / n, fy = (y + 0.5f) / n; if (!up) fy = 1f - fy;
        float half = 0.42f * (1f - (fy - 0.18f) / 0.64f);                   // 아래(0.18) 넓고 위(0.82) 뾰족
        if (fy < 0.18f || fy > 0.82f) return 0f;
        return Mathf.Clamp01((half - Mathf.Abs(fx - 0.5f)) * n + 0.5f);
    }

    static Sprite Spr(string name, int n, System.Func<int, int, float> a, int border)
    {
        string path = UI_DIR + name + ".png";
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) px[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a(x, y)) * 255f));
        tex.SetPixels32(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single; imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true; imp.filterMode = FilterMode.Bilinear; imp.wrapMode = TextureWrapMode.Clamp;
        imp.spriteBorder = new Vector4(border, border, border, border); imp.spritePixelsPerUnit = 100f;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 디지털 알람시계: 2.6 kHz 삐 4번(80 ms, 사이 60 ms) + 쉼 → 1.1 s 반복
    static AudioClip MakeAlarmWav()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(WAV));
        const int rate = 44100; float total = 1.1f; int n = (int)(rate * total);
        var s = new short[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)rate; float v = 0f;
            for (int k = 0; k < 4; k++)
            {
                float t0 = 0.02f + k * 0.14f, t1 = t0 + 0.08f;
                if (t >= t0 && t < t1)
                {
                    float env = Mathf.Min(1f, (t - t0) / 0.004f) * Mathf.Min(1f, (t1 - t) / 0.004f);
                    float ph = 2f * Mathf.PI * 2600f * t;
                    v = env * (0.75f * Mathf.Sin(ph) + 0.22f * Mathf.Sin(3f * ph) + 0.08f * Mathf.Sin(5f * ph));
                }
            }
            s[i] = (short)Mathf.Clamp(v * 0.7f * 32767f, -32768f, 32767f);
        }
        using (var fs = new FileStream(WAV, FileMode.Create))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(Encoding.ASCII.GetBytes("RIFF")); bw.Write(36 + n * 2); bw.Write(Encoding.ASCII.GetBytes("WAVE"));
            bw.Write(Encoding.ASCII.GetBytes("fmt ")); bw.Write(16); bw.Write((short)1); bw.Write((short)1); bw.Write(rate); bw.Write(rate * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write(Encoding.ASCII.GetBytes("data")); bw.Write(n * 2);
            foreach (var v in s) bw.Write(v);
        }
        AssetDatabase.ImportAsset(WAV, ImportAssetOptions.ForceUpdate);
        var imp = (AudioImporter)AssetImporter.GetAtPath(WAV);
        imp.loadInBackground = false;
        var st = imp.defaultSampleSettings; st.loadType = AudioClipLoadType.DecompressOnLoad; st.preloadAudioData = true; imp.defaultSampleSettings = st;
        imp.SaveAndReimport();
        sb.AppendLine("알람 소리 " + WAV + " " + total + " s (2.6 kHz × 4)");
        return AssetDatabase.LoadAssetAtPath<AudioClip>(WAV);
    }

    // ───────────────────────── 렌더 ─────────────────────────
    static void Renders()
    {
        var room = Root("TentBedroom"); var o = room.transform; var p = o.Find(ROOT); if (p == null) return;
        var pc = p.Find("PanelCanvas").gameObject; var mir = p.Find("Mirror").gameObject;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 60f;
            Vector3 Wp(float x, float y, float z) => o.TransformPoint(new Vector3(x, y, z));
            Shot(cam, Wp(0.3f, 1.55f, -0.2f), Wp(0.2f, 0.7f, -2.6f), "bp_closed");
            pc.SetActive(true);
            Shot(cam, Wp(0.3f, 1.55f, -0.2f), Wp(0.2f, 0.7f, -2.6f), "bp_open_stand");
            Shot(cam, Wp(0.1f, 1.0f, -1.2f), Wp(0.1f, 0.8f, -2.6f), "bp_open_close");
            Shot(cam, Wp(-0.28f, 0.45f, -1.9f), Wp(0.1f, 0.85f, -2.6f), "bp_open_lie");
            Shot(cam, Wp(1.2f, 1.5f, 2.0f), Wp(-0.3f, 0.6f, -2.2f), "bp_open_room");
            pc.SetActive(false);
            mir.SetActive(true);
            Shot(cam, Wp(-1.0f, 1.5f, 0.3f), Wp(3.0f, 1.0f, -0.6f), "bp_mirror");
            Shot(cam, Wp(1.0f, 1.2f, 1.2f), Wp(2.9f, 0.9f, -0.6f), "bp_mirror_side");
            var tvT = p.Find("BedroomTV");
            if (tvT) { tvT.gameObject.SetActive(true); Shot(cam, Wp(0.8f, 1.5f, -0.2f), Wp(-2.8f, 1.1f, -0.9f), "bp_tv"); Shot(cam, Wp(-0.28f, 0.45f, -1.6f), Wp(-2.8f, 1.1f, -0.9f), "bp_tv_lie"); tvT.gameObject.SetActive(false); }
            mir.SetActive(false);
        }
        finally
        {
            pc.SetActive(false); mir.SetActive(false); var tvT2 = p.Find("BedroomTV"); if (tvT2) tvT2.gameObject.SetActive(false);
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

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_panel.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }
}
#endif
