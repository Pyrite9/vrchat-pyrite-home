// 라디오 설정 패널 전 화이트리스트 검사. 지워도 된다.
using UdonSharp; using UnityEngine; 
public class T8g_ShadowCast : UdonSharpBehaviour { public Renderer r; void Start() { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; } }
