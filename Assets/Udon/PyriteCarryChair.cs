// 들고 다니는 접이식 의자 — 등받이를 잡아 옮기고, 놓으면 땅에 똑바로 선다 (위치는 VRCObjectSync 가 모두에게 맞춘다)
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteCarryChair : UdonSharpBehaviour
{
    public int groundMask = 1 | (1 << 11);   // Default, Environment

    public override void OnDrop() { Settle(); }

    public void Settle()
    {
        Vector3 p = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out hit, 4f, groundMask)) p.y = hit.point.y;
        float yaw = transform.eulerAngles.y;
        transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
    }
}
