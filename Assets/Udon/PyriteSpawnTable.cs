// 타프 뒤 소품 테이블 (2026-09-27) — 의자·돗자리를 더 꺼내고(생성), 한 번에 제자리로 돌려놓는다(정리)
//  VRChat 은 실행 중에 동기화 오브젝트를 새로 만들 수 없다 → 미리 숨겨 둔 추가분(풀)을 켜고 끈다
//  chairs[0..chairHomes.Length-1] = 원래 있던 의자(항상 켜짐), 나머지 = 추가분. 켜짐 여부는 chairMask 비트로 모두에게 동기화 (돗자리도 같음)
//  생성: 테이블·그 물건의 주인이 되어 비어 있는 추가분을 켜고, 다른 물건과 가장 먼 생성 자리에 둔다 (위치는 각 물건의 VRCObjectSync)
//  정리: 원래 것은 처음 자리로, 추가분은 숨긴다. 누가 들고 있거나 앉아·누워 있는 것(pickupable false)은 건드리지 않는다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteSpawnTable : UdonSharpBehaviour
{
    public GameObject[] chairs;
    public Transform[] chairHomes;
    public Transform[] chairSpots;
    public GameObject[] mats;
    public Transform[] matHomes;
    public Transform[] matSpots;

    [UdonSynced] public int chairMask;
    [UdonSynced] public int matMask;

    private void Start() { Apply(); }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        ApplyKind(chairs, chairHomes.Length, chairMask);
        ApplyKind(mats, matHomes.Length, matMask);
    }

    private void ApplyKind(GameObject[] arr, int home, int mask)
    {
        for (int i = home; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;
            bool on = (mask & (1 << (i - home))) != 0;
            if (arr[i].activeSelf != on) arr[i].SetActive(on);
        }
    }

    public void SpawnChair()
    {
        int m = Spawn(chairs, chairHomes.Length, chairMask, chairSpots);
        if (m < 0) return;
        chairMask = m;
        Commit();
    }

    public void SpawnMat()
    {
        int m = Spawn(mats, matHomes.Length, matMask, matSpots);
        if (m < 0) return;
        matMask = m;
        Commit();
    }

    public void ResetAll()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        chairMask = ResetKind(chairs, chairHomes, chairMask);
        matMask = ResetKind(mats, matHomes, matMask);
        Commit();
    }

    private void Commit()
    {
        RequestSerialization();
        Apply();
    }

    private int Spawn(GameObject[] arr, int home, int mask, Transform[] spots)
    {
        int idx = -1;
        for (int i = home; i < arr.Length; i++)
        {
            if (arr[i] != null && (mask & (1 << (i - home))) == 0) { idx = i; break; }
        }
        if (idx < 0) return -1;   // 추가분을 다 꺼냈다
        VRCPlayerApi lp = Networking.LocalPlayer;
        Networking.SetOwner(lp, gameObject);
        GameObject o = arr[idx];
        o.SetActive(true);
        Networking.SetOwner(lp, o);
        Transform s = BestSpot(arr, spots, o);
        if (s != null) Place(o, s);
        return mask | (1 << (idx - home));
    }

    // 켜져 있는 같은 종류 물건들과의 최소 수평 거리가 가장 큰 자리
    private Transform BestSpot(GameObject[] arr, Transform[] spots, GameObject self)
    {
        Transform best = null;
        float bestD = -1f;
        for (int s = 0; s < spots.Length; s++)
        {
            if (spots[s] == null) continue;
            Vector3 p = spots[s].position;
            float d = 1000f;
            for (int i = 0; i < arr.Length; i++)
            {
                GameObject o = arr[i];
                if (o == null || o == self || !o.activeInHierarchy) continue;
                Vector3 q = o.transform.position;
                float dx = q.x - p.x;
                float dz = q.z - p.z;
                float dd = Mathf.Sqrt(dx * dx + dz * dz);
                if (dd < d) d = dd;
            }
            if (d > bestD) { bestD = d; best = spots[s]; }
        }
        return best;
    }

    private int ResetKind(GameObject[] arr, Transform[] homes, int mask)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        int home = homes.Length;
        for (int i = 0; i < home && i < arr.Length; i++)
        {
            GameObject o = arr[i];
            if (o == null || homes[i] == null || !Free(o)) continue;
            Networking.SetOwner(lp, o);
            Place(o, homes[i]);
        }
        for (int i = home; i < arr.Length; i++)
        {
            int bit = 1 << (i - home);
            if ((mask & bit) == 0 || arr[i] == null) continue;
            if (Free(arr[i])) mask &= ~bit;
        }
        return mask;
    }

    private bool Free(GameObject o)
    {
        VRCPickup p = (VRCPickup)o.GetComponent(typeof(VRCPickup));
        if (p == null) return true;
        return !p.IsHeld && p.pickupable;
    }

    private void Place(GameObject o, Transform t)
    {
        o.transform.SetPositionAndRotation(t.position, t.rotation);
        VRCObjectSync sync = (VRCObjectSync)o.GetComponent(typeof(VRCObjectSync));
        if (sync != null) sync.FlagDiscontinuity();
    }
}
