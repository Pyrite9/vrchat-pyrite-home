// 영상 프로젝터 — 테이블 오른쪽 미니 빔. 누르면 빛줄기 + 스크린(ProTV) + 조작 패널이 켜진다/꺼진다
//  모두에게 동기화 (같이 보는 영화). 영상 재생 자체는 ProTV 가 따로 동기화한다
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteVideoProjector : UdonSharpBehaviour
{
    public GameObject[] onObjects;       // 빛줄기, 스크린, 테두리, 조작 패널, 렌즈 빛, LED
    public Renderer lensRenderer;
    public Material lensOn;
    public Material lensOff;
    [UdonSynced] public bool isOn = false;

    void Start() { Apply(); }

    public override void Interact()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        isOn = !isOn;
        RequestSerialization();
        Apply();
    }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        for (int i = 0; i < onObjects.Length; i++) if (onObjects[i] != null) onObjects[i].SetActive(isOn);
        if (lensRenderer != null) lensRenderer.sharedMaterial = isOn ? lensOn : lensOff;
    }
}
