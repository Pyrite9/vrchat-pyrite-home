// 캠프 테이블 위 다이얼 — 상호작용하면 시간대를 넘긴다.
// VRChat의 Interact는 콜라이더와 UdonBehaviour가 같은 오브젝트에 있어야 발동하므로,
// 시간대 본체(PyriteTimeOfDay)와 분리해서 다이얼에 따로 붙인다.
using UdonSharp;
using UnityEngine;

public class PyriteDial : UdonSharpBehaviour
{
    public PyriteTimeOfDay tod;

    public override void Interact()
    {
        if (tod != null) tod.Next();
    }
}
