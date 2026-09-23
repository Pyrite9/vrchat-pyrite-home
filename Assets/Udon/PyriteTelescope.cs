// 망원경 — 누르면(Interact) 제자리에 서서 머리가 보는 쪽을 망원경으로 본다 (로컬, 나만 보임)
//  경통이 머리 방향을 따라 돌고, 경통 끝 카메라(좁은 화각)가 RT 에 그린 것을 머리 앞 둥근 화면으로 보여 준다
//  점프로 나온다. 멀어지거나(순간이동 등) 다시 누르면 나온다
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteTelescope : UdonSharpBehaviour
{
    public Transform yawPivot;          // 수평 회전
    public Transform pitchPivot;        // 경통 (yawPivot 의 자식)
    public GameObject cam;              // 경통 끝 카메라 (RT)
    public Transform view;              // 머리 앞 둥근 화면
    public float viewDistance = 0.12f;
    public float minPitch = -10f;       // 올려다보기 +, 내려다보기 -
    public float maxPitch = 75f;
    public float exitDistance = 2.5f;

    private bool viewing;
    private VRCPlayerApi lp;

    private void Start()
    {
        if (cam != null) cam.SetActive(false);
        if (view != null) view.gameObject.SetActive(false);
    }

    public override void Interact()
    {
        if (viewing) Exit(); else Enter();
    }

    private void Enter()
    {
        lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        viewing = true;
        if (cam != null) cam.SetActive(true);
        if (view != null) view.gameObject.SetActive(true);
        lp.Immobilize(true);
    }

    public void Exit()
    {
        viewing = false;
        if (cam != null) cam.SetActive(false);
        if (view != null) view.gameObject.SetActive(false);
        if (Utilities.IsValid(lp)) lp.Immobilize(false);
    }

    public override void InputJump(bool value, UdonInputEventArgs args)
    {
        if (value && viewing) Exit();
    }

    public override void PostLateUpdate()
    {
        if (!viewing) return;
        if (!Utilities.IsValid(lp)) { viewing = false; return; }
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        if (view != null) view.SetPositionAndRotation(h.position + h.rotation * Vector3.forward * viewDistance, h.rotation);
        Vector3 f = h.rotation * Vector3.forward;
        float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        float up = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
        up = Mathf.Clamp(up, minPitch, maxPitch);
        if (yawPivot != null) yawPivot.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(-up, 0f, 0f);
        if ((lp.GetPosition() - transform.position).sqrMagnitude > exitDistance * exitDistance) Exit();
    }

    public override void OnPlayerRespawn(VRCPlayerApi player)
    {
        if (viewing && Utilities.IsValid(player) && player.isLocal) Exit();
    }
}
