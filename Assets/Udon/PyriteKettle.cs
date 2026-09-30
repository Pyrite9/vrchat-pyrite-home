// 주전자 — 스토브 위에 있으면 잠시 뒤 김이 난다(모두 같은 위치를 보므로 각자 계산).
//  2026-09-30 23:19: PC/VR 구분 제거. 픽업은 에디터에서 Any(잡은 자세 그대로)로 고정(Z54a), 스크립트는 몸체 회전을 건드리지 않는다
//  따르기 두 가지 (누구나):
//   - 기울이기: 주둥이가 아래를 향하면(앞으로 약 56°) 물줄기, 주둥이 아래 머그가 pourFillTime 초 있으면 가득
//   - 사용(트리거·좌클릭): 주둥이 아래(수평 pourRadius) 머그가 있으면 바로 가득 + 물줄기 1.2 초. 스토브 근처면 올려놓기
//  놓기: 스토브 근처면 스토브에, 아니면 손잡이 자리 그대로 똑바로 세워 떨어뜨린다
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
    public float pourRadius = 0.35f;        // 사용으로 따를 때 주둥이 ↔ 머그 수평 거리
    public ParticleSystem steam;
    public float heatUp = 4f;               // 올려놓고 김이 나기까지 (초)
    public Transform visual;                // 몸체 (피벗 = 손잡이, +Z = 주둥이). 회전은 항상 identity
    public Transform stream;                // 따르는 물줄기 (피벗 = 위 끝, -Y 로 뻗음)
    public float pourFillTime = 0.5f;       // 기울여 따를 때 머그 위에서 이만큼 따르면 가득
    public float tiltPourRadius = 0.12f;    // 기울여 따를 때 주둥이 ↔ 머그 수평 거리 (겨누니 좁게)

    private float heat;
    private float streamT;                  // 사용으로 따를 때 물줄기 남은 시간
    private bool placeNext;
    private VRCPickup pk;
    private bool tiltPouring;
    private float pourAcc;
    private int pourMug = -1;
    private Vector3 spoutBase = new Vector3(0f, 0.05f, 0.072f);   // 메시 공간 주둥이 뿌리 (Z31b) — U# 는 static 필드 없음

    private void Start()
    {
        pk = (VRCPickup)GetComponent(typeof(VRCPickup));
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
        if (streamT > 0f) streamT -= Time.deltaTime;

        // 기울여 따르기: 든 사람만 판정하고, 채움은 머그 State 가 동기화
        if (pk != null && pk.IsHeld && Networking.IsOwner(gameObject) && tiltPouring)
        {
            int m = NearestMug(tiltPourRadius, spout != null ? spout.position : transform.position);
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
        if (visual != null && visual.localRotation != Quaternion.identity) visual.localRotation = Quaternion.identity;
        bool held = pk != null && pk.IsHeld;
        tiltPouring = held && SpoutDown();          // 모든 사람이 동기화된 자세로 각자 계산
        if (stream != null)
        {
            bool s = tiltPouring || streamT > 0f;
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
        int m = NearestMug(pourRadius, spout != null ? spout.position : transform.position);
        if (m >= 0 && mugStates[m] != null)
        {
            streamT = 1.2f;
            Networking.SetOwner(Networking.LocalPlayer, mugStates[m].gameObject);
            mugStates[m].Fill();
        }
    }

    public override void OnDrop()
    {
        tiltPouring = false; streamT = 0f;
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

    // 몸체 바닥 기준 원점 (visual 회전이 identity 라 루트 위치와 같다)
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
