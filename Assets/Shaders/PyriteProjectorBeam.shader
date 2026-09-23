// 설정 프로젝터 빛줄기 — 렌즈에서 패널로 퍼지는 사각뿔 (가산 반투명)
//  uv.y: 0 = 렌즈, 1 = 패널 · 면을 비스듬히 볼수록 밝게(부피감) · 렌즈 근처 밝고 끝으로 갈수록 옅게 · 먼지 줄 약하게 흐름
Shader "Pyrite/ProjectorBeam"
{
    Properties
    {
        [HDR] _Color ("Beam Color", Color) = (1.0, 0.82, 0.58, 1)
        _Intensity ("Intensity", Float) = 0.05
        _EdgePow ("Edge Softness", Float) = 1.6
        _Dust ("Dust Streaks", Float) = 0.12
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+5" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            half4 _Color; half _Intensity, _EdgePow, _Dust;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 wn : TEXCOORD1; float3 wp : TEXCOORD2; UNITY_FOG_COORDS(3) };
            v2f vert (appdata_base v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy;
                o.wn = UnityObjectToWorldNormal(v.normal); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.wp);
                half side = pow(1.0 - saturate(abs(dot(normalize(i.wn), V))), _EdgePow);   // 면을 스치듯 볼수록 진함
                half along = lerp(1.0, 0.10, pow(saturate(i.uv.y), 0.55));                  // 렌즈 쪽 진하고 패널 쪽으로 빨리 옅어짐
                half across = smoothstep(0.0, 0.35, i.uv.x) * smoothstep(1.0, 0.65, i.uv.x); // 모서리(면 이음새) 부드럽게 → 겹침 선 제거
                half dust = 1.0 + _Dust * sin(i.uv.x * 23.0 + i.uv.y * 4.0 + _Time.y * 0.5) * sin(i.uv.x * 11.0 - _Time.y * 0.3);
                half3 c = _Color.rgb * _Intensity * (0.3 + side) * along * (0.35 + 0.65 * across) * dust;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0,0,0,0));
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
