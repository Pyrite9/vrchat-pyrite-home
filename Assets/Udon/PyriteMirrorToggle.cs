using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

// 거울 토글 — 로컬 전용
//   거울은 보는 사람의 클라이언트에서만 렌더된다. 그래서 동기화하지 않는다.
//   동기화해 버리면 한 명이 켠 순간 월드에 있는 전원이 렌더 비용을 같이 떠안는다.
//   startOn: 캠프 거울은 false(기본 꺼짐), 호수 반사는 true(기본 켜짐, 무거우면 각자 끈다)
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteMirrorToggle : UdonSharpBehaviour
{
    public GameObject mirror;          // VRCMirrorReflection 이 붙은 오브젝트
    public Renderer lamp;              // 켜짐 표시등
    public Material lampOff;
    public Material lampOn;
    public bool startOn = false;

    private bool on = false;

    void Start() { on = startOn; Apply(); }

    public override void Interact() { on = !on; Apply(); }

    // 설정 UI 가 부른다
    public void SetOn(bool v) { on = v; Apply(); }
    public bool IsOn() { return on; }

    private void Apply()
    {
        if (mirror != null) mirror.SetActive(on);
        if (lamp != null && lampOff != null && lampOn != null)
            lamp.sharedMaterial = on ? lampOn : lampOff;
    }
}
