// 설정 패널 (프로젝터가 띄우는 공중 UI) — 로컬. 시간만 DayCycle 을 거쳐 모두에게 동기화된다
//  UI 이벤트(Slider/Toggle/Button) → UdonBehaviour.SendCustomEvent("On...") 로 들어온다 (에디터 Z25a 가 연결)
//  2026-09-24: 스크립트는 항상 켜진 프로젝터 루트에 있다(패널 캔버스 = panelRoot). 입장 때 개인 설정을 PlayerData 에서 불러와 적용하고,
//  바꿀 때마다 1 초 모아서 저장한다. 시간·시간 흐름은 방 전체 공유라 저장 안 함
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteSettings : UdonSharpBehaviour
{
    [Header("연결")]
    public PyriteDayCycle cycle;
    public PyriteProjector projector;

    [Header("시간")]
    public Slider timeSlider;             // 0..1439 분
    public TextMeshProUGUI timeText;
    public Toggle autoToggle;

    [Header("화면")]
    public Slider brightSlider;           // -1..1 → 밝게/어둡게 볼륨 weight
    public PostProcessVolume ppBright;
    public PostProcessVolume ppDark;
    public Toggle bloomToggle;
    public PostProcessVolume ppNoBloom;

    [Header("반사")]
    public Toggle lakeToggle;
    public Toggle campMirrorToggle;         // 타프 줄 손거울과 같은 거울
    public PyriteMirrorToggle lakeMirror;
    public PyriteMirrorToggle campMirror;

    [Header("소리")]
    public Slider soundSlider;            // 0..1.5
    public TextMeshProUGUI soundText;

    [Header("성능")]
    public Toggle flowersToggle;           // (옛 꽃 켬/끔 — 2026-09-24 부터 거리 바로 대체, 비어 있음)
    public Slider flowerDistSlider;        // 0..16 → ×10 m. 0 = 끔, 16 = 전부
    public TextMeshProUGUI flowerDistText;
    public PyriteFlowerCull flowerCull;
    public Toggle firefliesToggle;
    public Toggle shadowsToggle;
    public Toggle lightShadowsToggle;      // 모닥불·들고 다니는 랜턴 점광 그림자
    public Light[] shadowLights;
    public Renderer[] flowerRenderers;
    public Renderer[] fireflyRenderers;
    public Light sunLight;

    [Header("언어")]
    public TextMeshProUGUI[] texts;
    public string[] textEn;
    public string[] textKo;
    public Image langEnBg;
    public Image langKoBg;
    public Color langOn = new Color(0.95f, 0.78f, 0.42f, 0.9f);
    public Color langOff = new Color(1f, 1f, 1f, 0.08f);
    public int lang = 0;                  // 0 EN, 1 KO

    [Header("패널 (캔버스)")]
    public GameObject panelRoot;

    [Header("사용한 에셋 팝업")]
    public GameObject assetsPopup;        // 메인이 닫히면 같이 닫힌다 (OnDisable)
    public GameObject guidePopup;         // 상호작용 가능한 사물 (같은 방식)

    private bool updating = false;
    private bool inited = false;
    private float lastUserTime = -10f;
    private float nextTick = 0f;
    private LightShadows sunShadowMode = LightShadows.Soft;
    public LightShadows lightShadowOn = LightShadows.Soft;   // 켤 때 모드 (LightShadows[] 는 Udon 미노출 — 둘 다 Soft 였다)

    private bool wasOpen = false;
    private bool restored = false;         // PlayerData 를 불러온 뒤에만 저장한다
    private bool dirty = false;
    private float saveAt = 0f;

    void Start() { Init(); }

    private void ClosePopups()
    {
        if (assetsPopup != null) assetsPopup.SetActive(false);
        if (guidePopup != null) guidePopup.SetActive(false);
    }

    private void Init()
    {
        if (inited) return;
        inited = true;
        if (sunLight != null && sunLight.shadows != LightShadows.None) sunShadowMode = sunLight.shadows;
        ApplyLang();
        Refresh(true);
    }

    void Update()
    {
        if (dirty && Time.time >= saveAt) SavePrefs();
        bool open = panelRoot == null || panelRoot.activeInHierarchy;
        if (open != wasOpen)
        {
            wasOpen = open;
            ClosePopups();
            if (open) Refresh(true);
        }
        if (!open) return;
        if (Time.time < nextTick) return;
        nextTick = Time.time + 0.25f;
        Refresh(false);
    }

    // 현재 상태 → UI (이벤트가 다시 들어오지 않게 updating 으로 막는다)
    private void Refresh(bool all)
    {
        if (cycle == null) return;
        updating = true;
        float h = cycle.CurrentHour();
        int mins = Mathf.FloorToInt(h * 60f) % 1440;
        if (timeText != null) timeText.text = Two(mins / 60) + ":" + Two(mins % 60);
        if (timeSlider != null && Time.time - lastUserTime > 1.0f && Mathf.Abs(timeSlider.value - mins) > 1.5f) timeSlider.value = mins;
        if (autoToggle != null && autoToggle.isOn != cycle.autoFlow) autoToggle.isOn = cycle.autoFlow;
        if (all)
        {
            if (lakeToggle != null && lakeMirror != null) lakeToggle.isOn = lakeMirror.IsOn();
            if (campMirrorToggle != null && campMirror != null) campMirrorToggle.isOn = campMirror.IsOn();
            if (soundSlider != null) soundSlider.value = cycle.soundScale;
            SoundLabel();
            if (flowerDistSlider != null && flowerCull != null) flowerDistSlider.value = Mathf.Round(flowerCull.distance / 10f);
            FlowerLabel();
            if (lightShadowsToggle != null && shadowLights != null && shadowLights.Length > 0 && shadowLights[0] != null) lightShadowsToggle.isOn = shadowLights[0].shadows != LightShadows.None;
        }
        updating = false;
    }

    private string Two(int v) { return v < 10 ? "0" + v : v.ToString(); }

    // ── 시간 ──
    public void OnTime()
    {
        if (updating || timeSlider == null || cycle == null) return;
        lastUserTime = Time.time;
        int mins = Mathf.RoundToInt(timeSlider.value);
        cycle.SetHour(mins / 60f);
        if (timeText != null) timeText.text = Two(mins / 60) + ":" + Two(mins % 60);
    }

    public void OnAuto()
    {
        if (updating || autoToggle == null || cycle == null) return;
        cycle.SetAutoFlow(autoToggle.isOn);
    }

    // ── 화면 ──
    public void OnBright()
    {
        if (updating || brightSlider == null) return;
        float v = brightSlider.value;
        if (Mathf.Abs(v) < 0.05f) v = 0f;
        if (ppBright != null) ppBright.weight = v > 0f ? v : 0f;
        if (ppDark != null) ppDark.weight = v < 0f ? -v : 0f;
        MarkDirty();
    }

    public void OnBloom()
    {
        if (updating || bloomToggle == null) return;
        if (ppNoBloom != null) ppNoBloom.weight = bloomToggle.isOn ? 0f : 1f;
        MarkDirty();
    }

    // ── 반사 ──
    public void OnLake()
    {
        if (updating || lakeToggle == null || lakeMirror == null) return;
        lakeMirror.SetOn(lakeToggle.isOn);
        MarkDirty();
    }

    public void OnCampMirror()
    {
        if (updating || campMirrorToggle == null || campMirror == null) return;
        campMirror.SetOn(campMirrorToggle.isOn);
    }

    // ── 소리 ──
    public void OnSound()
    {
        if (updating || soundSlider == null || cycle == null) return;
        cycle.SetSoundScale(soundSlider.value);
        SoundLabel();
        MarkDirty();
    }

    private void SoundLabel()
    {
        if (soundText != null && soundSlider != null) soundText.text = Mathf.RoundToInt(soundSlider.value * 100f) + "%";
    }

    // ── 성능 ──
    public void OnFlowers()
    {
        if (updating || flowersToggle == null) return;
        bool on = flowersToggle.isOn;
        for (int i = 0; i < flowerRenderers.Length; i++) if (flowerRenderers[i] != null) flowerRenderers[i].enabled = on;
    }

    public void OnFlowerDist()
    {
        if (updating || flowerDistSlider == null || flowerCull == null) return;
        flowerCull.SetDistance(flowerDistSlider.value * 10f);
        FlowerLabel();
        MarkDirty();
    }

    private void FlowerLabel()
    {
        if (flowerDistText == null || flowerDistSlider == null) return;
        float v = flowerDistSlider.value;
        if (v <= 0f) flowerDistText.text = lang == 1 ? "끔" : "Off";
        else if (v >= flowerDistSlider.maxValue) flowerDistText.text = lang == 1 ? "전부" : "All";
        else flowerDistText.text = Mathf.RoundToInt(v * 10f) + " m";
    }

    public void OnLightShadows()
    {
        if (updating || lightShadowsToggle == null || shadowLights == null) return;
        bool on = lightShadowsToggle.isOn;
        for (int i = 0; i < shadowLights.Length; i++)
            if (shadowLights[i] != null) shadowLights[i].shadows = on ? lightShadowOn : LightShadows.None;
        MarkDirty();
    }

    public void OnFireflies()
    {
        if (updating || firefliesToggle == null) return;
        bool on = firefliesToggle.isOn;
        for (int i = 0; i < fireflyRenderers.Length; i++) if (fireflyRenderers[i] != null) fireflyRenderers[i].enabled = on;
        MarkDirty();
    }

    public void OnShadows()
    {
        if (updating || shadowsToggle == null || sunLight == null) return;
        sunLight.shadows = shadowsToggle.isOn ? sunShadowMode : LightShadows.None;
        MarkDirty();
    }

    // ── 개인 설정 저장 (PlayerData) ──
    private const string K_BRIGHT = "pl_bright", K_BLOOM = "pl_bloom", K_LAKE = "pl_lake", K_SOUND = "pl_sound",
        K_FLOWER = "pl_flowerDist", K_FIREFLY = "pl_fireflies", K_SUNSH = "pl_sunShadows", K_LIGHTSH = "pl_lightShadows", K_LANG = "pl_lang";

    private void MarkDirty()
    {
        if (!restored || updating) return;
        dirty = true; saveAt = Time.time + 1f;
    }

    private void SavePrefs()
    {
        dirty = false;
        if (!restored) return;
        if (brightSlider != null) PlayerData.SetFloat(K_BRIGHT, brightSlider.value);
        if (bloomToggle != null) PlayerData.SetBool(K_BLOOM, bloomToggle.isOn);
        if (lakeToggle != null) PlayerData.SetBool(K_LAKE, lakeToggle.isOn);
        // 거울은 저장 안 함 (관리자 2026-09-24: 입장하자마자 거울이 켜지면 무겁다)
        if (soundSlider != null) PlayerData.SetFloat(K_SOUND, soundSlider.value);
        if (flowerDistSlider != null) PlayerData.SetFloat(K_FLOWER, flowerDistSlider.value);
        if (firefliesToggle != null) PlayerData.SetBool(K_FIREFLY, firefliesToggle.isOn);
        if (shadowsToggle != null) PlayerData.SetBool(K_SUNSH, shadowsToggle.isOn);
        if (lightShadowsToggle != null) PlayerData.SetBool(K_LIGHTSH, lightShadowsToggle.isOn);
        PlayerData.SetInt(K_LANG, lang);
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal) return;
        Init();
        // 컨트롤 값만 먼저 바꾸고(updating 으로 이벤트 막음) 핸들러를 직접 불러 적용한다
        if (brightSlider != null && PlayerData.HasKey(player, K_BRIGHT)) { updating = true; brightSlider.value = PlayerData.GetFloat(player, K_BRIGHT); updating = false; OnBright(); }
        if (bloomToggle != null && PlayerData.HasKey(player, K_BLOOM)) { updating = true; bloomToggle.isOn = PlayerData.GetBool(player, K_BLOOM); updating = false; OnBloom(); }
        if (lakeToggle != null && PlayerData.HasKey(player, K_LAKE)) { updating = true; lakeToggle.isOn = PlayerData.GetBool(player, K_LAKE); updating = false; OnLake(); }
        if (soundSlider != null && PlayerData.HasKey(player, K_SOUND)) { updating = true; soundSlider.value = PlayerData.GetFloat(player, K_SOUND); updating = false; OnSound(); }
        if (flowerDistSlider != null && PlayerData.HasKey(player, K_FLOWER)) { updating = true; flowerDistSlider.value = PlayerData.GetFloat(player, K_FLOWER); updating = false; OnFlowerDist(); }
        if (firefliesToggle != null && PlayerData.HasKey(player, K_FIREFLY)) { updating = true; firefliesToggle.isOn = PlayerData.GetBool(player, K_FIREFLY); updating = false; OnFireflies(); }
        if (shadowsToggle != null && PlayerData.HasKey(player, K_SUNSH)) { updating = true; shadowsToggle.isOn = PlayerData.GetBool(player, K_SUNSH); updating = false; OnShadows(); }
        if (lightShadowsToggle != null && PlayerData.HasKey(player, K_LIGHTSH)) { updating = true; lightShadowsToggle.isOn = PlayerData.GetBool(player, K_LIGHTSH); updating = false; OnLightShadows(); }
        if (PlayerData.HasKey(player, K_LANG)) { lang = PlayerData.GetInt(player, K_LANG); ApplyLang(); }
        restored = true;   // 이 뒤로 바꾸는 것만 저장 (불러오기 자체는 저장 안 함)
    }

    // ── 언어 / 닫기 ──
    public void SetEN() { lang = 0; ApplyLang(); MarkDirty(); }
    public void SetKO() { lang = 1; ApplyLang(); MarkDirty(); }

    private void ApplyLang()
    {
        string[] src = lang == 1 ? textKo : textEn;
        for (int i = 0; i < texts.Length; i++) if (texts[i] != null && i < src.Length) texts[i].text = src[i];
        if (langEnBg != null) langEnBg.color = lang == 0 ? langOn : langOff;
        if (langKoBg != null) langKoBg.color = lang == 1 ? langOn : langOff;
        FlowerLabel();
    }

    public void OpenAssets() { ClosePopups(); if (assetsPopup != null) assetsPopup.SetActive(true); }
    public void OpenGuide() { ClosePopups(); if (guidePopup != null) guidePopup.SetActive(true); }
    public void CloseGuide() { if (guidePopup != null) guidePopup.SetActive(false); }
    public void CloseAssets() { if (assetsPopup != null) assetsPopup.SetActive(false); }

    public void Close()
    {
        ClosePopups();
        if (projector != null) projector.TurnOff();
    }
}
