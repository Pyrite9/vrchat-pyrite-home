// PyriteStarGlobe — 협탁 위 별 구. 누르면 별 조명(천장 별 + 구 발광)을 켜고 끔 (로컬, 동기화 없음). 상태는 PyriteBedroomPanel 이 가짐
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteStarGlobe : UdonSharpBehaviour
{
    public PyriteBedroomPanel panel;

    public override void Interact()
    {
        if (panel != null) panel.ToggleStar();
    }
}
