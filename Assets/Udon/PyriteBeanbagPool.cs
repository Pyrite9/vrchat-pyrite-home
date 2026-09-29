// PyriteBeanbagPool — 침실 빈백 개수 0~6 (2026-09-30 관리자: 개수 조절 · 색 3가지 · 캠프 의자처럼 들고 옮기기)
//  VRChat 은 실행 중에 동기화 물건을 새로 못 만든다 → 빈백 6개를 미리 두고 켜고 끈다 (PyriteCampPool 과 같은 방식)
//  mask: 비트 i = i 번째 빈백 켜짐. 모두에게 동기화(Manual). 기본 3 = 1·2번 (V 자)
//  더하기 = 꺼진 것 중 번호가 가장 작은 것을 켜서 제자리(home)에 / 빼기 = 켜진 것 중 번호가 가장 큰 '자유로운' 것(안 들림·안 앉음)을 끔
//  제자리 = 켜진 자유로운 빈백을 전부 home 으로. 머리맡 패널(PyriteBedroomPanel)이 부른다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteBeanbagPool : UdonSharpBehaviour
{
    public GameObject[] bags;
    public Transform[] homes;

    [UdonSynced] public int mask = 3;

    private void Start() { Apply(); }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        for (int i = 0; i < bags.Length; i++)
        {
            if (bags[i] == null) continue;
            bool on = (mask & (1 << i)) != 0;
            if (bags[i].activeSelf != on) bags[i].SetActive(on);
        }
    }

    private void Commit()
    {
        RequestSerialization();
        Apply();
    }

    public int Count()
    {
        int c = 0;
        for (int i = 0; i < bags.Length; i++) if ((mask & (1 << i)) != 0) c++;
        return c;
    }

    public int Max() { return bags.Length; }

    public void Add()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        for (int i = 0; i < bags.Length; i++)
        {
            if (bags[i] == null || (mask & (1 << i)) != 0) continue;
            Networking.SetOwner(lp, gameObject);
            bags[i].SetActive(true);
            Networking.SetOwner(lp, bags[i]);
            if (i < homes.Length && homes[i] != null) Place(bags[i], homes[i].position, homes[i].rotation);
            mask = mask | (1 << i);
            Commit();
            return;
        }
    }

    public void Remove()
    {
        for (int i = bags.Length - 1; i >= 0; i--)
        {
            if (bags[i] == null || (mask & (1 << i)) == 0 || !Free(bags[i])) continue;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            mask = mask & ~(1 << i);
            Commit();
            return;
        }
    }

    public void ResetAll()
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        for (int i = 0; i < bags.Length && i < homes.Length; i++)
        {
            if (bags[i] == null || homes[i] == null || (mask & (1 << i)) == 0 || !Free(bags[i])) continue;
            Networking.SetOwner(lp, bags[i]);
            Place(bags[i], homes[i].position, homes[i].rotation);
        }
    }

    private bool Free(GameObject o)
    {
        VRCPickup p = (VRCPickup)o.GetComponent(typeof(VRCPickup));
        if (p == null) return true;
        return !p.IsHeld && p.pickupable;
    }

    private void Place(GameObject o, Vector3 pos, Quaternion rot)
    {
        o.transform.SetPositionAndRotation(pos, rot);
        VRCObjectSync sync = (VRCObjectSync)o.GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.FlagDiscontinuity();
    }
}
