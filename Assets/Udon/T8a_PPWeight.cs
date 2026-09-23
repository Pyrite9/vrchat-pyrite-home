// 라디오 설정 패널 전 화이트리스트 검사. 지워도 된다.
using UdonSharp; using UnityEngine; using UnityEngine.Rendering.PostProcessing;
public class T8a_PPWeight : UdonSharpBehaviour { public PostProcessVolume v; void Start() { v.weight = 0.5f; } }
