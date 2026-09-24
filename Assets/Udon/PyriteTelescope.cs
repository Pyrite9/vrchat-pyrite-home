// 망원경 — 접안렌즈 들여다보기 (관리자: 머리 앞에 띄우는 화면은 사진 찍을 때 불쾌 → 폐기)
//  접안부 끝 지름 6 cm 원형 화면에 경통 끝 카메라(RT)를 보여 준다. 몸은 고정하지 않는다
//  얼굴을 접안부 가까이(engageDist) 대고 경통 쪽을 보면 경통이 머리 방향을 따라 돈다 → 멀어지면(releaseDist) 멈춘다
//  방향(yaw/pitch)은 모두에게 동기화(Manual, 5 Hz) — 다른 사람도 경통이 도는 걸 본다
//  카메라는 내 머리가 renderDist 안일 때만 켠다 (성능)
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteTelescope : UdonSharpBehaviour
{
    public Transform yawPivot;          // 수평 회전
    public Transform pitchPivot;        // 경통 (yawPivot 의 자식, +Z = 대물렌즈 쪽)
    public Transform eyepiece;          // 접안 화면 중심
    public GameObject cam;              // 경통 끝 카메라 (RT)
    public float engageDist = 0.5f;
    public float releaseDist = 0.85f;
    public float engageDot = 0.55f;     // 머리 앞 방향 · 경통 방향
    public float renderDist = 3f;
    public float minPitch = -10f;       // 올려다보기 +, 내려다보기 -
    public float maxPitch = 75f;

    [UdonSynced] public float yaw = 180f;
    [UdonSynced] public float pitch = 18f;

    private bool engaged;
    private float lastSend = -10f;
    private float curYaw, curPitch;

    private void Start()
    {
        curYaw = yaw; curPitch = pitch;
        Apply();
        if (cam != null) cam.SetActive(false);
    }

    public override void OnDeserialization() { }

    public override void PostLateUpdate()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp) || eyepiece == null) return;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        float d = Vector3.Distance(h.position, eyepiece.position);
        Vector3 f = h.rotation * Vector3.forward;

        if (cam != null)
        {
            bool near = d < renderDist;
            if (cam.activeSelf != near) cam.SetActive(near);
        }

        if (!engaged && d < engageDist && pitchPivot != null && Vector3.Dot(f, pitchPivot.forward) > engageDot) engaged = true;
        else if (engaged && d > releaseDist) { engaged = false; if (Networking.IsOwner(gameObject)) RequestSerialization(); }

        if (engaged)
        {
            yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, minPitch, maxPitch);
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(lp, gameObject);
            if (Time.time - lastSend > 0.2f) { lastSend = Time.time; RequestSerialization(); }
            curYaw = yaw; curPitch = pitch;
        }
        else
        {
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            curYaw = Mathf.LerpAngle(curYaw, yaw, k);
            curPitch = Mathf.Lerp(curPitch, pitch, k);
        }
        Apply();
    }

    private void Apply()
    {
        if (yawPivot != null) yawPivot.rotation = Quaternion.Euler(0f, curYaw, 0f);
        if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(-curPitch, 0f, 0f);
    }
}
