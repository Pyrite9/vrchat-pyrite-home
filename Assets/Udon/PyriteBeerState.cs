// 병맥주 상태 (병의 자식 State, Manual) — 뚜껑 땄는지 · 남은 모금을 모두에게 맞춘다
//  동기화 값이 바뀌는 걸 보고 각자 연출: 딴 순간 = 뽕 + 탄산 소리 · 병뚜껑 날아감 · 거품 / 모금이 줄면 마시는 소리
//  입장 직후 첫 동기화는 조용히 (머그와 같음)
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteBeerState : UdonSharpBehaviour
{
    [UdonSynced] public bool opened;
    [UdonSynced] public int sips = 8;
    public int maxSips = 8;
    public PyriteBeer beer;
    public AudioSource audioSrc;
    public AudioClip openClip;
    public AudioClip sipClip;

    private bool lastOpened;
    private int lastSips = -1;
    private bool gotFirst;

    private void Start() { Apply(); }

    public void Open()
    {
        if (opened) return;
        opened = true;
        Apply();
        RequestSerialization();
    }

    public void Sip()
    {
        if (!opened || sips <= 0) return;
        sips = sips - 1;
        Apply();
        RequestSerialization();
    }

    public void ResetFresh()
    {
        opened = false;
        sips = maxSips;
        Apply();
        RequestSerialization();
    }

    public override void OnDeserialization()
    {
        if (!gotFirst) { gotFirst = true; lastSips = -1; }
        Apply();
    }

    private void Apply()
    {
        bool fx = lastSips >= 0;
        if (fx && opened && !lastOpened)
        {
            if (audioSrc != null && openClip != null) audioSrc.PlayOneShot(openClip, 1f);
            if (beer != null) beer.PopFx();
        }
        else if (fx && opened && sips < lastSips)
        {
            if (audioSrc != null && sipClip != null) audioSrc.PlayOneShot(sipClip, 0.9f);
        }
        lastOpened = opened;
        lastSips = sips;
        if (beer != null) beer.ShowState(opened, sips, maxSips);
    }
}
