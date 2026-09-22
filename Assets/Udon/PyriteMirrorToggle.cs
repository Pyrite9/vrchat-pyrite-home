using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 거울 토글 — 로컬 전용
//   거울은 보는 사람의 클라이언트에서만 렌더된다. 그래서 동기화하지 않는다.
//   동기화해 버리면 한 명이 켠 순간 월드에 있는 전원이 렌더 비용을 같이 떠안는다.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteMirrorToggle : UdonSharpBehaviour
{
    public GameObject mirror;          // VRCMirrorReflection 이 붙은 오브젝트
    public Renderer lamp;              // 켜짐 표시등
    public Material lampOff;
    public Material lampOn;

    private bool on = false;

    void Start() { Apply(); }

    public override void Interact() { on = !on; Apply(); }

    private void Apply()
    {
        if (mirror != null) mirror.SetActive(on);
        if (lamp != null && lampOff != null && lampOn != null)
            lamp.sharedMaterial = on ? lampOn : lampOff;
    }
}
