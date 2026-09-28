// PyriteTeleportDoor.cs — 누르면 나(로컬 플레이어)를 target 으로 옮긴다. 동기화 없음.
// 텐트 침실: 캠프 텐트(TentDoor) → 침실 Spawn, 침실 입구 천막 → 캠프 텐트 앞 CampReturn
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteTeleportDoor : UdonSharpBehaviour
{
    public Transform target;

    public override void Interact()
    {
        VRCPlayerApi p = Networking.LocalPlayer;
        if (p == null) return;
        if (target == null) return;
        p.TeleportTo(target.position, target.rotation, VRC_SceneDescriptor.SpawnOrientation.AlignPlayerWithSpawnPoint, false);
    }
}
