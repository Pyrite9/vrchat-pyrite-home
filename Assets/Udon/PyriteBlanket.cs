// PyriteBlanket — 이불 주머니를 누르면 모두에게 이불이 덮인다(다시 누르면 걷힘). 동기화 isOn (Manual)
//  덮인 동안 매 프레임: 매트 근처에 누운 플레이어 뼈대(머리·가슴·골반·무릎·발) → 선분 6개씩 → Pyrite/Blanket 머티리얼 _Seg
//  각 클라이언트가 자기 화면에서 계산 (뼈 위치는 VRChat 이 이미 동기화). 뼈 없는 아바타는 플레이어 위치·방향으로 선분 1개
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteBlanket : UdonSharpBehaviour
{
    [UdonSynced] public bool isOn;
    public Renderer blanket;
    public Material mat;
    public GameObject folded;
    public Transform area;
    public Vector3 half = new Vector3(1.125f, 0.3f, 1.1f);
    public float dropTime = 1.1f;

    private float anim;
    private VRCPlayerApi[] players;
    private Vector4[] segs;
    private int k;

    void Start()
    {
        players = new VRCPlayerApi[32];
        segs = new Vector4[96];
        anim = isOn ? 1f : 0f;
        Apply();
    }

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        isOn = !isOn;
        RequestSerialization();
    }

    void Update()
    {
        float target = isOn ? 1f : 0f;
        if (anim != target) anim = Mathf.MoveTowards(anim, target, Time.deltaTime / dropTime);
        Apply();
    }

    private void Apply()
    {
        bool vis = anim > 0.001f;
        if (blanket.enabled != vis) blanket.enabled = vis;
        bool fold = anim < 0.02f;
        if (folded != null && folded.activeSelf != fold) folded.SetActive(fold);
        if (!vis) return;
        float inv = 1f - anim;
        float e = 1f - inv * inv * inv;
        mat.SetFloat("_Drop", e);
        Gather();
    }

    private void Gather()
    {
        k = 0;
        int n = VRCPlayerApi.GetPlayerCount();
        if (n > 32) n = 32;
        VRCPlayerApi.GetPlayers(players);
        for (int i = 0; i < n; i++)
        {
            VRCPlayerApi p = players[i];
            if (!Utilities.IsValid(p)) continue;
            if (k > 42) break;
            Vector3 hips = p.GetBonePosition(HumanBodyBones.Hips);
            bool noBones = hips == Vector3.zero;
            if (noBones) hips = p.GetPosition();   // 휴머노이드 뼈가 없는 아바타(ClientSim 포함): 위치·방향으로 몸통 하나
            Vector3 lp = area.InverseTransformPoint(hips);
            if (Mathf.Abs(lp.x) > half.x + 0.3f) continue;
            if (Mathf.Abs(lp.z) > half.z + 0.3f) continue;
            if (lp.y > half.y + 0.35f) continue;   // 누운 골반만(앉거나 선 사람은 이불이 머리 높이로 솟으므로 제외)
            if (noBones)
            {
                Vector3 f = p.GetRotation() * Vector3.forward;
                Vector3 c = hips + Vector3.up * 0.12f;
                Seg(c - f * 0.7f, c + f * 0.75f, 0.15f);
                continue;
            }
            Vector3 head = p.GetBonePosition(HumanBodyBones.Head);
            Vector3 chest = p.GetBonePosition(HumanBodyBones.Chest);
            Vector3 lLeg = p.GetBonePosition(HumanBodyBones.LeftLowerLeg);
            Vector3 lFoot = p.GetBonePosition(HumanBodyBones.LeftFoot);
            Vector3 rLeg = p.GetBonePosition(HumanBodyBones.RightLowerLeg);
            Vector3 rFoot = p.GetBonePosition(HumanBodyBones.RightFoot);
            Seg(head, chest, 0.11f);
            Seg(chest, hips, 0.14f);
            Seg(hips, lLeg, 0.085f);
            Seg(lLeg, lFoot, 0.065f);
            Seg(hips, rLeg, 0.085f);
            Seg(rLeg, rFoot, 0.065f);
        }
        for (int j = k * 2; j < 96; j++) segs[j] = Vector4.zero;
        mat.SetVectorArray("_Seg", segs);
        mat.SetFloat("_SegCount", k);
    }

    private void Seg(Vector3 a, Vector3 b, float r)
    {
        if (a == Vector3.zero) return;
        if (b == Vector3.zero) return;
        segs[k * 2] = new Vector4(a.x, a.y, a.z, r);
        segs[k * 2 + 1] = new Vector4(b.x, b.y, b.z, 0f);
        k++;
    }
}
