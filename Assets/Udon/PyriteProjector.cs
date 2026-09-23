// 설정 프로젝터 — 테이블 위 미니 빔. 누르면 빛줄기와 공중 패널이 켜진다/꺼진다 (로컬: 각자 자기 화면만)
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteProjector : UdonSharpBehaviour
{
    public GameObject[] onObjects;       // 빛줄기, 패널, 렌즈 빛
    public Renderer lensRenderer;        // 렌즈 유리 (켜지면 발광)
    public Material lensOn;
    public Material lensOff;
    public bool isOn = false;

    void Start() { Apply(); }

    public override void Interact()
    {
        isOn = !isOn;
        Apply();
    }

    public void TurnOff() { isOn = false; Apply(); }

    private void Apply()
    {
        for (int i = 0; i < onObjects.Length; i++) if (onObjects[i] != null) onObjects[i].SetActive(isOn);
        if (lensRenderer != null) lensRenderer.sharedMaterial = isOn ? lensOn : lensOff;
    }
}
