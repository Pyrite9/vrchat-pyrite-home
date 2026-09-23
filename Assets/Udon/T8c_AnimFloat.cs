// 라디오 설정 패널 전 화이트리스트 검사. 지워도 된다.
using UdonSharp; using UnityEngine; 
public class T8c_AnimFloat : UdonSharpBehaviour { public Animator a; void Start() { a.SetFloat("w", 0.5f); } }
