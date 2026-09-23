// 머그 속 음료 (머그의 자식) — fill 0~1 을 모두에게 맞춘다. 차 있으면 김이 난다
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PyriteMugState : UdonSharpBehaviour
{
    [UdonSynced] public float fill;
    public Transform liquid;            // 음료 윗면 (원판)
    public float emptyY = 0.012f;
    public float fullY = 0.072f;
    public float sip = 0.34f;
    public ParticleSystem steam;

    private void Start() { Apply(); }

    public void Fill() { fill = 1f; Apply(); RequestSerialization(); }

    public void Sip()
    {
        if (fill <= 0.01f) return;
        fill = Mathf.Max(0f, fill - sip);
        if (fill < 0.02f) fill = 0f;
        Apply();
        RequestSerialization();
    }

    public override void OnDeserialization() { Apply(); }

    private void Apply()
    {
        bool has = fill > 0.01f;
        if (liquid != null)
        {
            if (liquid.gameObject.activeSelf != has) liquid.gameObject.SetActive(has);
            Vector3 p = liquid.localPosition; p.y = Mathf.Lerp(emptyY, fullY, fill); liquid.localPosition = p;
        }
        if (steam != null)
        {
            if (has && !steam.isPlaying) steam.Play();
            else if (!has && steam.isPlaying) steam.Stop();
        }
    }
}
