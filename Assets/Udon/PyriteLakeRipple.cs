using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 호수 파문 — 로컬 전용(동기화 없음). 플레이어 위치·속도는 VRChat 이 이미 동기화하므로
//  각 클라이언트가 모든 플레이어의 파문을 스스로 시뮬레이션한다.
//  · 60 Hz 고정 스텝(프레임률과 무관한 파속), 한 프레임 최대 4 스텝
//  · 수면(y 0.05 = WaterWalk 윗면) ±band 안에서 수평 속도 0.2 m/s 이상인 사람만, 최대 8명
//  · 시뮬레이션 텍스처는 전역 _UdonLakeRipple 로 — VRChat 거울이 머티리얼을 복제해도 반사가 파문을 따라간다
//  AddDrop(x, z, amp): 물수제비 돌 등 바깥 물방울 (Z34b)
//  화이트리스트: SetVectorArray / GetPlayers / GetVelocity / CRT.Update / VRCShader.SetGlobalTexture 확인됨 (T7, 2026-09-23)
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteLakeRipple : UdonSharpBehaviour
{
    public CustomRenderTexture crt;
    public Material sim;
    public float stepHz = 60f;
    public int maxSteps = 4;
    public float surfaceY = 0.05f;
    public float band = 0.6f;
    public float dropAmp = 0.004f;
    public float dropRadius = 0.35f;
    public float speedRef = 3f;

    private VRCPlayerApi[] players = new VRCPlayerApi[90];
    private Vector4[] drops = new Vector4[8];
    private float acc;
    // 물수제비 돌 등 바깥에서 넣는 물방울 (최대 4개, 잠깐 유지)
    private Vector4[] ext = new Vector4[4];
    private float[] extT = new float[4];

    public void AddDrop(float x, float z, float amp)
    {
        int best = 0; float bt = 999f;
        for (int i = 0; i < 4; i++) if (extT[i] < bt) { bt = extT[i]; best = i; }
        ext[best] = new Vector4(x, z, amp, dropRadius * 1.2f);
        extT[best] = 0.07f;
    }

    void Start()
    {
        VRCShader.SetGlobalTexture(VRCShader.PropertyToID("_UdonLakeRipple"), crt);
    }

    void Update()
    {
        if (crt == null || sim == null) return;
        acc += Time.deltaTime;
        int steps = (int)(acc * stepHz);
        if (steps <= 0) return;
        acc -= steps / stepHz;
        if (steps > maxSteps) { steps = maxSteps; acc = 0f; }

        int n = VRCPlayerApi.GetPlayerCount();
        if (n > players.Length) n = players.Length;
        VRCPlayerApi.GetPlayers(players);
        int c = 0;
        for (int i = 0; i < n && c < 8; i++)
        {
            VRCPlayerApi p = players[i];
            if (!Utilities.IsValid(p)) continue;
            Vector3 pos = p.GetPosition();
            if (Mathf.Abs(pos.y - surfaceY) > band) continue;
            Vector3 v = p.GetVelocity();
            float sp = Mathf.Sqrt(v.x * v.x + v.z * v.z);
            if (sp < 0.2f) continue;
            drops[c] = new Vector4(pos.x, pos.z, dropAmp * Mathf.Clamp01(sp / speedRef), dropRadius);
            c++;
        }
        for (int e = 0; e < 4 && c < 8; e++)
        {
            if (extT[e] <= 0f) continue;
            drops[c] = ext[e]; c++;
            extT[e] -= steps / stepHz;
        }
        for (int j = c; j < 8; j++) drops[j] = Vector4.zero;
        sim.SetVectorArray("_Drops", drops);
        sim.SetFloat("_DropCount", c);
        crt.Update(steps);
    }
}
