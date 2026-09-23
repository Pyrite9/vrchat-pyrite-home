// 마시멜로 꼬치 — 들고(좌클릭), 불에 대면 익고, 든 채 좌클릭(사용)으로 먹기 / 다시 좌클릭으로 새 마시멜로, 우클릭으로 놓기
//  위치는 같은 오브젝트의 VRCObjectSync. 익은 정도·먹음은 자식 State(Manual 동기화)가 모두에게 맞춘다
//  (VRCObjectSync 와 Manual 동기화 Udon 은 한 오브젝트에 같이 둘 수 없어서 자식으로 뺐다)
//  놓을 때 꽂이(slots) 근처면 꽂이에 꽂히고, 아니면 떨어진다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteSkewer : UdonSharpBehaviour
{
    public PyriteSkewerState state;
    public Transform tip;               // 마시멜로 중심
    public Transform fire;              // 불 중심
    public float fireRadius = 0.5f;     // 불 중심에서 수평 거리
    public float fireLow = -0.3f;       // 불 중심 기준 높이 범위
    public float fireHigh = 0.8f;
    public float cookSeconds = 20f;     // 0 → 1(탄) 까지 (불 가운데일수록 빠르다)
    public Transform[] slots;           // 꽂이 자리
    public Transform[] others;          // 다른 꼬치 (자리 점유 확인)
    public float snapRadius = 0.6f;

    private bool held;

    public override void OnPickup()
    {
        held = true;
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        if (state.eaten) state.Renew(); else state.Eat();
    }

    public override void OnDrop()
    {
        held = false;
        if (state != null) state.Flush();
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        Transform s = FreeSlotNear();
        if (s != null)
        {
            transform.SetPositionAndRotation(s.position, s.rotation);
            if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
            return;
        }
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); }
    }

    private void Update()
    {
        if (!held || state == null || tip == null || fire == null || state.eaten) return;
        Vector3 d = tip.position - fire.position;
        float dy = d.y; d.y = 0f;
        float r = d.magnitude;
        if (r < fireRadius && dy > fireLow && dy < fireHigh)
        {
            float k = 1f - r / fireRadius;
            state.AddCook(Time.deltaTime / cookSeconds * (0.5f + k));
        }
    }

    private Transform FreeSlotNear()
    {
        if (slots == null) return null;
        Transform best = null;
        float bestD = snapRadius;
        for (int i = 0; i < slots.Length; i++)
        {
            Transform s = slots[i];
            if (s == null) continue;
            float d = Vector3.Distance(transform.position, s.position);
            if (d > bestD || Occupied(s)) continue;
            best = s; bestD = d;
        }
        return best;
    }

    private bool Occupied(Transform s)
    {
        if (others == null) return false;
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] == null || others[i] == transform) continue;
            if (Vector3.Distance(others[i].position, s.position) < 0.05f) return true;
        }
        return false;
    }
}
