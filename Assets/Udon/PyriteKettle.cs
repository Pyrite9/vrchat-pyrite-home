// 주전자 — 스토브 위에 있으면 잠시 뒤 김이 난다(모두 같은 위치를 보므로 각자 계산).
//  데스크톱: 든 채 좌클릭(사용) — 스토브 근처면 스토브에 올려놓고, 머그 위면 따른다(머그 채움은 머그 State 가 동기화)
//            들고 있을 때 손 방향 대신 똑바로 세우고 주둥이를 드는 사람 시선 쪽으로 (visual 피벗 = 손잡이) — 랜턴과 같은 방식
//  VR (2026-09-30): 각도 고정 없음. 잡은 자세 그대로(Any + AutoHold No). 주둥이가 아래를 향하게 기울이면 물줄기가 나오고,
//            그 아래 머그가 pourFillTime 초 동안 있으면 채운다. 사용(트리거)은 스토브 근처일 때 올려놓기만
//  놓기: 스토브 근처면 스토브에, 아니면 똑바로 세워 떨어뜨린다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteKettle : UdonSharpBehaviour
{
    public Transform stoveSlot;
    public float stoveRadius = 0.45f;       // 수평
    public float stoveHeight = 1.2f;        // 위쪽 허용
    public Transform spout;                 // 주둥이 끝
    public Transform[] mugRoots;
    public PyriteMugState[] mugStates;
    public float pourRadius = 0.35f;        // 주둥이 ↔ 머그 수평 거리
    public ParticleSystem steam;
    public float heatUp = 4f;               // 올려놓고 김이 나기까지 (초)
    public Transform visual;                // 몸체 (피벗 = 손잡이, +Z = 주둥이)
    public Transform stream;                // 따르는 물줄기 (피벗 = 위 끝, -Y 로 뻗음)
    public bool vrFreeGrip = true;          // VR: 잡은 자세 그대로 (false 면 옛 방식)
    public bool vrHoldToCarry = true;       // VR: 쥐는 동안만 든다 (AutoHold No)
    public float pourFillTime = 0.5f;       // VR: 머그 위에서 이만큼 따르면 가득
    public float vrPourRadius = 0.12f;      // VR: 물줄기 끝 ↔ 머그 수평 거리 (손으로 겨누니 데스크톱보다 좁게)

    private float heat;
    private float pourT = -1f;
    private float tilt;
    private bool placeNext;
    private VRCPickup pk;
    private bool vrLocal;
    private bool vrPouring;
    private float pourAcc;
    private int pourMug = -1;
    private Vector3 spoutBase = new Vector3(0f, 0.05f, 0.072f);   // 메시 공간 주둥이 뿌리 (Z31b) — U# 는 static 필드 없음

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

    private void Update()
    {
        bool on = stoveSlot != null && (transform.position - stoveSlot.position).sqrMagnitude < 0.0025f;
        heat = on ? Mathf.Min(heat + Time.deltaTime, heatUp + 1f) : Mathf.Max(heat - Time.deltaTime * 0.05f, 0f);
        bool steamOn = on && heat >= heatUp;
        if (steam != null)
        {
            if (steamOn && !steam.isPlaying) steam.Play();
            else if (!steamOn && steam.isPlaying) steam.Stop();
        }
        tilt = 0f;
        if (pourT >= 0f)
        {
            pourT += Time.deltaTime;
            float a = pourT < 0.3f ? pourT / 0.3f : (pourT > 1.4f ? Mathf.Max(0f, 1f - (pourT - 1.4f) / 0.3f) : 1f);
            tilt = a * 50f;
            if (pourT > 1.7f) { pourT = -1f; tilt = 0f; }
        }
        // VR 따르기: 든 사람만 판정하고, 채움은 머그 State 가 동기화
        if (vrLocal && vrFreeGrip && pk != null && pk.IsHeld && Networking.IsOwner(gameObject) && vrPouring)
        {
            int m = NearestMug(vrPourRadius, spout != null ? spout.position : transform.position);
            if (m >= 0 && m == pourMug) pourAcc += Time.deltaTime; else { pourMug = m; pourAcc = 0f; }
            if (m >= 0 && pourAcc >= pourFillTime && mugStates[m] != null && mugStates[m].fill < 0.99f)
            {
                Networking.SetOwner(Networking.LocalPlayer, mugStates[m].gameObject);
                mugStates[m].Fill();
                pourAcc = 0f;
            }
        }
        else { pourAcc = 0f; pourMug = -1; }
    }

    public override void PostLateUpdate()
    {
        bool vrHeld = false;
        if (visual != null)
        {
            if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
            VRCPlayerApi p = (pk != null && pk.IsHeld) ? pk.currentPlayer : null;
            vrHeld = Utilities.IsValid(p) && vrFreeGrip && p.IsUserInVR();
            if (Utilities.IsValid(p) && !vrHeld)
            {
                Vector3 f = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
                visual.rotation = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            }
            else visual.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        }
        // 물줄기: 데스크톱 = 연출 기울기, VR = 주둥이가 실제로 아래를 향할 때 (모든 사람이 동기화된 자세로 각자 계산)
        vrPouring = vrHeld && SpoutDown();
        if (stream != null)
        {
            bool s = vrHeld ? vrPouring : tilt > 42f;
            if (stream.gameObject.activeSelf != s) stream.gameObject.SetActive(s);
            if (s && spout != null) stream.SetPositionAndRotation(spout.position, Quaternion.identity);
        }
    }

    private bool SpoutDown()
    {
        if (spout == null || spout.parent == null) return false;
        Vector3 d = spout.position - spout.parent.TransformPoint(spoutBase);
        return d.y < -0.01f;          // 주둥이 끝이 뿌리보다 1 cm 이상 낮다 = 흐른다 (똑바로면 +8.4 cm)
    }

    public override void OnPickup() { placeNext = false; pourAcc = 0f; pourMug = -1; }

    public override void OnPickupUseDown()
    {
        if (NearStove())
        {
            placeNext = true;
            if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
            if (pk != null) pk.Drop();
            return;
        }
        if (vrLocal && vrFreeGrip) return;        // VR 은 손으로 기울여 따른다
        if (pourT >= 0f) return;
        pourT = 0f;
        int m = NearestMug(pourRadius, spout != null ? spout.position : transform.position);
        if (m >= 0 && mugStates[m] != null)
        {
            Networking.SetOwner(Networking.LocalPlayer, mugStates[m].gameObject);
            mugStates[m].Fill();
        }
    }

    public override void OnDrop()
    {
        pourT = -1f; tilt = 0f; vrPouring = false;
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        bool toStove = placeNext || NearStove();
        placeNext = false;
        if (toStove && stoveSlot != null)
        {
            transform.SetPositionAndRotation(stoveSlot.position, stoveSlot.rotation);
            if (visual != null) visual.localRotation = Quaternion.identity;
            if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
            return;
        }
        // 보이던 방향의 yaw 로 똑바로 세우되 손잡이 자리는 그대로
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
        if (f.sqrMagnitude < 0.04f) { f = t.up; f.y = 0f; }             // 주둥이를 거의 수직으로 들었을 때
        if (f.sqrMagnitude < 0.0001f) return t.eulerAngles.y;
        return Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
    }

    private bool NearStove()
    {
        if (stoveSlot == null) return false;
        Vector3 d = Origin() - stoveSlot.position;
        float dy = d.y; d.y = 0f;
        return d.magnitude < stoveRadius && dy > -0.2f && dy < stoveHeight;
    }

    // 보이는 몸체 기준 원점 (데스크톱은 들고 있을 때 루트가 손 방향으로 돌아 있어 루트 위치와 다르다)
    private Vector3 Origin()
    {
        return visual != null ? visual.TransformPoint(-visual.localPosition) : transform.position;
    }

    private int NearestMug(float radius, Vector3 from)
    {
        if (mugRoots == null) return -1;
        int best = -1;
        float bestD = radius;
        for (int i = 0; i < mugRoots.Length; i++)
        {
            if (mugRoots[i] == null) continue;
            Vector3 d = mugRoots[i].position - from;
            float dy = d.y; d.y = 0f;
            float r = d.magnitude;
            if (r < bestD && dy < 0.1f && dy > -1.5f) { best = i; bestD = r; }
        }
        return best;
    }
}
