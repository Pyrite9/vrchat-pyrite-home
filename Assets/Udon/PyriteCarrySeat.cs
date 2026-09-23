// 들고 다니는 의자의 앉는 자리 — 누르면 앉고, 누가 앉아 있는 동안은 아무도 들 수 없다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteCarrySeat : UdonSharpBehaviour
{
    public VRCStation station;
    public VRCPickup pickup;

    public override void Interact()
    {
        if (station != null) station.UseStation(VRC.SDKBase.Networking.LocalPlayer);
    }

    public override void OnStationEntered(VRC.SDKBase.VRCPlayerApi player)
    {
        if (pickup != null) pickup.pickupable = false;
    }

    public override void OnStationExited(VRC.SDKBase.VRCPlayerApi player)
    {
        if (pickup != null) pickup.pickupable = true;
    }
}
