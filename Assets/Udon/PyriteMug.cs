// 머그 — 들고(좌클릭), 든 채 좌클릭(사용)으로 한 모금(여섯 모금이면 빈 잔), 우클릭으로 놓으면 똑바로 선다
//  채움은 자식 PyriteMugState(Manual 동기화)
//  들고 있을 때 방향: 손 방향 대신 똑바로 세우고 손잡이를 드는 사람 오른쪽으로 (visual 피벗 = 손잡이) — 랜턴과 같은 방식
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteMug : UdonSharpBehaviour
{
    public PyriteMugState state;
    public Transform visual;            // 몸체 (피벗 = 손잡이)
    private float sipT = -1f;
    private float tilt;
    private VRCPickup pk;

    public override void OnPickup()
    {
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null || sipT >= 0f) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        sipT = 0f;
        state.Sip();
    }

    private void Update()
    {
        tilt = 0f;
        if (sipT < 0f) return;
        sipT += Time.deltaTime;
        float a = sipT < 0.35f ? sipT / 0.35f : Mathf.Max(0f, 1f - (sipT - 0.8f) / 0.35f);
        tilt = -a * 40f;
        if (sipT > 1.15f) { sipT = -1f; tilt = 0f; }
    }

    public override void PostLateUpdate()
    {
        if (visual == null) return;
        if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        VRCPlayerApi p = (pk != null && pk.IsHeld) ? pk.currentPlayer : null;
        if (Utilities.IsValid(p))
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
        float yaw = visual != null ? visual.eulerAngles.y : transform.eulerAngles.y;
        Vector3 o = visual != null ? visual.TransformPoint(-visual.localPosition) : transform.position;   // 보이는 잔 바닥
        transform.SetPositionAndRotation(o, Quaternion.Euler(0f, yaw, 0f));
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }
}
