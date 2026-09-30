// 병맥주 — 캠프 아이스박스(BeerCooler)에서 꺼내 마신다. 상태(뚜껑 · 모금)는 자식 PyriteBeerState(Manual)
//  첫 사용(트리거) = 병뚜껑 따기(뽕 · 탄산 · 뚜껑이 날아감 · 거품). 그 뒤 사용 = 한 모금 (8모금이면 빈 병)
//  2026-09-30 23:19: PC/VR 구분 제거. 픽업은 에디터에서 Any(잡은 자세 그대로)로 고정(Z54a), 스크립트는 회전을 건드리지 않는다
//   사용(트리거·좌클릭) = 따기 · 한 모금. 병 입구를 입가(mouthDist 안)로 가져가 sipTilt° 이상 기울여도 한 모금
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
    public Renderer liquid;               // 병 속 맥주 (Pyrite/BeerLiquid, _Level = 로컬 수면 높이)
    public float levelFull = 0.192f;
    public float levelLast = 0.022f;      // 한 모금 남았을 때
    public Rigidbody capFly;              // 날아가는 병뚜껑 (월드에 따로, 꺼 둠)
    public ParticleSystem foam;
    public float sipTilt = 50f;
    public float mouthDist = 0.14f;
    public float sipCooldown = 1.1f;
    public float idleReturn = 60f;
    public Vector3 boxHalf = new Vector3(0.22f, 0.6f, 0.33f);   // 박스 위 놓기 판정 (박스 로컬, y 는 바닥에서 위로)

    private VRCPickup pk;
    private float dropT = -1f;
    private VRCPickup dropPk;
    private VRCObjectSync sync;
    private Rigidbody rb;
    private bool held;
    private float sipCool;
    private float idleT;
    private float flyT;
    private float pickT;
    private Material liqMat;              // 가득일 땐 공유 재질(인스턴싱 유지), 한 모금이라도 줄면 이 병만 복제

    private void Start()
    {
        pk = (VRCPickup)GetComponent(typeof(VRCPickup));
        sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        rb = (Rigidbody)GetComponent(typeof(Rigidbody));
    }

    public override void OnPickup()
    {
        RotLock(false);
        held = true; idleT = 0f; sipCool = 0f;
        if (state != null && !Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
    }

    public override void OnPickupUseDown()
    {
        if (state == null) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(Networking.LocalPlayer, state.gameObject);
        if (!state.opened) { state.Open(); sipCool = 0.8f; return; }
        if (state.sips <= 0 || sipCool > 0f) return;
        state.Sip();
        sipCool = 0.35f;
    }

    private void Update()
    {
        DropTick();
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

        if (held && state != null && state.opened && state.sips > 0 && sipCool <= 0f && Networking.IsOwner(gameObject)) MouthSip();

        // 주인: 밖에 놓인 병은 60 초(물에 빠지면 3 초) 가만있으면 제자리로
        if (!held && home != null && rb != null && Networking.IsOwner(gameObject) && !AtHome())
        {
            bool still = rb.isKinematic || rb.velocity.sqrMagnitude < 0.01f;
            bool wet = transform.position.y < 0.3f;
            if (still || wet) idleT += dt; else idleT = 0f;
            if (idleT > (wet ? 3f : idleReturn)) ReturnHome();
        }
    }

    private void MouthSip()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        VRCPlayerApi.TrackingData h = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
        Vector3 mouth = h.position + h.rotation * new Vector3(0f, -0.07f, 0.07f);
        Vector3 lip = transform.TransformPoint(0f, 0.225f, 0f);           // 병 입구 (루트 = 병 바닥)
        if ((lip - mouth).sqrMagnitude > mouthDist * mouthDist) return;
        if (Vector3.Angle(transform.up, Vector3.up) < sipTilt) return;
        if (!Networking.IsOwner(state.gameObject)) Networking.SetOwner(lp, state.gameObject);
        state.Sip();
        sipCool = sipCooldown;
    }

    // 🔴 반대손으로 고쳐 잡으면 VRChat 은 OnDrop → OnPickup 을 보낸다(관리자 2026-09-30 23:30 재현). 놓기 처리를 바로 하면
    //  새 손이 잡은 물건을 세우기·물리 켜기로 덮어써 굳는다 → dropT 초 미루고, 그 사이 다시 잡혀 있으면 취소
    public override void OnDrop()
    {
        held = false; idleT = 0f;
        dropT = 0.12f;
    }

    private void DropTick()
    {
        if (dropT < 0f) return;
        if (dropPk == null) dropPk = (VRCPickup)GetComponent(typeof(VRCPickup));
        if (dropPk != null && dropPk.IsHeld) { dropT = -1f; return; }   // 손 바꾸기 → 취소
        dropT -= Time.deltaTime;
        if (dropT <= 0f) { dropT = -1f; DoDrop(); }
    }

    // 🔴 2026-10-01 01:32 관리자 인게임: 떨어뜨린 뒤 다시 들면 수직으로 굳어 각도가 안 변함
    //  원인: Rigidbody.constraints = FreezeRotation. 키네마틱일 땐 무관하지만 놓기 처리(SetKinematic(false)) 뒤 동적 몸체가 되면
    //  VRChat 이 들고 있는 동안에도 물리로 옮겨 회전이 잠긴다 → 잡으면 풀고, 놓기 처리에서 다시 잠근다(쓰러져 구르지 않게)
    private Rigidbody lockRb;
    private void RotLock(bool on)
    {
        if (lockRb == null) lockRb = (Rigidbody)GetComponent(typeof(Rigidbody));
        if (lockRb != null) lockRb.constraints = on ? RigidbodyConstraints.FreezeRotation : RigidbodyConstraints.None;
    }

    // 자세 옮기기: transform 과 리지드바디 자세를 같이 (보간 리지드바디는 transform 만 바꾸면 다음 보간이 옛 자세로 덮는다 — Z53e 01:44 실측)
    private void Teleport(Vector3 p, Quaternion r)
    {
        transform.SetPositionAndRotation(p, r);
        if (lockRb == null) lockRb = (Rigidbody)GetComponent(typeof(Rigidbody));
        if (lockRb != null) { lockRb.position = p; lockRb.rotation = r; }
    }

    private void DoDrop()
    {
        if (!Networking.IsOwner(gameObject)) return;
        if (visual != null && OverBox(visual.position)) { ReturnHome(); return; }
        Transform t = visual != null ? visual : transform;
        Quaternion up = Quaternion.Euler(0f, YawOf(t), 0f);
        Vector3 o = visual != null ? visual.position - up * visual.localPosition : transform.position;
        Teleport(o, up);
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
        RotLock(true);   // 자세·물리를 정한 뒤에 잠근다 (먼저 잠그면 세우기가 안 먹었다 — Z53e)
    }

    // 제자리로 = 새 병 (뚜껑 · 8모금)
    public void ReturnHome()
    {
        if (home == null) return;
        if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
        idleT = 0f;
        Teleport(home.position, home.rotation);
        if (visual != null) visual.localRotation = Quaternion.identity;
        if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }   // 키네마틱에 속도를 넣으면 경고
        if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
        RotLock(true);
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
    public void ShowState(bool opened, int sips, int maxSips)
    {
        bool hasBeer = sips > 0;
        if (cap != null && cap.activeSelf == opened) cap.SetActive(!opened);
        if (glassFull != null && glassFull.activeSelf != hasBeer) glassFull.SetActive(hasBeer);
        if (glassEmpty != null && glassEmpty.activeSelf == hasBeer) glassEmpty.SetActive(!hasBeer);
        if (liquid == null) return;
        if (liquid.gameObject.activeSelf != hasBeer) liquid.gameObject.SetActive(hasBeer);
        if (!hasBeer) return;
        if (sips >= maxSips && liqMat == null) return;          // 새 병 = 공유 재질 기본값(가득)
        if (liqMat == null) liqMat = liquid.material;
        float k = maxSips > 1 ? (float)(sips - 1) / (maxSips - 1) : 1f;
        liqMat.SetFloat("_Level", Mathf.Lerp(levelLast, levelFull, k));
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
