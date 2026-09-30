// 머그 — 들고(좌클릭), 든 채 좌클릭(사용)으로 한 모금(여섯 모금이면 빈 잔), 우클릭으로 놓으면 똑바로 선다
//  채움은 자식 PyriteMugState(Manual 동기화)
//  데스크톱: 들고 있을 때 손 방향 대신 똑바로 세우고 손잡이를 드는 사람 오른쪽으로 (visual 피벗 = 손잡이) — 랜턴과 같은 방식
//  VR (2026-09-30): 각도 고정 없음. 잡은 자세 그대로 손을 따라간다(Any + AutoHold No = 쥐는 동안만 든다)
//   잔을 입가(머리 앞 mouthDist 안)로 가져가 sipTilt° 이상 기울이면 한 모금 (sipCooldown 초마다). 트리거(사용)도 한 모금
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteMug : UdonSharpBehaviour
{
    public PyriteMugState state;
    public Transform visual;            // 몸체 (피벗 = 손잡이)
    public bool vrFreeGrip = true;      // VR: 잡은 자세 그대로 (false 면 옛 방식)
    public bool vrHoldToCarry = true;   // VR: 쥐는 동안만 든다 (AutoHold No)
    public float sipTilt = 45f;         // VR: 잔 기울기(°) — 똑바로 = 0
    public float mouthDist = 0.16f;     // VR: 잔 입구 ↔ 입 거리 (m)
    public float sipCooldown = 1.1f;
    private float sipT = -1f;
    private float tilt;
    private float sipCool;
    private VRCPickup pk;
    private bool vrLocal;

    private void Start()
    {
        pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        VRCPlayerApi lp = Networking.LocalPlayer;
        vrLocal = Utilities.IsValid(lp) && lp.IsUserInVR();
        if (vrLocal && pk != null)
        {
            if (vrFreeGrip) pk.orientation = VRC_Pickup.PickupOrientation.Any;
            if (vrHoldToCarry) pk.AutoHold = VRC_Pickup.AutoHoldMode.No;
        }
    }

    public override void OnPickup()
    {
        sipCool = 0f;
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null || sipT >= 0f) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        if (!(vrLocal && vrFreeGrip)) sipT = 0f;     // 기울이는 연출은 데스크톱만 (VR 은 손이 기울인다)
        state.Sip();
        sipCool = sipCooldown;
    }

    private void Update()
    {
        if (sipCool > 0f) sipCool -= Time.deltaTime;
        if (vrLocal && vrFreeGrip && pk != null && pk.IsHeld && state != null && sipCool <= 0f && Networking.IsOwner(gameObject)) VrSip();
        tilt = 0f;
        if (sipT < 0f) return;
        sipT += Time.deltaTime;
        float a = sipT < 0.35f ? sipT / 0.35f : Mathf.Max(0f, 1f - (sipT - 0.8f) / 0.35f);
        tilt = -a * 40f;
        if (sipT > 1.15f) { sipT = -1f; tilt = 0f; }
    }

    // VR: 입가에서 기울이면 한 모금
    private void VrSip()
    {
        if (state.fill <= 0.01f) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 mouth = h.position + h.rotation * new Vector3(0f, -0.07f, 0.07f);
        Vector3 rim = transform.TransformPoint(0f, 0.09f, 0f);          // 잔 입구 가운데 (루트 = 잔 바닥)
        if ((rim - mouth).sqrMagnitude > mouthDist * mouthDist) return;
        if (Vector3.Angle(transform.up, Vector3.up) < sipTilt) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(lp, state.gameObject);
        state.Sip();
        sipCool = sipCooldown;
    }

    public override void PostLateUpdate()
    {
        if (visual == null) return;
        if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        VRCPlayerApi p = (pk != null && pk.IsHeld) ? pk.currentPlayer : null;
        if (Utilities.IsValid(p) && !(vrFreeGrip && p.IsUserInVR()))
        {
            Vector3 f = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
            // 로컬 +X(손잡이) 가 시선 오른쪽 → yaw 를 시선 yaw 그대로
            visual.rotation = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f) * Quaternion.Euler(tilt, 0f, 0f);
        }
        else visual.localRotation = Quaternion.Euler(tilt, 0f, 0f);
    }

    public override void OnDrop()
    {
        sipT = -1f; tilt = 0f;
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        // 보이던 방향의 yaw 로 똑바로 세우되, 손잡이(visual 피벗) 자리는 그대로 — 기울여 들고 있다 놓아도 튀지 않게
        Transform t = visual != null ? visual : transform;
        Quaternion up = Quaternion.Euler(0f, YawOf(t), 0f);
        Vector3 o = visual != null ? visual.position - up * visual.localPosition : transform.position;
        transform.SetPositionAndRotation(o, up);
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }

    private float YawOf(Transform t)
    {
        Vector3 f = t.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.04f) { f = -t.up; f.y = 0f; }            // 거의 눕혀 들었을 때
        if (f.sqrMagnitude < 0.0001f) return t.eulerAngles.y;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }
}
