// 소품 테이블 버튼 — 누르면 PyriteSpawnTable 의 이벤트(SpawnChair / SpawnMat / ResetAll)를 부르고, 윗부분이 잠깐 눌린다 (로컬)
using UdonSharp;
using UnityEngine;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteSpawnButton : UdonSharpBehaviour
{
    public PyriteSpawnTable table;
    public string eventName = "SpawnChair";
    public Transform cap;          // 눌리는 부분 (버튼 윗판 + 미니어처)
    public float pressDepth = 0.008f;

    private Vector3 capRest;
    private bool hasRest;

    public override void Interact()
    {
        if (table != null) table.SendCustomEvent(eventName);
        if (cap == null) return;
        if (!hasRest) { capRest = cap.localPosition; hasRest = true; }
        cap.localPosition = capRest - new Vector3(0f, pressDepth, 0f);
        SendCustomEventDelayedSeconds(nameof(Release), 0.15f);
    }

    public void Release()
    {
        if (cap != null && hasRest) cap.localPosition = capRest;
    }
}
