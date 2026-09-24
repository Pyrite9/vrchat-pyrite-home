// 꽃밭 거리 컬링 — 로컬. 설정 패널의 '꽃 보이는 거리' 바가 SetDistance 로 값을 넘긴다
//  꽃밭은 덩어리 렌더러 18개(가장 큰 것 41 m) → 덩어리 단위로 켜고 끈다. 내 위치에서 덩어리 경계 상자까지 수평·수직 거리
//  distance ≥ maxDistance = 전부 켬(검사 안 함), 0 = 전부 끔. 그 사이는 0.5 초마다 다시 판정
//  설정 패널이 꺼지면 패널 스크립트는 멈추므로, 판정은 항상 켜져 있는 꽃밭 루트에서 한다
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteFlowerCull : UdonSharpBehaviour
{
    public Renderer[] renderers;
    public float maxDistance = 160f;
    public float distance = 160f;

    private Vector3[] centers;
    private Vector3[] extents;
    private bool ready;
    private float next;

    void Start() { Init(); }

    private void Init()
    {
        if (ready || renderers == null) return;
        int n = renderers.Length;
        centers = new Vector3[n]; extents = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            if (renderers[i] == null) continue;
            Bounds b = renderers[i].bounds;
            centers[i] = b.center; extents[i] = b.extents;
        }
        ready = true;
        Apply();
    }

    public void SetDistance(float d)
    {
        distance = d;
        Init();
        Apply();
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.5f;
        if (distance > 0f && distance < maxDistance) Apply();
    }

    private void Apply()
    {
        if (!ready) return;
        bool all = distance >= maxDistance, none = distance <= 0f;
        Vector3 p = Vector3.zero;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (Utilities.IsValid(lp)) p = lp.GetPosition();
        float d2 = distance * distance;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;
            bool on = all;
            if (!all && !none)
            {
                Vector3 c = centers[i], e = extents[i];
                float dx = Mathf.Max(Mathf.Abs(p.x - c.x) - e.x, 0f);
                float dy = Mathf.Max(Mathf.Abs(p.y - c.y) - e.y, 0f);
                float dz = Mathf.Max(Mathf.Abs(p.z - c.z) - e.z, 0f);
                on = dx * dx + dy * dy + dz * dz <= d2;
            }
            if (r.enabled != on) r.enabled = on;
        }
    }
}
