using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

// 돌릴 수 있는 의자 — 손잡이를 누를 때마다 45°씩 돌고, 모두에게 같은 각도로 보인다.
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteSpinChair : UdonSharpBehaviour
{
    public Transform pivot;
    public float stepDegrees = 45f;

    [UdonSynced] public int step = 0;

    void Start() { Apply(); }

    public override void Interact()
    {
        if (!Networking.IsOwner(Networking.LocalPlayer, gameObject))
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
        step = (step + 1) % 8;
        RequestSerialization();
        Apply();
    }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, step * stepDegrees, 0f);
    }
}
