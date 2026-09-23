using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 들고 다닐 수 있는 랜턴.
//  - 위치 동기화는 같은 오브젝트의 VRCObjectSync 가 한다.
//  - 든 채로 사용(좌클릭): 걸이 근처(snapRadius)면 걸이에 건다. 아니면 손에서 놓아 땅에 떨어뜨린다(중력).
//  - 그냥 놓아도(드롭) 걸이 근처면 걸리고, 아니면 떨어진다.
//  - 걸이에 이미 다른 랜턴이 있으면 그 걸이는 건너뛴다.
//  - body(손잡이 꼭대기가 피벗)는 매 프레임 세로로 세운다 → 손 방향과 관계없이 매달린 모양.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteLanternHook : UdonSharpBehaviour
{
    public Transform hook;          // (예전 단일 걸이 — hooks 가 비어 있을 때만 쓴다)
    public Transform[] hooks;       // 랜턴 걸이들
    public Transform[] others;      // 다른 랜턴 루트 (걸이 점유 확인)
    public float snapRadius = 0.8f;
    public Transform body;          // 세워 둘 몸체 (피벗 = 손잡이 꼭대기)
    public float swing = 10f;       // 클수록 빨리 세워진다 (살짝 흔들리는 느낌)

    private bool hangNext = false;

    public override void PostLateUpdate()
    {
        if (body == null) return;
        Vector3 f = transform.forward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) { f = transform.up; f.y = 0f; }
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        Quaternion target = Quaternion.LookRotation(f.normalized, Vector3.up);
        float k = 1f - Mathf.Exp(-swing * Time.deltaTime);
        body.rotation = Quaternion.Slerp(body.rotation, target, k);
    }

    public override void OnPickup()
    {
        hangNext = false;
    }

    public override void OnPickupUseDown()
    {
        hangNext = true;
        VRCPickup pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (pk != null) pk.Drop();
    }

    public override void OnDrop()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        Transform h = FreeHookNear();
        hangNext = false;
        if (h != null)
        {
            transform.SetPositionAndRotation(h.position, h.rotation);
            if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
            return;
        }
        // 떨어뜨린다: 기울기 없이 세우고 중력
        float yaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }

    private Transform FreeHookNear()
    {
        Transform best = null;
        float bestD = snapRadius;
        int n = hooks != null ? hooks.Length : 0;
        for (int i = 0; i < n + 1; i++)
        {
            Transform h = i < n ? hooks[i] : (n == 0 ? hook : null);
            if (h == null) continue;
            float d = Vector3.Distance(transform.position, h.position);
            if (d > bestD) continue;
            if (Occupied(h)) continue;
            best = h; bestD = d;
        }
        return best;
    }

    private bool Occupied(Transform h)
    {
        if (others == null) return false;
        for (int i = 0; i < others.Length; i++)
        {
            if (others[i] == null || others[i] == transform) continue;
            if (Vector3.Distance(others[i].position, h.position) < 0.15f) return true;
        }
        return false;
    }
}
