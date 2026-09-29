// 들고 다니는 의자의 앉는 자리 — 누르면 앉고, pickup 이 연결돼 있으면 누가 앉아 있는 동안은 아무도 들 수 없다
//  (침실 빈백은 pickup = null → 앉아 있어도 들 수 있음, 2026-09-30 관리자). occupied = 누가 앉아 있음 (개수 풀이 빼기·제자리에서 건너뜀)
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteCarrySeat : UdonSharpBehaviour
{
    public VRCStation station;
    public VRCPickup pickup;
    private bool occupied;
    public bool IsOccupied() { return occupied; }

    public override void Interact()
    {
        if (station != null) station.UseStation(VRC.SDKBase.Networking.LocalPlayer);
    }

    public override void OnStationEntered(VRC.SDKBase.VRCPlayerApi player)
    {
        occupied = true;
        if (pickup != null) pickup.pickupable = false;
    }

    public override void OnStationExited(VRC.SDKBase.VRCPlayerApi player)
    {
        occupied = false;
        if (pickup != null) pickup.pickupable = true;
    }
}
