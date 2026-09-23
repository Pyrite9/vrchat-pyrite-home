// 머그 — 들고(좌클릭), 든 채 좌클릭(사용)으로 한 모금(3모금이면 빈 잔), 우클릭으로 놓으면 똑바로 선다
//  채움은 자식 PyriteMugState(Manual 동기화)
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteMug : UdonSharpBehaviour
{
    public PyriteMugState state;
    public Transform visual;            // 기울일 몸체
    private float sipT = -1f;

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
        if (sipT < 0f) return;
        sipT += Time.deltaTime;
        float a = sipT < 0.35f ? sipT / 0.35f : Mathf.Max(0f, 1f - (sipT - 0.8f) / 0.35f);
        if (visual != null) visual.localRotation = Quaternion.Euler(-a * 45f, 0f, 0f);
        if (sipT > 1.15f) { sipT = -1f; if (visual != null) visual.localRotation = Quaternion.identity; }
    }

    public override void OnDrop()
    {
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }
}
