// Pyrite/SkyDisc — 하늘에 붙은 해·달 원반 (가산 합성)
//  오브젝트 위치를 무시하고 "카메라 + 방향 × 먼 거리" 에 빌보드로 그린다.
//  → 어디서 봐도 스카이박스처럼 같은 방향에 있다 (시차 없음). 절벽·지형 뒤로는 깊이 테스트로 가려진다.
//  거리는 카메라 far 의 90% — 리플렉션 프로브(far 220) 에서도 잘리지 않는다.
//  메시 바운즈는 에디터 스크립트가 크게 잡아 프러스텀 컬링을 피한다.
Shader "Pyrite/SkyDisc"
{
    Properties
    {
        _MainTex ("Texture (RGB, 가산)", 2D) = "black" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Dir ("Direction (world, 해·달 쪽)", Vector) = (0,0.2,-1,0)
        _AngSize ("Quad Half Angle (deg)", Float) = 4
    }
    SubShader
    {
        Tags { "Queue"="Transparent-1" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4 _Color;
            float4 _Dir;
            float _AngSize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 d = normalize(_Dir.xyz);
                float3 up0 = abs(d.y) > 0.99 ? float3(0, 0, 1) : float3(0, 1, 0);
                float3 r = normalize(cross(up0, d));
                float3 u = cross(d, r);
                float dist = _ProjectionParams.z * 0.9;
                float s = dist * tan(radians(_AngSize)) * 2.0;
                float3 wp = _WorldSpaceCameraPos + d * dist + (r * v.vertex.x + u * v.vertex.y) * s;
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.uv = v.uv;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half3 c = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                return half4(c, 1);
            }
            ENDCG
        }
    }
}
