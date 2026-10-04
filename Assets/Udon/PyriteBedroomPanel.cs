// PyriteBedroomPanel — 텐트 침실 머리맡 팝업 (로컬 전용, 동기화 없음)
//  아이콘(달) 누르기 = 패널 열기/닫기 (알람이 울리는 중이면 알람 끄기)
//  수면 모드 0~1: 침실 광원 기준 밝기 × (1 → 0.05), 창밖(M_Backdrop _Dim) × (1 → 0.4)
//  거울: 오른쪽 벽 전신거울 켜기/끄기
//  침실 환경광: 방(부모) 반경 안에 있으면 PostLateUpdate 에서 DayCycle 이 쓴 환경광(Trilight)·반사 세기의 "밤보다 밝은 몫"을 dayAmbient × (1 − 수면) 만 남김. 나가면 DayCycle 값 복원
//  빈백: − n/6 + · RESET → PyriteBeanbagPool (모두에게 동기화). 개수 글자는 0.5 s 마다 새로 읽음 (다른 사람이 바꿔도 맞게)
//  알람: AM/PM·시·분 화살표 (누르고 있으면 0.45 s 뒤부터 반복, 1.5 s 뒤 더 빠르게), 켜짐이면 PC 시계로 그 분이 되면 울림. STOP 또는 아이콘으로 끔. 5분 뒤 자동으로 끔
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteBedroomPanel : UdonSharpBehaviour
{
    public GameObject panel;
    public GameObject mirror;
    public GameObject tvRoot;               // 침실 전용 ProTV (씬에선 꺼진 채로 시작, 로컬 표시 토글)
    public Slider sleepSlider;
    public Slider natureSlider;
    public TextMeshProUGUI natureValue;
    public AudioSource[] natureNight;       // 풀벌레 (밤일수록)
    public AudioSource[] natureDay;         // 물가 (항상)
    public float[] natureNightBase = new float[] { 0.38f };          // DayCycle nightAmbBase(AMB_N_Crickets_A)
    public float[] natureDayBase = new float[] { 0.42f };             // DayCycle ambBase(AMB_Water_ShoreN)
    public TextMeshProUGUI sleepValue;
    public Toggle mirrorToggle;
    public TextMeshProUGUI clockText;
    public TextMeshPro deskClock;           // 오른쪽 협탁 탁상시계 (Z51r). 같은 시각, 알람 켜짐이면 알람 시각 한 줄, 울리면 깜박임
    public Color deskClockColor = new Color(1f, 0.58f, 0.22f, 1f);
    public Toggle alarmToggle;
    public TextMeshProUGUI ampmText;
    public TextMeshProUGUI hourText;
    public TextMeshProUGUI minText;
    public GameObject stopButton;
    public Image icon;
    public AudioSource alarmSource;
    public Light[] lights;
    public Material backdrop;
    public float minLight = 0.05f;
    public float minWindow = 0.4f;
    public float ringMaxSeconds = 300f;
    public float dayAmbient = 0.35f;
    public float roomRadius = 12f;
    public Color nightSky = new Color(0.028f, 0.040f, 0.088f, 1f);
    public Color nightEq = new Color(0.022f, 0.030f, 0.062f, 1f);
    public Color nightGr = new Color(0.008f, 0.011f, 0.022f, 1f);
    public float nightRefl = 0.35f;
    public Light starLight;                 // 무드등: 별 조명(스팟 + 쿠키). 수면 올리면 밝아지고 천천히 돎
    public float starMax = 2f;
    public Material stringMat;              // 무드등: 줄전구 전구 머티리얼. 수면 올리면 어두워짐
    public Material globeMat;               // 별 구(협탁 위) 발광 머티리얼 — 끄면 발광 0 (Z51r)
    public Color globeEmit = new Color(1.1f, 1.1f, 1.1f, 1f);
    public Color stringEmit = new Color(2.2f, 1.54f, 0.79f, 1f);
    public PyriteBeanbagPool bagPool;       // 빈백 개수 (Z52i)
    public TextMeshProUGUI bagValue;
    public float minWindowDay = 0.13f;     // 낮 수면 100% 창 밝기 (Z50e: 창 카메라 정오 99~105 vs 밤 33 → ×0.13 ≈ 밤 ×0.4)

    private bool inRoom;
    private Color dSky;
    private Color dEq;
    private Color dGr;
    private float dRefl;
    private Color wSky;
    private Color wEq;
    private Color wGr;
    private float wRefl;

    private float[] baseI;
    private bool alarmOn;
    private bool pm;
    private int hour12 = 7;
    private int minute;
    private bool ringing;
    private float ringStart;
    private int lastFiredKey = -1;
    private int holdDir;          // 1 시↑ 2 시↓ 3 분↑ 4 분↓
    private float holdStart;
    private float nextRepeat;
    private float nextClock;
    private bool updating;
    private bool starOff;                   // 별 구 눌러서 끔 (로컬)

    void Start()
    {
        baseI = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseI[i] = lights[i] != null ? lights[i].intensity : 0f;
        panel.SetActive(false);
        mirror.SetActive(false);
        stopButton.SetActive(false);
        ApplySleep(0f);
        OnNature();
        SetNature(0f, 0f);
        RefreshAlarm();
        UpdateClock();
        ApplyStar();
    }

    // ── 별 조명 켜기/끄기 (협탁 위 별 구를 누름, 로컬) ──
    public void ToggleStar()
    {
        starOff = !starOff;
        ApplyStar();
    }

    private void ApplyStar()
    {
        if (starLight != null) starLight.enabled = !starOff;
        if (globeMat != null) globeMat.SetColor("_EmissionColor", starOff ? Color.black : globeEmit);
    }

    // ── 아이콘 · 패널 ──
    public void OnIcon()
    {
        if (ringing) { StopAlarm(); return; }
        panel.SetActive(!panel.activeSelf);
    }

    public void OnClose() { panel.SetActive(false); }

    // ── 빈백 개수 (모두에게) ──
    public void OnBagAdd() { if (bagPool != null) bagPool.Add(); RefreshBags(); }
    public void OnBagRemove() { if (bagPool != null) bagPool.Remove(); RefreshBags(); }
    public void OnBagReset() { if (bagPool != null) bagPool.ResetAll(); }

    private void RefreshBags()
    {
        if (bagPool == null || bagValue == null) return;
        bagValue.text = bagPool.Count() + " / " + bagPool.Max();
    }

    // ── 동영상 플레이어 (위 아이콘) ──
    public void OnVideo()
    {
        if (tvRoot != null) tvRoot.SetActive(!tvRoot.activeSelf);
    }

    // ── 수면 모드 ──
    public void OnSleep()
    {
        if (updating) return;
        ApplySleep(sleepSlider.value);
    }

    private void ApplySleep(float s)
    {
        float k = Mathf.Lerp(1f, minLight, s);
        for (int i = 0; i < lights.Length; i++) if (lights[i] != null) lights[i].intensity = baseI[i] * k;
        if (backdrop != null) backdrop.SetFloat("_Dim", Mathf.Lerp(1f, minWindow, s));
        sleepValue.text = Mathf.RoundToInt(s * 100f) + "%";
    }

    // ── 자연 소리 ──
    public void OnNature()
    {
        natureValue.text = Mathf.RoundToInt(natureSlider.value * 100f) + "%";
    }

    private void SetNature(float level, float nightK)
    {
        for (int i = 0; i < natureNight.Length; i++) if (natureNight[i] != null) natureNight[i].volume = level * (i < natureNightBase.Length ? natureNightBase[i] : 0.3f) * nightK;
        for (int i = 0; i < natureDay.Length; i++) if (natureDay[i] != null) natureDay[i].volume = level * (i < natureDayBase.Length ? natureDayBase[i] : 0.3f);
    }

    // ── 거울 ──
    public void OnMirror()
    {
        if (updating) return;
        mirror.SetActive(mirrorToggle.isOn);
    }

    // ── 알람 설정 ──
    public void OnAlarmToggle()
    {
        if (updating) return;
        alarmOn = alarmToggle.isOn;
        if (!alarmOn && ringing) StopAlarm();
        lastFiredKey = -1;
    }

    public void AmPm() { pm = !pm; RefreshAlarm(); }
    public void HourUp() { Press(1); }
    public void HourDown() { Press(2); }
    public void MinUp() { Press(3); }
    public void MinDown() { Press(4); }
    public void Release() { holdDir = 0; }

    private void Press(int dir)
    {
        Step(dir);
        holdDir = dir;
        holdStart = Time.time;
        nextRepeat = Time.time + 0.45f;
    }

    private void Step(int dir)
    {
        if (dir == 1) { hour12 = hour12 % 12 + 1; }
        else if (dir == 2) { hour12 = (hour12 + 10) % 12 + 1; }
        else if (dir == 3) { minute = (minute + 1) % 60; }
        else if (dir == 4) { minute = (minute + 59) % 60; }
        lastFiredKey = -1;
        RefreshAlarm();
    }

    private void RefreshAlarm()
    {
        ampmText.text = pm ? "PM" : "AM";
        hourText.text = hour12 < 10 ? "0" + hour12 : hour12.ToString();
        minText.text = minute < 10 ? "0" + minute : minute.ToString();
    }

    private int AlarmHour24()
    {
        int h = hour12 % 12;
        return pm ? h + 12 : h;
    }

    // ── 울리기 ──
    public void StopAlarm()
    {
        ringing = false;
        if (alarmSource != null) alarmSource.Stop();
        stopButton.SetActive(false);
        icon.color = Color.white;
        if (deskClock != null) deskClock.color = deskClockColor;
    }

    private void Ring()
    {
        ringing = true;
        ringStart = Time.time;
        if (alarmSource != null) { alarmSource.loop = true; alarmSource.Play(); }
        stopButton.SetActive(true);
    }

    private void UpdateClock()
    {
        DateTime now = DateTime.Now;
        int h = now.Hour;
        int h12 = h % 12; if (h12 == 0) h12 = 12;
        string mm = now.Minute < 10 ? "0" + now.Minute : now.Minute.ToString();
        clockText.text = h12 + ":" + mm + (h < 12 ? " AM" : " PM");
        if (deskClock != null) deskClock.text = h12 + ":" + mm + "<size=40%> " + (h < 12 ? "AM" : "PM") + (alarmOn ? "\n<size=30%>ALARM " + hour12 + ":" + (minute < 10 ? "0" + minute : minute.ToString()) + (pm ? " PM" : " AM") : "");
        int key = h * 60 + now.Minute;
        if (alarmOn && !ringing && h == AlarmHour24() && now.Minute == minute && key != lastFiredKey)
        {
            lastFiredKey = key;
            Ring();
        }
    }

    void Update()
    {
        if (holdDir != 0 && Time.time >= nextRepeat)
        {
            Step(holdDir);
            nextRepeat = Time.time + (Time.time - holdStart > 1.5f ? 0.04f : 0.09f);
        }
        if (Time.time >= nextClock)
        {
            nextClock = Time.time + 0.5f;
            UpdateClock();
            if (panel.activeSelf) RefreshBags();
        }
        if (ringing)
        {
            float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
            icon.color = new Color(1f, 1f, 1f, a);
            if (deskClock != null) deskClock.color = new Color(deskClockColor.r, deskClockColor.g, deskClockColor.b, a);
            if (Time.time - ringStart > ringMaxSeconds) StopAlarm();
        }
    }

    // ── 침실 환경광 ──
    public override void PostLateUpdate()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        Transform room = transform.parent;
        bool now = (lp.GetPosition() - room.position).sqrMagnitude < roomRadius * roomRadius;
        if (!now)
        {
            if (inRoom)
            {
                inRoom = false;
                RenderSettings.ambientSkyColor = dSky;
                RenderSettings.ambientEquatorColor = dEq;
                RenderSettings.ambientGroundColor = dGr;
                RenderSettings.reflectionIntensity = dRefl;
                SetNature(0f, 0f);   // 2D 라 침실 밖에서도 들리므로 끔
            }
            return;
        }
        Color cs = RenderSettings.ambientSkyColor;
        Color ce = RenderSettings.ambientEquatorColor;
        Color cg = RenderSettings.ambientGroundColor;
        float cr = RenderSettings.reflectionIntensity;
        if (!inRoom || !Same(cs, wSky) || !Same(ce, wEq) || !Same(cg, wGr) || cr != wRefl)
        {
            dSky = cs; dEq = ce; dGr = cg; dRefl = cr;   // DayCycle 가 새로 쓴 값
        }
        float k = Mathf.Lerp(dayAmbient, 0f, sleepSlider.value);
        wSky = Dim(nightSky, dSky, k);
        wEq = Dim(nightEq, dEq, k);
        wGr = Dim(nightGr, dGr, k);
        wRefl = Mathf.Lerp(Mathf.Min(nightRefl, dRefl), dRefl, k);
        RenderSettings.ambientSkyColor = wSky;
        RenderSettings.ambientEquatorColor = wEq;
        RenderSettings.ambientGroundColor = wGr;
        RenderSettings.reflectionIntensity = wRefl;
        // 창: 밤은 minWindow 그대로, 낮일수록 minWindowDay 쪽 (낮 정도 = 환경광 sky 합, 밤 0.156 → 1.0 이상이면 낮)
        float dayK = Mathf.Clamp01((dSky.r + dSky.g + dSky.b - 0.156f) / 0.85f);
        if (backdrop != null) backdrop.SetFloat("_Dim", Mathf.Lerp(1f, Mathf.Lerp(minWindow, minWindowDay, dayK), sleepSlider.value));
        SetNature(natureSlider.value, 1f - dayK);
        float sl = sleepSlider.value;
        if (starLight != null && !starOff)
        {
            starLight.intensity = starMax * Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 0.7f, sl)));
            starLight.transform.Rotate(0f, 0f, 1.5f * Time.deltaTime, Space.Self);
        }
        if (stringMat != null) stringMat.SetColor("_EmissionColor", stringEmit * Mathf.Lerp(1f, 0.3f, sl));
        inRoom = true;
    }

    private bool Same(Color a, Color b)
    {
        return a.r == b.r && a.g == b.g && a.b == b.b;
    }

    private Color Dim(Color night, Color day, float k)
    {
        Color lo = new Color(Mathf.Min(night.r, day.r), Mathf.Min(night.g, day.g), Mathf.Min(night.b, day.b), 1f);
        return Color.Lerp(lo, day, k);
    }

    // 에디터 시험용 (ClientSim): 알람 시각을 지금 분으로 맞추고 켠다
    public void DebugArmNow()
    {
        DateTime now = DateTime.Now;
        pm = now.Hour >= 12;
        hour12 = now.Hour % 12; if (hour12 == 0) hour12 = 12;
        minute = now.Minute;
        alarmOn = true;
        updating = true; alarmToggle.isOn = true; updating = false;
        lastFiredKey = -1;
        RefreshAlarm();
        UpdateClock();
    }
}
