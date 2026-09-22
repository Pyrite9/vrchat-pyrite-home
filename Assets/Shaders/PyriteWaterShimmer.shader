// 수면 발광 — 스크롤 노이즈 2겹 애디티브. 불투명도/깊이에 영향 없음.
Shader "Pyrite/WaterShimmer"
{
    Properties
    {
        _MainTex ("Shimmer (R)", 2D) = "black" {}
        _Tint    ("Tint", Color) = (0.45, 0.72, 1.0, 1)
        _Gain    ("Gain", Range(0,8)) = 1.0
        _Scale1  ("Scale 1", Float) = 7.0
        _Scale2  ("Scale 2", Float) = 11.0
        _Speed1  ("Speed 1", Vector) = (0.012, 0.007, 0, 0)
        _Speed2  ("Speed 2", Vector) = (-0.008, 0.011, 0, 0)
        _EdgeIn  ("Edge fade start", Range(0,0.5)) = 0.34
        _EdgeOut ("Edge fade end",   Range(0,0.5)) = 0.485
    }
    SubShader
    {
        Tags { "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off
        Fog { Mode Off }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;
            float  _Gain, _Scale1, _Scale2, _EdgeIn, _EdgeOut;
            float4 _Speed1, _Speed2;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0;
                             UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0;
                             UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 a = i.uv * _Scale1 + _Time.y * _Speed1.xy * _Scale1;
                float2 b = i.uv * _Scale2 + _Time.y * _Speed2.xy * _Scale2;
                float  g1 = tex2D(_MainTex, a).r;
                float  g2 = tex2D(_MainTex, b).r;
                // 아주 옅은 윤기(g1) + 드문드문 튀는 반짝임.
                // 곱만 쓰면 거의 0이라 안 보이고, 곱을 그대로 키우면 수면이 서리처럼 덮인다.
                // pow로 어두운 쪽을 눌러서 평균은 낮추고 피크만 살린다.
                float  sp = g1 * g2;
                float  n  = g1 * 0.06 + pow(sp, 1.8) * 6.0;

                // 판 중심에서의 거리로 가장자리를 죽인다 (물가에서 끊기지 않게)
                float d = length(i.uv - 0.5);
                float edge = 1.0 - smoothstep(_EdgeIn, _EdgeOut, d);

                return fixed4(_Tint.rgb * (n * _Gain * edge), 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
