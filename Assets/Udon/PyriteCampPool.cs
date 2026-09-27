// 캠프 소품 개수 · QvPen 소환 (2026-09-27) — 설정 패널 2쪽에서 부른다
//  VRChat 은 실행 중에 동기화 물건을 새로 만들 수 없다 → 의자 10 · 돗자리 6 을 미리 만들어 두고 켜고 끈다
//  chairMask / matMask: 비트 i = i 번째 물건이 켜짐. 모두에게 동기화(Manual). 기본 = 원래 의자 3개(0~2) · 돗자리 1개(0)
//  더하기 = 꺼진 것 중 번호가 가장 작은 것을 켜고 그 물건의 제자리(home)에 둔다
//  빼기   = 켜진 것 중 번호가 가장 큰 '자유로운' 것을 끈다 (누가 들고 있거나 앉아·누워 있으면 건너뜀)
//  제자리 = 켜진 자유로운 물건을 전부 제자리로
//  펜 소환 = 아무도 안 든 QvPen 펜(또는 지우개) 하나를 내 앞 0.45 m, 가슴 높이로 옮긴다. 펜 제자리 = 자유로운 펜·지우개를 VRCObjectSync.Respawn
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteCampPool : UdonSharpBehaviour
{
    public GameObject[] chairs;
    public Transform[] chairHomes;
    public GameObject[] mats;
    public Transform[] matHomes;
    public GameObject[] pens;
    public GameObject[] erasers;

    [UdonSynced] public int chairMask = 7;
    [UdonSynced] public int matMask = 1;

    private int penCursor;
    private int eraserCursor;

    private void Start() { Apply(); }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        ApplyKind(chairs, chairMask);
        ApplyKind(mats, matMask);
    }

    private void ApplyKind(GameObject[] arr, int mask)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;
            bool on = (mask & (1 << i)) != 0;
            if (arr[i].activeSelf != on) arr[i].SetActive(on);
        }
    }

    private void Commit()
    {
        RequestSerialization();
        Apply();
    }

    // ── 개수 (설정 패널이 읽는다) ──
    public int ChairCount() { return Count(chairMask, chairs.Length); }
    public int MatCount() { return Count(matMask, mats.Length); }
    public int ChairMax() { return chairs.Length; }
    public int MatMax() { return mats.Length; }

    private int Count(int mask, int n)
    {
        int c = 0;
        for (int i = 0; i < n; i++) if ((mask & (1 << i)) != 0) c++;
        return c;
    }

    public void AddChair() { int m = Add(chairs, chairHomes, chairMask); if (m != chairMask) { chairMask = m; Commit(); } }
    public void RemoveChair() { int m = Remove(chairs, chairMask); if (m != chairMask) { chairMask = m; Commit(); } }
    public void AddMat() { int m = Add(mats, matHomes, matMask); if (m != matMask) { matMask = m; Commit(); } }
    public void RemoveMat() { int m = Remove(mats, matMask); if (m != matMask) { matMask = m; Commit(); } }

    private int Add(GameObject[] arr, Transform[] homes, int mask)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null || (mask & (1 << i)) != 0) continue;
            VRCPlayerApi lp = Networking.LocalPlayer;
            Networking.SetOwner(lp, gameObject);
            arr[i].SetActive(true);
            Networking.SetOwner(lp, arr[i]);
            if (i < homes.Length && homes[i] != null) Place(arr[i], homes[i].position, homes[i].rotation);
            return mask | (1 << i);
        }
        return mask;   // 전부 켜져 있다
    }

    private int Remove(GameObject[] arr, int mask)
    {
        for (int i = arr.Length - 1; i >= 0; i--)
        {
            if (arr[i] == null || (mask & (1 << i)) == 0 || !Free(arr[i])) continue;
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            return mask & ~(1 << i);
        }
        return mask;
    }

    public void ResetProps()
    {
        ResetKind(chairs, chairHomes, chairMask);
        ResetKind(mats, matHomes, matMask);
    }

    private void ResetKind(GameObject[] arr, Transform[] homes, int mask)
    {
        VRCPlayerApi lp = Networking.LocalPlayer;
        for (int i = 0; i < arr.Length && i < homes.Length; i++)
        {
            if (arr[i] == null || homes[i] == null || (mask & (1 << i)) == 0 || !Free(arr[i])) continue;
            Networking.SetOwner(lp, arr[i]);
            Place(arr[i], homes[i].position, homes[i].rotation);
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

    // ── QvPen ──
    public void SummonPen() { penCursor = Summon(pens, penCursor); }
    public void SummonEraser() { eraserCursor = Summon(erasers, eraserCursor); }

    private int Summon(GameObject[] arr, int cursor)
    {
        if (arr == null || arr.Length == 0) return cursor;
        VRCPlayerApi lp = Networking.LocalPlayer;
        if (!Utilities.IsValid(lp)) return cursor;
        for (int k = 0; k < arr.Length; k++)
        {
            int i = (cursor + k) % arr.Length;
            GameObject o = arr[i];
            if (o == null || !o.activeInHierarchy || !Free(o)) continue;
            VRCPlayerApi.TrackingData head = lp.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
            Vector3 f = head.rotation * Vector3.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 0.0001f) f = lp.GetRotation() * Vector3.forward;
            f.Normalize();
            Vector3 pos = head.position + f * 0.45f + Vector3.down * 0.25f;
            Vector3 side = Vector3.Cross(Vector3.up, f);
            for (int n = 0; n < 4 && Crowded(arr, o, pos); n++) pos += side * 0.12f;   // 연달아 부르면 옆으로 12 cm 씩 (ClientSim: 같은 자리에 겹쳤다)
            Networking.SetOwner(lp, o);
            Place(o, pos, Quaternion.LookRotation(f, Vector3.up));
            return (i + 1) % arr.Length;
        }
        return cursor;   // 전부 누가 들고 있다
    }

    private bool Crowded(GameObject[] arr, GameObject self, Vector3 p)
    {
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null || arr[i] == self) continue;
            if ((arr[i].transform.position - p).sqrMagnitude < 0.0064f) return true;
        }
        return false;
    }

    public void ReturnPens()
    {
        RespawnAll(pens);
        RespawnAll(erasers);
    }

    private void RespawnAll(GameObject[] arr)
    {
        if (arr == null) return;
        VRCPlayerApi lp = Networking.LocalPlayer;
        for (int i = 0; i < arr.Length; i++)
        {
            GameObject o = arr[i];
            if (o == null || !o.activeInHierarchy || !Free(o)) continue;
            VRCObjectSync sync = (VRCObjectSync)o.GetComponent(typeof(VRCObjectSync));
            if (sync == null) continue;
            Networking.SetOwner(lp, o);
            sync.Respawn();
        }
    }
}
