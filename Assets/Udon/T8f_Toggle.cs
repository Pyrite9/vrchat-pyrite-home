// 라디오 설정 패널 전 화이트리스트 검사. 지워도 된다.
using UdonSharp; using UnityEngine; 
public class T8f_Toggle : UdonSharpBehaviour { public UnityEngine.UI.Toggle t; void Start() { bool b = t.isOn; t.SetIsOnWithoutNotify(true); } }
