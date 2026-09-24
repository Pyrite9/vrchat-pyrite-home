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
    public float snapRadius = 0.6f;      // 꽂이 자리와의 수평 거리
    public Transform visual;            // 몸체 (피벗 = 손잡이). 들 때 방향은 손을 그대로 따른다 — 관리자: 초기 버전이 더 좋음 (머리 기준 세우기 폐기)

    private bool held;
    private Transform snapSlot;         // 꽂은 자리 — 놓은 직후 VRChat 이 리지드바디 상태를 되돌려 빠지는 걸 막으려고 몇 프레임 뒤 다시 꽂는다

    public override void OnPickup()
    {
        held = true;
        snapSlot = null;
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
        Debug.Log("[PyriteSkewer] " + gameObject.name + " drop → " + (s != null ? s.name : "없음(떨어뜨림)"));
        if (s != null)
        {
            snapSlot = s;
            ReSnap();
            SendCustomEventDelayedFrames(nameof(ReSnap), 1);
            SendCustomEventDelayedFrames(nameof(ReSnap), 10);
            return;
        }
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); }
    }

    public void ReSnap()
    {
        if (snapSlot == null || held || !Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        transform.SetPositionAndRotation(snapSlot.position, snapSlot.rotation);
        if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
        Rigidbody rb = (Rigidbody)GetComponent(typeof(Rigidbody));
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
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
            // 🔴 3D 거리 0.6 m 로는 손 높이(꽂이보다 1 m 위)에서 놓으면 안 꽂혔다 → 수평 거리만 보고, 높이는 꽂이 위 1.8 m 까지 허용
            Vector3 v = transform.position - s.position;
            float dy = v.y; v.y = 0f;
            float d = v.magnitude;
            if (dy < -0.3f || dy > 1.8f) continue;
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
