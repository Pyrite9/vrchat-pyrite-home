// 첫 방문 안내 — 처음 들어온 사람에게만 설정 패널을 켜고 '상호작용' 팝업을 연다 (로컬)
//  VRChat Persistence(PlayerData)에 key 가 없으면 첫 방문. 자동으로 연 패널을 그 사람이 닫는 순간 key 를 저장 → 다음부터는 안 켠다
//  PlayerData 는 OnPlayerRestored 뒤에만 믿을 수 있다. 이벤트가 안 오면(저장소 없음) 아무것도 안 한다 = 예전과 같음
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteFirstVisit : UdonSharpBehaviour
{
    public PyriteProjector projector;
    public PyriteSettings settings;
    public string key = "pyrite_intro_seen";
    public bool openGuide = true;

    private bool autoOpened;
    private bool done;
    private float next;

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        if (!Utilities.IsValid(player) || !player.isLocal || done || autoOpened) return;
        if (PlayerData.HasKey(player, key)) { done = true; return; }
        if (projector == null) return;
        autoOpened = true;
        projector.TurnOn();
        if (openGuide) SendCustomEventDelayedFrames(nameof(OpenGuideLater), 3);
    }

    public void OpenGuideLater()
    {
        if (settings != null && projector != null && projector.isOn) settings.OpenGuide();
    }

    void Update()
    {
        if (!autoOpened || done) return;
        if (Time.time < next) return;
        next = Time.time + 0.5f;
        if (!projector.isOn)
        {
            PlayerData.SetBool(key, true);
            done = true;
        }
    }
}
