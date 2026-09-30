// 들고 다니는 접이식 의자 — 등받이를 잡아 옮기고, 놓으면 땅에 똑바로 선다 (위치는 VRCObjectSync 가 모두에게 맞춘다)
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteCarryChair : UdonSharpBehaviour
{
    public int groundMask = 1 | (1 << 11);   // Default, Environment

    private float dropT = -1f;
    private VRCPickup dropPk;

    // 🔴 반대손으로 고쳐 잡으면 VRChat 은 OnDrop → OnPickup 을 보낸다(관리자 2026-09-30 23:30 재현). 놓기 처리를 바로 하면
    //  새 손이 잡은 물건을 세우기·물리 켜기로 덮어써 굳는다 → dropT 초 미루고, 그 사이 다시 잡혀 있으면 취소
    public override void OnDrop() { dropT = 0.12f; }

    private void Update() { DropTick(); }

    private void DropTick()
    {
        if (dropT < 0f) return;
        if (dropPk == null) dropPk = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (dropPk != null && dropPk.IsHeld) { dropT = -1f; return; }   // 손 바꾸기 → 취소
        dropT -= Time.deltaTime;
        if (dropT <= 0f) { dropT = -1f; Settle(); }
    }


    public void Settle()
    {
        Vector3 p = transform.position;
        RaycastHit hit;
        if (Physics.Raycast(p + Vector3.up * 1.0f, Vector3.down, out hit, 4f, groundMask)) p.y = hit.point.y;
        float yaw = transform.eulerAngles.y;
        transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
    }
}
