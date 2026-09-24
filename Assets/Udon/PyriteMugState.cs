// 머그 속 음료 (머그의 자식) — fill 0~1 을 모두에게 맞춘다. 한 모금 = 1/6, 차 있으면 김이 난다
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteMugState : UdonSharpBehaviour
{
    [UdonSynced] public float fill;
    public Transform liquid;            // 음료 윗면 (원판)
    public float emptyY = 0.012f;
    public float fullY = 0.072f;
    public float sip = 1f / 6f;       // 여섯 모금
    public ParticleSystem steam;
    public AudioSource audioSrc;          // 효과음 (2026-09-24): 차오르면 따르기, 줄면 마시기 — 동기화된 fill 변화로 모두에게
    public AudioClip pourClip;
    public AudioClip sipClip;
    private float lastFill = -1f;

    private void Start() { Apply(); }

    public void Fill() { fill = 1f; Apply(); RequestSerialization(); }

    public void Sip()
    {
        if (fill <= 0.01f) return;
        fill = Mathf.Max(0f, fill - sip);
        if (fill < 0.02f) fill = 0f;
        Apply();
        RequestSerialization();
    }

    private bool gotFirst;
    public override void OnDeserialization()
    {
        if (!gotFirst) { gotFirst = true; lastFill = -1f; }   // 입장 직후 첫 동기화는 소리 없이 (늦게 들어온 사람에게 따르기·한입이 울리지 않게)
        Apply();
    }

    private void Apply()
    {
        if (lastFill >= 0f && audioSrc != null)
        {
            if (fill > lastFill + 0.01f && pourClip != null) audioSrc.PlayOneShot(pourClip, 0.8f);
            else if (fill < lastFill - 0.01f && sipClip != null) audioSrc.PlayOneShot(sipClip, 0.9f);
        }
        lastFill = fill;
        bool has = fill > 0.01f;
        if (liquid != null)
        {
            if (liquid.gameObject.activeSelf != has) liquid.gameObject.SetActive(has);
            Vector3 p = liquid.localPosition; p.y = Mathf.Lerp(emptyY, fullY, fill); liquid.localPosition = p;
        }
        if (steam != null)
        {
            if (has && !steam.isPlaying) steam.Play();
            else if (!has && steam.isPlaying) steam.Stop();
        }
    }
}
