// 병맥주 — 캠프 아이스박스(BeerCooler)에서 꺼내 마신다. 상태(뚜껑 · 모금)는 자식 PyriteBeerState(Manual)
//  첫 사용(트리거) = 병뚜껑 따기(뽕 · 탄산 · 뚜껑이 날아감 · 거품). 그 뒤 사용 = 한 모금 (8모금이면 빈 병)
//  데스크톱: 머그처럼 똑바로 + 시선 yaw 로 들고(visual 피벗 = Grip), 사용하면 입 쪽으로 기울이는 연출
//  VR: 각도 고정 없음(Any + AutoHold No). 병 입구를 입가(mouthDist 안)로 가져가 sipTilt° 이상 기울이면 한 모금. 트리거는 따기·한 모금(연출 없음)
//  박스 안(제자리)에 있을 때는 뚜껑이 열려 있어야 집힌다(각자 판정). 박스 위에서 놓으면 제자리로, 밖에 60 초 두면(물에 빠지면 3 초) 제자리로 → 새 병
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteBeer : UdonSharpBehaviour
{
    public PyriteBeerState state;
    public Transform visual;              // 몸체 (피벗 = Grip, 병 가운데)
    public Transform home;                // 박스 안 제자리 (부모 = 박스 바닥 가운데, 박스 로컬 축)
    public PyriteTrunkLid lid;
    public GameObject glassFull;
    public GameObject glassEmpty;
    public GameObject cap;
    public Rigidbody capFly;              // 날아가는 병뚜껑 (월드에 따로, 꺼 둠)
    public ParticleSystem foam;
    public bool vrFreeGrip = true;
    public bool vrHoldToCarry = true;
    public float sipTilt = 50f;
    public float mouthDist = 0.14f;
    public float sipCooldown = 1.1f;
    public float idleReturn = 60f;
    public Vector3 boxHalf = new Vector3(0.22f, 0.6f, 0.33f);   // 박스 위 놓기 판정 (박스 로컬, y 는 바닥에서 위로)

    private VRCPickup pk;
    private VRCObjectSync sync;
    private Rigidbody rb;
    private bool vrLocal;
    private bool held;
    private float sipT = -1f;
    private float tilt;
    private float sipCool;
    private float idleT;
    private float flyT;
    private float pickT;

    private void Start()
    {
        pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        rb = (Rigidbody)GetComponent(typeof(Rigidbody));
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
        held = true; idleT = 0f; sipCool = 0f;
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        if (!state.opened) { state.Open(); sipCool = 0.8f; return; }
        if (state.sips <= 0 || sipT >= 0f) return;
        if (!(vrLocal && vrFreeGrip)) sipT = 0f;          // 기울이는 연출은 데스크톱만
        state.Sip();
        sipCool = sipCooldown;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (sipCool > 0f) sipCool -= dt;
        if (flyT > 0f) { flyT -= dt; if (flyT <= 0f && capFly != null) capFly.gameObject.SetActive(false); }

        // 박스 안에서는 뚜껑이 열려 있을 때만 집힌다 (각자 판정, 0.25 초마다)
        pickT -= dt;
        if (pickT <= 0f && pk != null && !held)
        {
            pickT = 0.25f;
            bool can = !AtHome() || lid == null || lid.IsOpen();
            if (pk.pickupable != can) pk.pickupable = can;
        }

        if (held && vrLocal && vrFreeGrip && state != null && state.opened && state.sips > 0 && sipCool <= 0f) VrSip();

        // 주인: 밖에 놓인 병은 60 초(물에 빠지면 3 초) 가만있으면 제자리로
        if (!held && home != null && rb != null && Networking.IsOwner(gameObject) && !AtHome())
        {
            bool still = rb.isKinematic || rb.velocity.sqrMagnitude < 0.01f;
            bool wet = transform.position.y < 0.3f;
            if (still || wet) idleT += dt; else idleT = 0f;
            if (idleT > (wet ? 3f : idleReturn)) ReturnHome();
        }

        tilt = 0f;
        if (sipT >= 0f)
        {
            sipT += dt;
            float a = sipT < 0.35f ? sipT / 0.35f : Mathf.Max(0f, 1f - (sipT - 0.8f) / 0.35f);
            tilt = -a * 70f;
            if (sipT > 1.15f) { sipT = -1f; tilt = 0f; }
        }
    }

    private void VrSip()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 mouth = h.position + h.rotation * new Vector3(0f, -0.07f, 0.07f);
        Vector3 lip = transform.TransformPoint(0f, 0.225f, 0f);           // 병 입구 (루트 = 병 바닥)
        if ((lip - mouth).sqrMagnitude > mouthDist * mouthDist) return;
        if (Vector3.Angle(transform.up, Vector3.up) < sipTilt) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(lp, state.gameObject);
        state.Sip();
        sipCool = sipCooldown;
    }

    public override void PostLateUpdate()
    {
        if (visual == null) return;
        VRCPlayerApi p = (pk != null && pk.IsHeld) ? pk.currentPlayer : null;
        if (Utilities.IsValid(p) && !(vrFreeGrip && p.IsUserInVR()))
        {
            Vector3 f = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
            visual.rotation = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f) * Quaternion.Euler(tilt, 0f, 0f);
        }
        else visual.localRotation = Quaternion.Euler(tilt, 0f, 0f);
    }

    public override void OnDrop()
    {
        held = false; sipT = -1f; tilt = 0f; idleT = 0f;
        if (!Networking.IsOwner(gameObject)) return;
        if (visual != null && OverBox(visual.position)) { ReturnHome(); return; }
        Transform t = visual != null ? visual : transform;
        Quaternion up = Quaternion.Euler(0f, YawOf(t), 0f);
        Vector3 o = visual != null ? visual.position - up * visual.localPosition : transform.position;
        transform.SetPositionAndRotation(o, up);
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }

    // 제자리로 = 새 병 (뚜껑 · 8모금)
    public void ReturnHome()
    {
        if (home == null) return;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        idleT = 0f;
        transform.SetPositionAndRotation(home.position, home.rotation);
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }   // 키네마틱에 속도를 넣으면 경고
        if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
        if (state != null)
        {
            if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
            state.ResetFresh();
        }
    }

    public bool IsFree() { return !held && (pk == null || !pk.IsHeld); }

    // 설정 2쪽 '맥주 모두 채우기' — 들고 있는 병은 건너뛰고, 이미 제자리의 새 병은 그대로
    public void Refill()
    {
        if (!IsFree()) return;
        if (AtHome() && state != null && !state.opened && state.sips == state.maxSips) return;
        ReturnHome();
    }

    private bool AtHome()
    {
        return home != null && (transform.position - home.position).sqrMagnitude < 0.0004f;
    }

    private bool OverBox(Vector3 p)
    {
        if (home == null || home.parent == null) return false;
        if (lid != null && !lid.IsOpen()) return false;
        Vector3 l = home.parent.InverseTransformPoint(p);
        return Mathf.Abs(l.x) < boxHalf.x && Mathf.Abs(l.z) < boxHalf.z && l.y > -0.05f && l.y < boxHalf.y;
    }

    private float YawOf(Transform t)
    {
        Vector3 f = t.forward; f.y = 0f;
        if (f.sqrMagnitude < 0.04f) { f = -t.up; f.y = 0f; }
        if (f.sqrMagnitude < 0.0001f) return t.eulerAngles.y;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    // State 가 부른다 (모두에게)
    public void ShowState(bool opened, bool hasBeer)
    {
        if (cap != null && cap.activeSelf == opened) cap.SetActive(!opened);
        if (glassFull != null && glassFull.activeSelf != hasBeer) glassFull.SetActive(hasBeer);
        if (glassEmpty != null && glassEmpty.activeSelf == hasBeer) glassEmpty.SetActive(!hasBeer);
    }

    public void PopFx()
    {
        if (capFly != null && cap != null)
        {
            // 켜기 전에 자리부터 (켠 뒤 옮기면 보간 리지드바디가 옛 자리를 유지 — Z53e 실측)
            capFly.transform.SetPositionAndRotation(cap.transform.position, cap.transform.rotation);
            capFly.gameObject.SetActive(true);
            capFly.position = cap.transform.position;
            Vector3 up = cap.transform.up;
            capFly.velocity = up * 2.4f + new Vector3(Random.Range(-0.6f, 0.6f), 0.4f, Random.Range(-0.6f, 0.6f));
            capFly.angularVelocity = new Vector3(Random.Range(-25f, 25f), Random.Range(-25f, 25f), Random.Range(-25f, 25f));
            flyT = 6f;
        }
        if (foam != null) { foam.Stop(); foam.Play(); }
    }
}
