// PyriteTrunkLid — 침실 트렁크 뚜껑. 누르면 열림/닫힘 (모두에게 동기화). 경첩 = 이 오브젝트, 로컬 Z 축으로 회전
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteTrunkLid : UdonSharpBehaviour
{
    public float openAngle = -100f;   // 뚜껑이 경첩(벽 쪽)에서 −X 로 뻗어 있으므로 음수 = 위로 들림
    public float speed = 2.5f;        // 초당 진행 (0→1)

    [UdonSynced] private bool open;
    private float t;

    public override void Interact()
    {
        Networking.SetOwner(Networking.LocalPlayer, gameObject);
        open = !open;
        RequestSerialization();
    }

    void Update()
    {
        float target = open ? 1f : 0f;
        if (t == target) return;
        t = Mathf.MoveTowards(t, target, Time.deltaTime * speed);
        float e = t * t * (3f - 2f * t);
        transform.localRotation = Quaternion.Euler(0f, 0f, openAngle * e);
    }
}
