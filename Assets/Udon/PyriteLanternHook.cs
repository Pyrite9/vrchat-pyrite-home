using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 들고 다닐 수 있는 랜턴.
//  - 위치 동기화는 같은 오브젝트의 VRCObjectSync 가 한다.
//  - 놓은 자리에 그대로 떠 있는다(kinematic). 스탠드 고리 근처에서 놓으면 고리에 다시 걸린다.
//  - body(손잡이 꼭대기가 피벗)는 매 프레임 세로로 세운다 → 손 방향과 관계없이 매달린 모양.
//    모든 클라이언트에서 돌기 때문에 남이 들고 있는 랜턴도 똑바로 보인다.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteLanternHook : UdonSharpBehaviour
{
    public Transform hook;          // 걸려 있을 때의 루트 위치·회전
    public float snapRadius = 0.8f;
    public Transform body;          // 세워 둘 몸체 (피벗 = 손잡이 꼭대기)
    public float swing = 10f;       // 클수록 빨리 세워진다 (살짝 흔들리는 느낌)

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

    public override void OnDrop()
    {
        if (hook == null) return;
        if (!Networking.IsOwner(gameObject)) return;
        if (Vector3.Distance(transform.position, hook.position) > snapRadius) return;

        transform.SetPositionAndRotation(hook.position, hook.rotation);
        var sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.FlagDiscontinuity();
    }
}
