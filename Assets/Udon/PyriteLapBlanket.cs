// PyriteLapBlanket — 머스터드 빈백 양털: 앉은 사람이 누르면 무릎에 덮이고, 다시 누르거나 일어나면 등받이로 돌아감 (모두에게 동기화)
//  이 오브젝트 = 누르기 판정(BoxCollider, Default 레이어). 자식 Mesh = 무릎 담요 렌더러. drape = 등받이에 걸친 양털 렌더러
//  덮은 동안: owner(= 앉은 사람)의 허벅지 뿌리·무릎 bone 으로 매 프레임 위치·방향·크기(허벅지 길이 / refThigh) 맞춤
//  걸쳐 있을 때: 여기 앉은 사람 눈에만 판정이 자기 무릎 위에 생김 (등받이는 머리 뒤라 누르기 어려움) → 내려다보고 "Cover Lap"
//  owner 가 자리를 뜨면(0.6 초 유예) owner 가 스스로 해제. 휴머노이드 bone 이 없으면 기준 자세(refPos/refRot, 크기 1)로
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteLapBlanket : UdonSharpBehaviour
{
    public Transform sitPoint;          // 빈백 Seat/SitPoint
    public Renderer drape;              // 등받이에 걸친 양털
    public Renderer lap;                // 무릎 담요 (자식 Mesh)
    public float refThigh = 0.38f;      // 기준 아바타 허벅지 길이 (m) — 메시가 이 길이로 만들어짐
    public Vector3 refPos;              // bone 없을 때: SitPoint 로컬 위치
    public Quaternion refRot = Quaternion.identity;
    public Vector3 drapePos;            // 걸쳐 있을 때 판정 박스 자세 (부모 = 빈백 로컬)
    public Quaternion drapeRot = Quaternion.identity;
    public Vector3 drapeScale = Vector3.one;
    public float seatRadius = 0.35f;    // 골반이 SitPoint 에서 이 수평 거리 안
    public float seatUpMin = -0.15f, seatUpMax = 0.40f;

    [UdonSynced] private bool onLap;
    private bool shown;
    private bool parked;
    private bool lastShow;
    private float awayT;
    private int lastInteractive = -1;

    void Start() { Apply(false); }

    public override void Interact()
    {
        VRCPlayerApi me = Networking.LocalPlayer;
        if (!Utilities.IsValid(me)) return;
        if (!onLap)
        {
            if (!SeatedHere(me)) return;
            Networking.SetOwner(me, gameObject);
            onLap = true;
        }
        else
        {
            if (!Networking.IsOwner(gameObject)) return;
            onLap = false;
        }
        awayT = 0f;
        RequestSerialization();
    }

    public override void OnDeserialization() { awayT = 0f; }

    public override void PostLateUpdate()
    {
        VRCPlayerApi me = Networking.LocalPlayer;
        if (!Utilities.IsValid(me)) return;
        VRCPlayerApi o = Networking.GetOwner(gameObject);
        bool ownerOk = Utilities.IsValid(o);
        bool seated = onLap && ownerOk && SeatedHere(o);

        if (onLap && !seated) awayT += Time.deltaTime; else awayT = 0f;
        if (onLap && awayT > 0.6f && Networking.IsOwner(gameObject))
        {
            onLap = false; awayT = 0f;
            RequestSerialization();
        }

        bool show = onLap && ownerOk && (seated || awayT <= 0.6f);
        if (show != shown) Apply(show);
        bool meSeated = !show && NearSeat(me) && SeatedHere(me);
        if (show) { if (seated) PlaceOnLap(o); }
        else if (meSeated) { PlaceOnLap(me); parked = false; }      // 앉은 나: 판정을 내 무릎 위로 (내려다보고 누름)
        else if (!parked) Park();

        // 누르기 가능: 걸쳐 있으면 여기 앉은 나만, 덮여 있으면 덮은 사람(owner)만
        int inter = show ? (o.isLocal ? 1 : 0) : (meSeated ? 1 : 0);
        if (inter != lastInteractive || show != lastShow)
        {
            DisableInteractive = inter == 0;
            InteractionText = show ? "Put Back" : "Cover Lap";
            lastInteractive = inter; lastShow = show;
        }
    }

    private void Apply(bool show)
    {
        shown = show;
        if (lap != null) lap.enabled = show;
        if (drape != null) drape.enabled = !show;
        if (!show) Park();
    }

    // 쉬는 자세: 등받이 양털을 감싸는 박스 (누르기 꺼진 상태로 둠)
    private void Park()
    {
        transform.localPosition = drapePos;
        transform.localRotation = drapeRot;
        transform.localScale = drapeScale;
        parked = true;
    }

    private void PlaceOnLap(VRCPlayerApi p)
    {
        Vector3 lu = p.GetBonePosition(HumanBodyBones.LeftUpperLeg), ru = p.GetBonePosition(HumanBodyBones.RightUpperLeg);
        Vector3 lk = p.GetBonePosition(HumanBodyBones.LeftLowerLeg), rk = p.GetBonePosition(HumanBodyBones.RightLowerLeg);
        if (lu == Vector3.zero || lk == Vector3.zero || ru == Vector3.zero || rk == Vector3.zero)
        {
            transform.SetPositionAndRotation(sitPoint.TransformPoint(refPos), sitPoint.rotation * refRot);
            transform.localScale = Vector3.one;
            return;
        }
        Vector3 u = (lu + ru) * 0.5f, k = (lk + rk) * 0.5f;
        Vector3 d = k - u; float len = d.magnitude;
        if (len < 0.02f) return;
        Vector3 f = d / len;
        Vector3 r = Vector3.ProjectOnPlane(sitPoint.right, f).normalized;
        Vector3 up = Vector3.Cross(f, r);
        transform.SetPositionAndRotation(u, Quaternion.LookRotation(f, up));
        transform.localScale = Vector3.one * Mathf.Clamp(len / refThigh, 0.3f, 3f);
    }

    private bool NearSeat(VRCPlayerApi p) { return (p.GetPosition() - sitPoint.position).sqrMagnitude < 4f; }

    // 이 빈백에 앉아 있나: 골반이 SitPoint 근처 + 허벅지가 아래로 서 있지 않음 (선 자세 배제)
    private bool SeatedHere(VRCPlayerApi p)
    {
        Vector3 h = p.GetBonePosition(HumanBodyBones.Hips);
        Vector3 sp = sitPoint.position, su = sitPoint.up;
        if (h == Vector3.zero)
        {
            Vector3 q = p.GetPosition() - sp;
            return Vector3.ProjectOnPlane(q, su).magnitude < 0.25f;
        }
        Vector3 dd = h - sp;
        float upv = Vector3.Dot(dd, su);
        if (upv < seatUpMin || upv > seatUpMax) return false;
        if (Vector3.ProjectOnPlane(dd, su).magnitude > seatRadius) return false;
        Vector3 lu = p.GetBonePosition(HumanBodyBones.LeftUpperLeg), lk = p.GetBonePosition(HumanBodyBones.LeftLowerLeg);
        if (lu != Vector3.zero && lk != Vector3.zero)
        {
            Vector3 f = lk - lu;
            if (f.sqrMagnitude > 0.0001f && Vector3.Dot(f.normalized, su) < -0.6f) return false;
        }
        return true;
    }
}
