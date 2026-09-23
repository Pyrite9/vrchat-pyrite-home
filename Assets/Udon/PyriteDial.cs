// 캠프 테이블 위 다이얼 — 상호작용하면 시간을 넘긴다. (설정 결정 UI 가 생기면 치운다)
// VRChat의 Interact는 콜라이더와 UdonBehaviour가 같은 오브젝트에 있어야 발동하므로,
// 시간대 본체와 분리해서 다이얼에 따로 붙인다.
// cycle(연속 시간대)이 있으면 그쪽을 +stepHours, 없으면 예전 3단계 ToD.
using UdonSharp;
using UnityEngine;

public class PyriteDial : UdonSharpBehaviour
{
    public PyriteTimeOfDay tod;
    public PyriteDayCycle cycle;

    public override void Interact()
    {
        if (cycle != null) { cycle.SetHour(cycle.CurrentHour() + cycle.stepHours); return; }
        if (tod != null) tod.Next();
    }
}
