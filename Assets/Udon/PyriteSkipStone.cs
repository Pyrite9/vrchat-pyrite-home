// 물수제비 돌 — 부두 끝 쟁반에서 집어 던진다
//  데스크톱 (2026-09-24): 클릭 = 집기(AutoHold), 우클릭 = 놓기(VRChat 기본), 좌클릭 꾹 → 떼면 던지기. 오래 누를수록 세게, 시선 방향으로 낮게
//  VR 은 AutoHold No — 쥐고 있는 동안만 든다 (2026-09-27)
//  손 바꿔 잡기(OnDrop → OnPickup)는 던지기로 치지 않는다 (2026-09-30 23:3x, 0.08 초 기다렸다 발사)
//  VR 던지기 (2026-09-30, 손 스윙은 입사각·속도 맞추기가 어려웠다): 쥐고 있는 시간만큼 힘이 모이고(vrMinHold 뒤 vrChargeTime 동안 throwMin→throwMax,
//   손 진동이 점점 세짐), 손을 펴면 손이 가리키는 수평 방향으로 낮게 날아간다(피치 -4~10°, 데스크톱과 같음). 손 속도는 무시
//   vrMinHold 보다 짧게 쥐었다 놓으면 그냥 떨어뜨린다. 손 '가리키는 축'은 handAxis (트래킹 회전 기준)
//  수면(WaterWalk 콜라이더)에 닿을 때: 수평 속도 minSpeed 이상 + 입사각 maxAngle 이하면 튀고(속도 82%), 아니면 가라앉는다(콜라이더 끔 → 2.5초 뒤 쟁반으로)
//  물보라·물결 고리·호수 파문. 던진 사람(주인)은 충돌로, 다른 사람은 수면 통과로 물보라를 낸다
//  멀리 놓인 채 30초 가만있으면 쟁반으로 돌아온다
//  보는 사람 쪽 궤적 (2026-10-05): 물리는 주인만 계산하고 다른 사람은 VRCObjectSync 위치를 이어 그린 것만 봐서, 튀는 순간(한 프레임)이 묻혀 돌이 수면 위에서 꺾여 보였다(물보라도 마지막에만)
//   → 주인이 던질 때(NetThrow)·튈 때마다(NetSkip)·가라앉을 때(NetSink)·다른 데 부딪힐 때(NetEnd) 위치·속도를 보낸다. 보는 쪽은 그 사이 포물선만 계산해 자식 Mesh 를 그 자리에 그린다(루트는 동기화 그대로)
//   매 튐마다 주인 값으로 다시 맞추므로 오차가 쌓이지 않는다. 이벤트가 2 초 안 오면 동기화 위치로 돌아간다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.SDK3.UdonNetworkCalling;
using VRC.Udon.Common.Interfaces;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteSkipStone : UdonSharpBehaviour
{
    public Transform home;
    public Collider waterCollider;
    public ParticleSystem splash;
    public ParticleSystem ring;
    public PyriteLakeRipple ripple;
    public float surfaceY = 0.05f;
    public float minSpeed = 2.5f;
    public float maxAngle = 35f;
    public int maxSkips = 12;
    public AudioSource splashAudio;       // 효과음 (2026-09-24) — 물보라 파티클과 같은 오브젝트, 모든 돌이 같이 쓴다
    public AudioClip splashClip;
    public float throwMin = 7f;           // 짧게 누름 (m/s) — 대개 가라앉는다
    public float throwMax = 17f;          // chargeTime 이상 누름
    public float chargeTime = 0.9f;
    public float releaseHeight = 0.8f;    // 발 위 — 사이드암 높이. 낮을수록 입사각이 얕아 잘 튄다
    public float vrMinHold = 0.35f;       // VR: 이보다 짧게 쥐면 던지지 않고 놓기만
    public float vrChargeTime = 1.2f;     // VR: vrMinHold 뒤 이만큼 쥐면 최대 세기
    public Vector3 handAxis = new Vector3(0f, 0f, 1f);   // VR: 손 트래킹 회전에서 가리키는 방향 축 (인게임에서 틀리면 여기만)

    private Rigidbody rb;
    private Collider col;
    private VRCObjectSync sync;
    private VRCPickup pickup;
    private bool charging;
    private float chargeStart;
    private Vector3 throwVel;
    private int throwFrames;
    private int skips;
    private bool sinking, held;
    private float sinkT, idleT, prevY;
    private bool vrLocal, vrHeld;
    private float holdStart, hapT;
    private VRC_Pickup.PickupHand hand;
    private float throwPend = -1f;        // 손 바꾸기 판별: 놓은 뒤 이만큼 기다렸다 던진다 (다시 잡히면 취소)
    private Vector3 throwPos;
    // 보는 쪽 궤적
    public bool dbgRemote;                // 에디터 시험: 주인도 보는 쪽 계산을 같이 돌린다 (Mesh 는 계산 위치, 루트는 물리)
    public Vector3 dbgPos;
    public Vector3 dbgVel;
    public float dbgMaxGap;               // 시험 결과: 계산 위치와 물리 위치의 최대 거리
    public float dbgMinY;                 // 시험 결과: 계산 위치의 가장 낮은 높이
    public int dbgSkips;                  // 시험 결과: 받은 튐 수
    public bool dbgSimOn;
    private Transform vis;
    private Vector3 visPos0;
    private Quaternion visRot0;
    private bool rsim;
    private Vector3 rp;
    private Vector3 rv;
    private float rIdle;
    private float rFloor;
    private bool flying;                  // 주인: 던진 뒤 아직 물 말고 다른 데 안 부딪힘

    private void Start()
    {
        rb = (Rigidbody)GetComponent(typeof(Rigidbody));
        col = (Collider)GetComponent(typeof(Collider));
        sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        pickup = (VRCPickup)GetComponent(typeof(VRCPickup));
        prevY = transform.position.y;
        vis = transform.Find("Mesh");
        if (vis != null) { visPos0 = vis.localPosition; visRot0 = vis.localRotation; }
        VRCPlayerApi lp = Networking.LocalPlayer;
        vrLocal = Utilities.IsValid(lp) && lp.IsUserInVR();
        if (pickup != null && vrLocal) pickup.AutoHold = VRC_Pickup.AutoHoldMode.No;
    }

    public override void OnPickup()
    {
        EndSim(); flying = false;
        held = true; skips = 0; sinking = false; if (col != null) col.enabled = true; if (rb != null) rb.drag = 0.05f;
        vrHeld = vrLocal && pickup != null;
        if (vrHeld) { hand = pickup.currentHand; holdStart = Time.time; hapT = 0f; }
    }

    public override void OnDrop()
    {
        held = false; skips = 0; idleT = 0f; charging = false;
        if (!vrHeld) return;
        vrHeld = false;
        float t = Time.time - holdStart;
        if (t < vrMinHold || rb == null) return;                        // 짧게 쥐었다 놓음 = 그냥 놓기
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        float k = Mathf.Clamp01((t - vrMinHold) / Mathf.Max(vrChargeTime, 0.01f));
        float sp = Mathf.Lerp(throwMin, throwMax, k);
        VRCPlayerApi.TrackingDataType td = hand == VRC_Pickup.PickupHand.Left ? VRCPlayerApi.TrackingDataType.LeftHand : VRCPlayerApi.TrackingDataType.RightHand;
        Vector3 f = lp.GetTrackingData(td).rotation * handAxis;
        Vector3 flat = new Vector3(f.x, 0f, f.z);
        if (flat.sqrMagnitude < 0.01f) { Vector3 hf = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward; flat = new Vector3(hf.x, 0f, hf.z); }
        if (flat.sqrMagnitude < 0.0001f) flat = lp.GetRotation() * Vector3.forward;
        flat.Normalize();
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -4f, 10f) * Mathf.Deg2Rad;
        throwVel = flat * (sp * Mathf.Cos(pitch)) + Vector3.up * (sp * Mathf.Sin(pitch));   // 세기·방향은 놓는 순간 값
        // 🔴 반대손으로 고쳐 잡아도 VRChat 은 OnDrop → OnPickup 을 보낸다(2026-09-30 23:30) → 바로 던지면 손 바꾸기가 던지기가 된다
        //  0.08 초 제자리에 붙잡아 두고, 그 사이 다시 잡히면 취소. 아니면 놓은 자리에서 발사
        throwPend = 0.08f;
        throwPos = transform.position;
    }

    private void ThrowTick()
    {
        if (throwPend < 0f || rb == null) return;
        if (pickup != null && pickup.IsHeld) { throwPend = -1f; return; }    // 손 바꾸기 → 던지지 않음
        throwPend -= Time.deltaTime;
        transform.position = throwPos;
        if (!rb.isKinematic) rb.velocity = Vector3.zero;
        if (throwPend > 0f) return;
        throwPend = -1f;
        Launch();                    // 손에서 놓은 자리 그대로 발사
        SendThrow();
        throwFrames = 2;             // VRChat 이 놓은 직후 손 속도를 다시 넣을 수 있어 두 프레임 더 덮어쓴다
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (Utilities.IsValid(lp)) lp.PlayHapticEventInHand(hand, 0.08f, 0.6f, 180f);
    }

    // VR: 쥐고 있는 동안 힘이 모이는 걸 진동으로 — 점점 세고 빠르게, 가득 차면 0.6 초마다 한 번
    private void ChargeHaptics()
    {
        float t = Time.time - holdStart;
        if (t < vrMinHold) return;
        hapT -= Time.deltaTime;
        if (hapT > 0f) return;
        float k = Mathf.Clamp01((t - vrMinHold) / Mathf.Max(vrChargeTime, 0.01f));
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return;
        if (k >= 1f) { lp.PlayHapticEventInHand(hand, 0.03f, 0.35f, 240f); hapT = 0.6f; }
        else { lp.PlayHapticEventInHand(hand, 0.03f, 0.08f + 0.5f * k, 120f + 120f * k); hapT = Mathf.Lerp(0.22f, 0.09f, k); }
    }

    public override void OnPickupUseDown()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp) || lp.IsUserInVR()) return;
        charging = true; chargeStart = Time.time;
    }

    public override void OnPickupUseUp()
    {
        if (!charging) return;
        charging = false;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp) || pickup == null || rb == null) return;
        float k = Mathf.Clamp01((Time.time - chargeStart) / chargeTime);
        float sp = Mathf.Lerp(throwMin, throwMax, k);
        Vector3 f = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
        Vector3 flat = new Vector3(f.x, 0f, f.z);
        if (flat.sqrMagnitude < 0.0001f) flat = lp.GetRotation() * Vector3.forward;
        flat.Normalize();
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg, -4f, 10f) * Mathf.Deg2Rad;   // 위를 봐도 10° 까지만
        throwVel = flat * (sp * Mathf.Cos(pitch)) + Vector3.up * (sp * Mathf.Sin(pitch));
        Vector3 p = lp.GetPosition() + flat * 0.45f + Vector3.Cross(Vector3.up, flat) * 0.25f + Vector3.up * releaseHeight;
        pickup.Drop();
        transform.position = p;
        Launch();
        SendThrow();
        if (sync != null) sync.FlagDiscontinuity();
        throwFrames = 2;   // Drop 직후 VRChat 이 손 속도를 다시 넣을 수 있어 두 프레임 더 덮어쓴다
    }

    private void Launch()
    {
        rb.isKinematic = false; rb.useGravity = true;
        rb.velocity = throwVel;
        rb.angularVelocity = new Vector3(0f, 30f, 0f);
    }

    private void OnCollisionEnter(Collision c)
    {
        if (!Networking.IsOwner(gameObject) || sinking || held || c == null) return;
        if (c.collider != waterCollider)
        {
            if (flying) { flying = false; SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(NetEnd)); if (dbgRemote) EndSim(); }
            return;
        }
        Vector3 v = -c.relativeVelocity;
        float h = Mathf.Sqrt(v.x * v.x + v.z * v.z);
        float vy = Mathf.Abs(v.y);
        float ang = Mathf.Atan2(vy, Mathf.Max(h, 0.001f)) * Mathf.Rad2Deg;
        Vector3 p = transform.position;
        if (h > minSpeed && ang < maxAngle && skips < maxSkips)
        {
            skips++;
            rb.velocity = new Vector3(v.x * 0.82f, Mathf.Max(vy * 0.45f, 0.8f + h * 0.05f), v.z * 0.82f);
            rb.angularVelocity = new Vector3(0f, 25f, 0f);
            Splash(p, 0.6f);
            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(NetSkip), p, rb.velocity);
            if (dbgRemote) { dbgSkips++; StartSim(p, rb.velocity, p.y); }
        }
        else
        {
            Splash(p, 1f);
            sinking = true; sinkT = 0f;
            if (col != null) col.enabled = false;
            if (rb != null) rb.drag = 4f;
            flying = false;
            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(NetSink), p);
            if (dbgRemote) EndSim();
        }
    }

    private void Update()
    {
        ThrowTick();
        if (throwFrames > 0 && rb != null) { throwFrames--; Launch(); }
        if (vrHeld && held) ChargeHaptics();
        float y = transform.position.y;
        if (Networking.IsOwner(gameObject) && rb != null)
        {
            if (sinking)
            {
                sinkT += Time.deltaTime;
                if (sinkT > 2.5f) Respawn();
            }
            else if (!held && home != null && (transform.position - home.position).sqrMagnitude > 0.09f && rb.velocity.sqrMagnitude < 0.01f)
            {
                idleT += Time.deltaTime;
                if (idleT > 30f) Respawn();
            }
            else idleT = 0f;
        }
        prevY = y;
    }

    private bool OverWater(Vector3 p)
    {
        if (waterCollider == null) return false;
        Bounds b = waterCollider.bounds;
        return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;
    }

    private void Respawn()
    {
        sinking = false; skips = 0; idleT = 0f; flying = false;
        if (col != null) col.enabled = true;
        if (rb != null) { rb.drag = 0.05f; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (home != null) transform.SetPositionAndRotation(home.position, home.rotation);
        if (sync != null) sync.FlagDiscontinuity();
    }

    // ── 보는 쪽 궤적 ──
    private void SendThrow()
    {
        flying = true;
        SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(NetThrow), transform.position, throwVel);
        if (dbgRemote) { dbgSkips = 0; dbgMaxGap = 0f; dbgMinY = 99f; StartSim(transform.position, throwVel, WaterTop()); }
    }

    private float WaterTop()
    {
        if (waterCollider == null) return surfaceY;
        return waterCollider.bounds.max.y + 0.01f;       // 돌 상자 반 높이
    }

    [NetworkCallable]
    public void NetThrow(Vector3 p, Vector3 v)
    {
        if (Networking.IsOwner(gameObject)) return;
        StartSim(p, v, WaterTop());
    }

    [NetworkCallable]
    public void NetSkip(Vector3 p, Vector3 v)
    {
        if (Networking.IsOwner(gameObject)) return;
        Splash(p, 0.6f);
        StartSim(p, v, p.y);
    }

    [NetworkCallable]
    public void NetSink(Vector3 p)
    {
        if (Networking.IsOwner(gameObject)) return;
        Splash(p, 1f);
        EndSim();
    }

    [NetworkCallable]
    public void NetEnd()
    {
        if (Networking.IsOwner(gameObject)) return;
        EndSim();
    }

    private void StartSim(Vector3 p, Vector3 v, float floorY)
    {
        if (vis == null) return;
        rsim = true; rp = p; rv = v; rIdle = 0f; rFloor = floorY; dbgSimOn = true;
    }

    private void EndSim()
    {
        if (!rsim) return;
        rsim = false; dbgSimOn = false;
        if (vis != null) { vis.localPosition = visPos0; vis.localRotation = visRot0; }
    }

    // 동기화가 루트를 옮긴 뒤에 Mesh 만 계산 위치로 (포물선 + 공기 저항 0.05, 주인 리지드바디와 같은 값)
    public override void PostLateUpdate()
    {
        if (!rsim || vis == null) return;
        float dt = Time.deltaTime;
        rIdle += dt;
        if (rIdle > 2f) { EndSim(); return; }
        rv.y -= 9.81f * dt;
        rv *= 1f - 0.05f * dt;
        rp += rv * dt;
        if (rp.y < rFloor && OverWater(rp))
        {
            rp.y = rFloor;                               // 다음 튐 소식이 늦으면 수면에 붙어 미끄러지며 기다린다
            if (rv.y < 0f) rv.y = 0f;
        }
        vis.position = rp;
        vis.rotation = Quaternion.Euler(0f, Time.time * 1400f, 0f);
        if (dbgRemote)
        {
            float gap = Vector3.Distance(rp, transform.position);
            if (gap > dbgMaxGap) dbgMaxGap = gap;
            if (rp.y < dbgMinY) dbgMinY = rp.y;
        }
    }

    // 에디터 시험용 (ClientSim): dbgPos 에서 dbgVel 로 던진다
    public void DebugThrow()
    {
        if (rb == null) return;
        if (pickup != null && pickup.IsHeld) pickup.Drop();
        held = false; sinking = false; skips = 0;
        if (col != null) col.enabled = true;
        rb.drag = 0.05f;
        rb.isKinematic = false;
        rb.position = dbgPos;            // 보간 리지드바디는 transform 만 옮기면 옛 자리로 덮인다
        transform.position = dbgPos;
        throwVel = dbgVel;
        Launch();
        SendThrow();
        if (sync != null) sync.FlagDiscontinuity();
        throwFrames = 2;
    }

    private void Splash(Vector3 p, float s)
    {
        p.y = surfaceY;
        if (splash != null) { splash.transform.position = p; splash.Emit((int)(12f * s)); }
        if (ring != null) { ring.transform.position = p + Vector3.up * 0.01f; ring.Emit(1); }
        if (ripple != null) ripple.AddDrop(p.x, p.z, 0.012f * s);
        if (splashAudio != null && splashClip != null)
        {
            splashAudio.transform.position = p;
            splashAudio.pitch = Random.Range(0.9f, 1.12f) * (s >= 1f ? 0.8f : 1f);   // 가라앉을 땐 낮고 굵게
            splashAudio.PlayOneShot(splashClip, Mathf.Clamp01(0.45f + 0.5f * s));
        }
    }
}
