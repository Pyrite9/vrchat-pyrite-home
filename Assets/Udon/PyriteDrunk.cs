// 취기 효과 — 캠프 맥주를 마신 본인 화면에만 (로컬, 동기화 없음). 2026-10-05 관리자: 화면 효과만 · 2병(16모금)에 최대 · 3분에 깸 · 설정에서 켬/끔(기본 켬)
//  PyriteBeer 가 한 모금마다 AddSip() → level +1/sipsToMax (최대 1). 안 마시면 soberSeconds 에 걸쳐 1 → 0 으로 내려간다
//  화면: 전역 후처리 볼륨(PP_Drunk — 색 번짐 · 가장자리 어두움 · 빛 번짐 · 채도) 의 weight = level × (1 ± 숨쉬듯 흔들림). Udon 은 후처리 값을 직접 못 바꿔 weight 로만 조절
//  볼륨 priority 9 = 시간대(0~2)·침실(5) 위, 사용자 밝기·블룸 끔(10) 아래 → 설정에서 블룸을 끈 사람은 취해도 블룸이 안 켜진다
using UdonSharp;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteDrunk : UdonSharpBehaviour
{
    public PostProcessVolume volume;
    public int sipsToMax = 16;            // 2병
    public float soberSeconds = 180f;     // 최대에서 완전히 깨기까지
    public bool allow = true;             // 설정 토글 (끄면 화면 효과만 꺼지고 취한 정도는 그대로 흐른다)
    public float level = 0f;              // 0..1
    public float pulse = 0.14f;           // weight 흔들림 폭
    public float pulseHz = 0.22f;
    private float shown = -1f;

    public void AddSip()
    {
        level = Mathf.Min(1f, level + 1f / Mathf.Max(1, sipsToMax));
    }

    public void SetAllow(bool v)
    {
        allow = v;
    }

    private void Update()
    {
        if (level > 0f) level = Mathf.Max(0f, level - Time.deltaTime / Mathf.Max(1f, soberSeconds));
        float w = 0f;
        if (allow && level > 0f) w = Mathf.Clamp01(level * (1f + pulse * Mathf.Sin(Time.time * 6.2832f * pulseHz)));
        if (volume == null || w == shown) return;
        shown = w;
        volume.weight = w;
    }
}
