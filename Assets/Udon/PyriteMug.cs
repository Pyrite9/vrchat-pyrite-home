// 머그 — 들고, 든 채 사용(트리거·좌클릭)으로 한 모금(여섯 모금이면 빈 잔). 채움은 자식 PyriteMugState(Manual 동기화)
//  2026-09-30 23:19: PC/VR 구분 제거. 픽업은 에디터에서 Any(잡은 자세 그대로)로 고정(Z54a), 스크립트는 회전을 건드리지 않는다
//   (런타임에 VR 판정으로 orientation 을 바꾸던 방식은 다시 잡으면 PC 방식으로 돌아가는 일이 있었다 — 관리자 인게임)
//  잔 입구를 입가(mouthDist 안)로 가져가 sipTilt° 이상 기울여도 한 모금 (VR 에서 주로)
//  놓으면: 보이던 방향의 yaw 로 똑바로 세우되 손잡이(visual 피벗) 자리는 그대로 — 기울여 들다 놓아도 튀지 않게
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteMug : UdonSharpBehaviour
{
    public PyriteMugState state;
    public Transform visual;            // 몸체 (피벗 = 손잡이). 회전은 항상 identity
    public float sipTilt = 45f;         // 잔 기울기(°) — 똑바로 = 0
    public float mouthDist = 0.16f;     // 잔 입구 ↔ 입 거리 (m)
    public float sipCooldown = 1.1f;
    private float sipCool;
    private VRCPickup pk;

    private void Start()
    {
        pk = (VRCPickup)GetComponent(typeof(VRCPickup));
    }

    public override void OnPickup()
    {
        sipCool = 0f;
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null || sipCool > 0f) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        state.Sip();
        sipCool = 0.35f;
    }

    private void Update()
    {
        if (sipCool > 0f) sipCool -= Time.deltaTime;
        if (pk != null && pk.IsHeld && state != null && sipCool <= 0f && Networking.IsOwner(gameObject)) MouthSip();
    }

    // 입가에서 기울이면 한 모금
    private void MouthSip()
    {
        if (state.fill <= 0.01f) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 mouth = h.position + h.rotation * new Vector3(0f, -0.07f, 0.07f);
        Vector3 rim = transform.TransformPoint(0f, 0.09f, 0f);          // 잔 입구 가운데 (루트 = 잔 바닥)
        if ((rim - mouth).sqrMagnitude > mouthDist * mouthDist) return;
        if (Vector3.Angle(transform.up, Vector3.up) < sipTilt) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(lp, state.gameObject);
        state.Sip();
        sipCool = sipCooldown;
    }

    public override void OnDrop()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
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
