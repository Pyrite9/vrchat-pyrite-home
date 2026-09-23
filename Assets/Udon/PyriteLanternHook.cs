using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

// 들고 다닐 수 있는 랜턴.
//  - 위치 동기화는 같은 오브젝트의 VRCObjectSync 가 한다.
//  - 놓은 자리에 그대로 떠 있는다(kinematic). 스탠드 고리 근처에서 놓으면 고리에 다시 걸린다.
[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteLanternHook : UdonSharpBehaviour
{
    public Transform hook;          // 걸려 있을 때의 루트 위치·회전
    public float snapRadius = 0.8f;

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
