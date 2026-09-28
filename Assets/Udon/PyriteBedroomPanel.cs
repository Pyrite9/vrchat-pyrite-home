// PyriteBedroomPanel — 텐트 침실 머리맡 팝업 (로컬 전용, 동기화 없음)
//  아이콘(달) 누르기 = 패널 열기/닫기 (알람이 울리는 중이면 알람 끄기)
//  수면 모드 0~1: 침실 광원 기준 밝기 × (1 → 0.05), 창밖(M_Backdrop _Dim) × (1 → 0.4)
//  거울: 오른쪽 벽 전신거울 켜기/끄기
//  알람: AM/PM·시·분 화살표 (누르고 있으면 0.45 s 뒤부터 반복, 1.5 s 뒤 더 빠르게), 켜짐이면 PC 시계로 그 분이 되면 울림. STOP 또는 아이콘으로 끔. 5분 뒤 자동으로 끔
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteBedroomPanel : UdonSharpBehaviour
{
    public GameObject panel;
    public GameObject mirror;
    public Slider sleepSlider;
    public TextMeshProUGUI sleepValue;
    public Toggle mirrorToggle;
    public TextMeshProUGUI clockText;
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

    void Start()
    {
        baseI = new float[lights.Length];
        for (int i = 0; i < lights.Length; i++) baseI[i] = lights[i] != null ? lights[i].intensity : 0f;
        panel.SetActive(false);
        mirror.SetActive(false);
        stopButton.SetActive(false);
        ApplySleep(0f);
        RefreshAlarm();
        UpdateClock();
    }

    // ── 아이콘 · 패널 ──
    public void OnIcon()
    {
        if (ringing) { StopAlarm(); return; }
        panel.SetActive(!panel.activeSelf);
    }

    public void OnClose() { panel.SetActive(false); }

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
        }
        if (ringing)
        {
            float a = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 8f));
            icon.color = new Color(1f, 1f, 1f, a);
            if (Time.time - ringStart > ringMaxSeconds) StopAlarm();
        }
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
