// 주전자 — 스토브 위에 있으면 잠시 뒤 김이 난다(모두 같은 위치를 보므로 각자 계산).
//  든 채 좌클릭(사용): 스토브 근처면 스토브에 올려놓고, 머그 위면 따른다(머그 채움은 머그 State 가 동기화)
//  우클릭(놓기): 스토브 근처면 스토브에, 아니면 똑바로 세워 떨어뜨린다
//  들고 있을 때 방향: 손 방향 대신 똑바로 세우고 주둥이를 드는 사람 시선 쪽으로 (visual 피벗 = 손잡이) — 랜턴과 같은 방식
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

    private float heat;
    private float pourT = -1f;
    private float tilt;
    private bool placeNext;
    private VRCPickup pk;

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
    }

    public override void PostLateUpdate()
    {
        if (visual != null)
        {
            if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
            VRCPlayerApi p = (pk != null && pk.IsHeld) ? pk.currentPlayer : null;
            if (Utilities.IsValid(p))
            {
                Vector3 f = p.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation * Vector3.forward;
                visual.rotation = Quaternion.Euler(0f, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg, 0f) * Quaternion.Euler(tilt, 0f, 0f);
            }
            else visual.localRotation = Quaternion.Euler(tilt, 0f, 0f);
        }
        if (stream != null)
        {
            bool s = tilt > 42f;
            if (stream.gameObject.activeSelf != s) stream.gameObject.SetActive(s);
            if (s && spout != null) stream.SetPositionAndRotation(spout.position, Quaternion.identity);
        }
    }

    public override void OnPickup() { placeNext = false; }

    public override void OnPickupUseDown()
    {
        if (NearStove())
        {
            placeNext = true;
            if (pk == null) pk = (VRCPickup)GetComponent(typeof(VRCPickup));
            if (pk != null) pk.Drop();
            return;
        }
        if (pourT >= 0f) return;
        pourT = 0f;
        int m = NearestMug();
        if (m >= 0 && mugStates[m] != null)
        {
            Networking.SetOwner(Networking.LocalPlayer, mugStates[m].gameObject);
            mugStates[m].Fill();
        }
    }

    public override void OnDrop()
    {
        pourT = -1f; tilt = 0f;
        if (!Networking.IsOwner(gameObject)) return;
        VRCObjectSync sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        bool toStove = placeNext || NearStove();
        placeNext = false;
        if (toStove && stoveSlot != null)
        {
            transform.SetPositionAndRotation(stoveSlot.position, stoveSlot.rotation);
            if (sync != null) { sync.SetGravity(false); sync.SetKinematic(true); sync.FlagDiscontinuity(); }
            return;
        }
        // 들고 있을 때 보이던 모양(똑바로, 시선 쪽) 그대로 내려놓는다
        float yaw = visual != null ? visual.eulerAngles.y : transform.eulerAngles.y;
        transform.SetPositionAndRotation(Origin(), Quaternion.Euler(0f, yaw, 0f));
        if (sync != null) { sync.SetKinematic(false); sync.SetGravity(true); sync.FlagDiscontinuity(); }
    }

    private bool NearStove()
    {
        if (stoveSlot == null) return false;
        Vector3 d = Origin() - stoveSlot.position;
        float dy = d.y; d.y = 0f;
        return d.magnitude < stoveRadius && dy > -0.2f && dy < stoveHeight;
    }

    // 보이는 몸체 기준 원점 (들고 있을 땐 루트가 손 방향으로 돌아 있어 루트 위치와 다르다)
    private Vector3 Origin()
    {
        return visual != null ? visual.TransformPoint(-visual.localPosition) : transform.position;
    }

    private int NearestMug()
    {
        if (mugRoots == null || spout == null) return -1;
        int best = -1;
        float bestD = pourRadius;
        for (int i = 0; i < mugRoots.Length; i++)
        {
            if (mugRoots[i] == null) continue;
            Vector3 d = mugRoots[i].position - spout.position;
            float dy = d.y; d.y = 0f;
            float r = d.magnitude;
            if (r < bestD && dy < 0.1f && dy > -1.5f) { best = i; bestD = r; }
        }
        return best;
    }
}
