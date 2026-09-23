// 망원경 접안 화면 — 머리 앞 사각형에 망원경 카메라 RT 를 둥근 시야로, 바깥은 검게. 모든 것 위에 그린다
Shader "Pyrite/ScopeView"
{
    Properties
    {
        _MainTex ("View RT", 2D) = "black" {}
        _Radius ("Circle radius (uv)", Float) = 0.1
        _Soft ("Edge softness (uv)", Float) = 0.006
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+10" "RenderType" = "Overlay" "IgnoreProjector" = "True" }
        ZTest Always
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Radius, _Soft;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float r = length(d);
                float m = 1.0 - smoothstep(_Radius - _Soft, _Radius, r);
                float2 suv = d / (2.0 * _Radius) + 0.5;
                fixed3 c = tex2D(_MainTex, suv).rgb;
                c *= lerp(0.55, 1.0, 1.0 - smoothstep(_Radius * 0.55, _Radius, r));   // 렌즈 가장자리 어둡게
                return fixed4(c * m, 1.0);
            }
            ENDCG
        }
    }
}
