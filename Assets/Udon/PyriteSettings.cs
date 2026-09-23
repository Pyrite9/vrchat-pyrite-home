// 설정 패널 (프로젝터가 띄우는 공중 UI) — 로컬. 시간만 DayCycle 을 거쳐 모두에게 동기화된다
//  UI 이벤트(Slider/Toggle/Button) → UdonBehaviour.SendCustomEvent("On...") 로 들어온다 (에디터 Z25a 가 연결)
//  패널이 켜져 있을 때만 Update 가 돈다 (꺼지면 GameObject 비활성)
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

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
    public Toggle flowersToggle;
    public Toggle firefliesToggle;
    public Toggle shadowsToggle;
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

    [Header("사용한 에셋 팝업")]
    public GameObject assetsPopup;        // 메인이 닫히면 같이 닫힌다 (OnDisable)

    private bool updating = false;
    private bool inited = false;
    private float lastUserTime = -10f;
    private float nextTick = 0f;
    private LightShadows sunShadowMode = LightShadows.Soft;

    void Start() { Init(); }

    void OnEnable() { if (assetsPopup != null) assetsPopup.SetActive(false); if (inited) Refresh(true); }

    void OnDisable() { if (assetsPopup != null) assetsPopup.SetActive(false); }

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
    }

    public void OnBloom()
    {
        if (updating || bloomToggle == null) return;
        if (ppNoBloom != null) ppNoBloom.weight = bloomToggle.isOn ? 0f : 1f;
    }

    // ── 반사 ──
    public void OnLake()
    {
        if (updating || lakeToggle == null || lakeMirror == null) return;
        lakeMirror.SetOn(lakeToggle.isOn);
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

    public void OnFireflies()
    {
        if (updating || firefliesToggle == null) return;
        bool on = firefliesToggle.isOn;
        for (int i = 0; i < fireflyRenderers.Length; i++) if (fireflyRenderers[i] != null) fireflyRenderers[i].enabled = on;
    }

    public void OnShadows()
    {
        if (updating || shadowsToggle == null || sunLight == null) return;
        sunLight.shadows = shadowsToggle.isOn ? sunShadowMode : LightShadows.None;
    }

    // ── 언어 / 닫기 ──
    public void SetEN() { lang = 0; ApplyLang(); }
    public void SetKO() { lang = 1; ApplyLang(); }

    private void ApplyLang()
    {
        string[] src = lang == 1 ? textKo : textEn;
        for (int i = 0; i < texts.Length; i++) if (texts[i] != null && i < src.Length) texts[i].text = src[i];
        if (langEnBg != null) langEnBg.color = lang == 0 ? langOn : langOff;
        if (langKoBg != null) langKoBg.color = lang == 1 ? langOn : langOff;
    }

    public void OpenAssets() { if (assetsPopup != null) assetsPopup.SetActive(true); }
    public void CloseAssets() { if (assetsPopup != null) assetsPopup.SetActive(false); }

    public void Close()
    {
        if (assetsPopup != null) assetsPopup.SetActive(false);
        if (projector != null) projector.TurnOff();
    }
}
