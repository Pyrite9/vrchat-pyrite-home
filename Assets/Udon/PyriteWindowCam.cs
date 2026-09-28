// PyriteWindowCam — 침실 창밖 실시간 풍경. 로컬 플레이어가 침실 근처에 있을 때만 캠프 텐트 앞 카메라를 켠다(동기화 없음)
//  침실(2000, 0, 0)에서는 월드 본체가 원거리 밖이라 안 그려지므로, 이 카메라 비용 ≈ 캠프에 서 있을 때 월드를 그리는 비용(단안 1536×768)
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

    private float next;
    private bool on;

    void Start()
    {
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

    public void SetAllowLive(bool v)
    {
        allowLive = v;
        next = 0f;
    }
}
