// PyriteLapBlanket — 머스터드 빈백 양털 한 장(등받이~좌석~앞 바닥). 침대 이불(PyriteBlanket)과 같은 방식:
//  이 빈백에 앉은 사람의 다리 뼈 선분(허벅지 2 · 정강이 2 · 사타구니 1)을 Pyrite/SheepThrow 머티리얼 _Seg 로 → 셰이더가 정점을 들어 올림
//  누르기·동기화 없음 (2026-09-30 관리자: 토글 없애고 기본 on). 뼈 위치는 VRChat 이 이미 동기화하므로 각 클라이언트가 자기 화면에서 계산
//  반지름은 허벅지 길이 비례(기준 0.329 m → 0.10) + 셰이더 _Thick — 옷 두께를 넉넉히 흡수 (첫 판 고정 메시는 옷 속으로 파고듦)
//  뼈 없는 아바타는 SitPoint 앞으로 선분 1개
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteLapBlanket : UdonSharpBehaviour
{
    public Material mat;
    public Transform sitPoint;
    public float seatRadius = 0.35f;
    public float seatUpMin = -0.15f, seatUpMax = 0.40f;
    public float thighR = 0.30f;       // 반지름 = 허벅지 길이 × 이 값
    public float shinR = 0.23f;
    public float rMin = 0.06f, rMax = 0.14f;

    private VRCPlayerApi[] players;
    private Vector4[] segs;
    private int k;
    private int lastK = -1;

    void Start()
    {
        players = new VRCPlayerApi[32];
        segs = new Vector4[32];
        Clear();
    }

    void Update()
    {
        k = 0;
        int n = VRCPlayerApi.GetPlayerCount();
        if (n > 32) n = 32;
        VRCPlayerApi.GetPlayers(players);
        Vector3 sp = sitPoint.position, su = sitPoint.up;
        for (int i = 0; i < n && k < 12; i++)
        {
            VRCPlayerApi p = players[i];
            if (!Utilities.IsValid(p)) continue;
            if ((p.GetPosition() - sp).sqrMagnitude > 4f) continue;
            Vector3 hips = p.GetBonePosition(HumanBodyBones.Hips);
            if (hips == Vector3.zero)
            {
                Vector3 q = p.GetPosition() - sp;
                if (Vector3.ProjectOnPlane(q, su).magnitude > 0.25f) continue;
                Vector3 c = sp + su * 0.08f;
                Seg(c, c + sitPoint.forward * 0.40f, 0.12f);
                continue;
            }
            Vector3 dd = hips - sp;
            float upv = Vector3.Dot(dd, su);
            if (upv < seatUpMin || upv > seatUpMax) continue;
            if (Vector3.ProjectOnPlane(dd, su).magnitude > seatRadius) continue;
            Vector3 lu = p.GetBonePosition(HumanBodyBones.LeftUpperLeg), ru = p.GetBonePosition(HumanBodyBones.RightUpperLeg);
            Vector3 lk = p.GetBonePosition(HumanBodyBones.LeftLowerLeg), rk = p.GetBonePosition(HumanBodyBones.RightLowerLeg);
            Vector3 lf = p.GetBonePosition(HumanBodyBones.LeftFoot), rf = p.GetBonePosition(HumanBodyBones.RightFoot);
            if (lu == Vector3.zero || lk == Vector3.zero) continue;
            Vector3 f = lk - lu;
            if (f.sqrMagnitude < 0.0001f || Vector3.Dot(f.normalized, su) < -0.6f) continue;   // 선 자세 제외
            float len = f.magnitude;
            float tr = Mathf.Clamp(len * thighR, rMin, rMax), sr = Mathf.Clamp(len * shinR, rMin * 0.8f, rMax);
            Seg(lu, lk, tr);
            Seg(ru, rk, tr);
            Seg(lu, ru, tr * 1.1f);
            Seg(lk, lf, sr);
            Seg(rk, rf, sr);
        }
        if (k != lastK || k > 0)
        {
            for (int j = k * 2; j < 32; j++) segs[j] = Vector4.zero;
            mat.SetVectorArray("_Seg", segs);
            mat.SetFloat("_SegCount", k);
            lastK = k;
        }
    }

    private void Clear()
    {
        for (int j = 0; j < 32; j++) segs[j] = Vector4.zero;
        mat.SetVectorArray("_Seg", segs);
        mat.SetFloat("_SegCount", 0f);
        lastK = 0;
    }

    private void Seg(Vector3 a, Vector3 b, float r)
    {
        if (a == Vector3.zero || b == Vector3.zero) return;
        segs[k * 2] = new Vector4(a.x, a.y, a.z, r);
        segs[k * 2 + 1] = new Vector4(b.x, b.y, b.z, 0f);
        k++;
    }
}
