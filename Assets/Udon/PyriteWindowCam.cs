// PyriteWindowCam — 침실 창밖 실시간 풍경 ("창 = 캠프로 열린 구멍"). 로컬 전용(동기화 없음)
//  로컬 플레이어가 침실 근처에 있을 때만 캠프 카메라를 켠다. 매 프레임 머리 위치·방향을 침실 → 캠프로 옮겨 카메라를 둔다
//  침실 점 p(방 로컬) → 캠프 camPos + Ryaw(camYaw)·(p − eyeLocal). 방향도 같은 회전. 창 면(방 로컬 z = planeZ) 앞의 것은 near 로 자른다
//  Pyrite/Backdrop 은 시선 방향을 카메라 화면에 투영하므로 카메라가 머리 자리에 있으면 시차까지 맞는다(양눈은 머리 가운데 1대로 근사)
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteWindowCam : UdonSharpBehaviour
{
    public Camera cam;
    public Transform room;
    public Material mat;
    public float radius = 12f;
    public bool allowLive = true;
    public bool followHead = true;
    public Vector3 camPos = new Vector3(-4.9f, 2.55f, 54.6f);
    public float camYaw = 210f;
    public Vector3 eyeLocal = new Vector3(0f, 0.74f, 0f);
    public float planeZ = 2.5f;

    private float next;
    private bool on;

    void Start()
    {
        // 기준점 = 이 오브젝트(BedroomWindowCam)를 씬에 놓은 자리·방향. 에디터에서 옮기면 그대로 따라간다
        camPos = transform.position;
        camYaw = transform.eulerAngles.y;
        mat.SetFloat("_Yaw", camYaw);
        cam.enabled = false;
        mat.SetFloat("_LiveOn", 0f);
    }

    void Update()
    {
        if (Time.time < next) return;
        next = Time.time + 0.5f;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        bool inside = allowLive && Vector3.Distance(lp.GetPosition(), room.position) < radius;
        if (inside == on) return;
        on = inside;
        cam.enabled = on;
        mat.SetFloat("_LiveOn", on ? 1f : 0f);
    }

    public override void PostLateUpdate()
    {
        if (!on || !followHead) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        VRCPlayerApi.TrackingData head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 local = room.InverseTransformPoint(head.position);
        Quaternion lrot = Quaternion.Inverse(room.rotation) * head.rotation;
        Quaternion yaw = Quaternion.Euler(0f, camYaw, 0f);
        Vector3 wpos = camPos + yaw * (local - eyeLocal);
        Quaternion wrot = yaw * lrot;
        cam.transform.SetPositionAndRotation(wpos, wrot);
        // 창 면 앞(캠프 텐트 등)을 자르기: 시선이 창 쪽일수록 near 를 창 면 가까이
        Vector3 fwdLocal = lrot * Vector3.forward;
        float d = planeZ - local.z;
        float near = 0.05f;
        if (d > 0f && fwdLocal.z > 0.2f) near = Mathf.Max(0.05f, (d * fwdLocal.z - 0.35f) * 0.9f);
        cam.nearClipPlane = near;
        Transform t = cam.transform;
        mat.SetVector("_LiveFwd", t.forward);
        mat.SetVector("_LiveRight", t.right);
        mat.SetVector("_LiveUp", t.up);
    }

    public void SetAllowLive(bool v)
    {
        allowLive = v;
        next = 0f;
    }
}
