// 마시멜로 상태 (꼬치의 자식) — 익은 정도 cook 0~1, 먹음 eaten 을 모두에게 맞춘다. 주인은 꼬치를 든 사람
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteSkewerState : UdonSharpBehaviour
{
    [UdonSynced] public float cook;
    [UdonSynced] public bool eaten;
    public Renderer marsh;
    public Color raw = new Color(0.97f, 0.95f, 0.91f);
    public Color golden = new Color(0.86f, 0.58f, 0.26f);
    public Color brown = new Color(0.42f, 0.22f, 0.09f);
    public Color burnt = new Color(0.07f, 0.055f, 0.05f);
    public float sendInterval = 0.5f;
    public AudioSource audioSrc;          // 효과음 (2026-09-24): 먹는 순간 한입 — 동기화된 eaten 변화로 모두에게
    public AudioClip biteClip;
    private int lastEaten = -1;

    private float lastSend = -10f;
    private bool dirty;
    private Material mat;

    private void Start() { Apply(); }

    public void AddCook(float v)
    {
        cook = Mathf.Clamp01(cook + v);
        dirty = true;
        Apply();
        if (Time.time - lastSend > sendInterval) Flush();
    }

    public void Eat() { eaten = true; dirty = true; Apply(); Flush(); }

    public void Renew() { eaten = false; cook = 0f; dirty = true; Apply(); Flush(); }

    public void Flush()
    {
        if (!dirty || !Networking.IsOwner(gameObject)) return;
        dirty = false;
        lastSend = Time.time;
        RequestSerialization();
    }

    private bool gotFirst;
    public override void OnDeserialization()
    {
        if (!gotFirst) { gotFirst = true; lastEaten = -1; }   // 입장 직후 첫 동기화는 소리 없이 (늦게 들어온 사람에게 따르기·한입이 울리지 않게)
        Apply();
    }

    private void Apply()
    {
        int e = eaten ? 1 : 0;
        if (lastEaten == 0 && e == 1 && audioSrc != null && biteClip != null) audioSrc.PlayOneShot(biteClip, 0.9f);
        lastEaten = e;
        if (marsh == null) return;
        marsh.enabled = !eaten;
        if (mat == null) mat = marsh.material;
        Color c;
        if (cook < 0.45f) c = Color.Lerp(raw, golden, cook / 0.45f);
        else if (cook < 0.75f) c = Color.Lerp(golden, brown, (cook - 0.45f) / 0.30f);
        else c = Color.Lerp(brown, burnt, (cook - 0.75f) / 0.25f);
        mat.color = c;
    }
}
