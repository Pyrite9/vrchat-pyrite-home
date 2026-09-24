// 새벽 호수 물안개 — 04:18 부터 짙어져 05:18~06:48 가장 짙고 08:00 에 사라진다. 해 뜨기 전 푸른 회색 → 해 뜬 뒤 따뜻한 색
//  파티클(수평 판)은 계속 돌고, 머티리얼 알파만 시간대로. 알파 0 이면 렌더러를 꺼서 비용 0
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteMist : UdonSharpBehaviour
{
    public PyriteDayCycle cycle;
    public Renderer mistRenderer;
    public float maxAlpha = 0.85f;
    public float inStart = 4.3f, inEnd = 5.3f, outStart = 6.8f, outEnd = 8.0f;
    public Color cool = new Color(0.58f, 0.64f, 0.74f, 1f);
    public Color warm = new Color(0.98f, 0.88f, 0.78f, 1f);
    public float warmFrom = 6.0f, warmTo = 6.6f;

    private Material mat;
    private float next;

    private void Start()
    {
        if (mistRenderer != null) mat = mistRenderer.material;
        Apply();
    }

    private void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.25f;
        Apply();
    }

    private void Apply()
    {
        if (cycle == null || mat == null) return;
        float h = cycle.currentHour;
        float a = 0f;
        if (h >= inStart && h < inEnd) a = (h - inStart) / (inEnd - inStart);
        else if (h >= inEnd && h < outStart) a = 1f;
        else if (h >= outStart && h < outEnd) a = 1f - (h - outStart) / (outEnd - outStart);
        a = a * a * (3f - 2f * a);
        bool on = a > 0.003f;
        if (mistRenderer.enabled != on) mistRenderer.enabled = on;
        if (!on) return;
        float w = Mathf.Clamp01((h - warmFrom) / (warmTo - warmFrom));
        mat.SetColor("_Color", Color.Lerp(cool, warm, w));
        mat.SetFloat("_Alpha", a * maxAlpha);
    }
}
