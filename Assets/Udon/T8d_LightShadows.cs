// 라디오 설정 패널 전 화이트리스트 검사. 지워도 된다.
using UdonSharp; using UnityEngine; 
public class T8d_LightShadows : UdonSharpBehaviour { public Light l; void Start() { l.shadows = LightShadows.None; } }
