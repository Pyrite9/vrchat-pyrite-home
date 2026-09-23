// Pyrite/WaterMirror — 호수 실시간 반사 오버레이 (VRCMirrorReflection 의 Custom Shader)
//
//  물 본체(Pyrite/Water)는 그대로 두고, 같은 격자를 한 장 더 겹쳐 그 위에 거울 반사를 얹는다.
//   · 반사 = VRChat 거울 텍스처(_ReflectionTex0/1, 눈별) — 물결 노멀로 화면 좌표를 흔들어 일렁이게
//   · 비율 = 물과 같은 프레넬(pow 3) × _MirrorStrength × 물가 페이드
//   · 물결 높이·기울기는 Pyrite/Water 와 같은 식(SW_* 상수 동일) → 두 면이 정확히 겹친다
//   · 수심은 텍스처 대신 정점 색 R(= 수심 / 4m) — VRChat 거울이 머티리얼을 새로 만들어
//     텍스처 참조가 빠져도 동작하게. 모든 값의 기본값 = 튜닝 값.
//  VRChat 거울은 -transform.forward 를 법선으로 쓴다(Z10b 인게임 실측: Quad 90,0,0 만 반사).
//  → 이 오브젝트는 (90,0,0) 회전, 메시는 그 로컬 좌표로 구워둔다. 셰이더는 월드 좌표만 쓴다.
Shader "Pyrite/WaterMirror"
{
    Properties
    {
        [HideInInspector] _ReflectionTex0("", 2D) = "black" {}
        [HideInInspector] _ReflectionTex1("", 2D) = "black" {}
        [NoScaleOffset] _NormalMap("Ripple Normal (optional)", 2D) = "bump" {}
        _MirrorStrength("Mirror Strength", Range(0, 1)) = 0.9
        _Distort("Reflection Distortion", Range(0, 0.2)) = 0.03
        _RippleDistort("Ripple Share of Distortion", Range(0, 1)) = 0.2
        _FresnelPower("Fresnel Power", Range(0.1, 10)) = 3
        _FresnelMin("Fresnel Min", Range(0, 1)) = 0.04
        _NormalScale("Ripple Strength", Range(0, 1)) = 0.35
        _WaveSpeed("Ripple Speed", Float) = 0.4
        _RippleFar("Ripple Far Fade (m)", Float) = 90
        _WaveHeight("Swell Height (x)", Float) = 1
        _WaveTime("Swell Speed (x)", Float) = 0.55
        _WaveNormal("Swell Slope Boost", Float) = 1.0
        _DepthMax("Depth Max (m)", Float) = 4
        _ShoreFade("Shore Fade (m)", Float) = 0.6
        _ShoreAlpha("Shore Alpha Fade (m)", Float) = 0.25
        _SwellFade("Swell Fade Depth (m)", Float) = 0.6
        _Lift("Lift above water (m)", Float) = 0.003
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+1" "IgnoreProjector"="True" }
        LOD 200

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float depth : TEXCOORD2;
                float lakeD : TEXCOORD3;
                UNITY_FOG_COORDS(4)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _ReflectionTex0, _ReflectionTex1, _NormalMap;
            float _MirrorStrength, _Distort, _RippleDistort, _FresnelPower, _FresnelMin;
            float _NormalScale, _WaveSpeed, _RippleFar, _WaveHeight, _WaveTime, _WaveNormal;
            float _DepthMax, _ShoreFade, _ShoreAlpha, _SwellFade, _Lift;

            // Pyrite/Water 와 같은 상수
            static const float2 SW_D[4] = { float2(0.940, 0.342), float2(0.643, 0.766), float2(0.993, -0.122), float2(0.208, 0.978) };
            static const float  SW_L[4] = { 17.0, 12.7, 8.3, 5.5 };
            static const float  SW_A[4] = { 0.018, 0.020, 0.015, 0.010 };
            static const float  SW_P[4] = { 0.0, 1.7, 4.1, 2.6 };

            float Swell(float2 p, float t, out float2 grad)
            {
                float h = 0; grad = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float k = 6.2831853 / SW_L[i];
                    float w = sqrt(9.8 * k) * _WaveTime;
                    float ph = k * dot(SW_D[i], p) - w * t + SW_P[i];
                    float a = SW_A[i] * _WaveHeight;
                    h += a * sin(ph);
                    grad += a * k * cos(ph) * SW_D[i];
                }
                return h;
            }

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = wp;
                o.lakeD = v.color.r * _DepthMax;

                float2 g;
                float h = Swell(wp.xz, _Time.y, g);
                wp.y += h * saturate(o.lakeD / max(_SwellFade, 1e-3)) + _Lift;

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.screenPos = ComputeNonStereoScreenPos(o.pos);
                o.depth = -mul(UNITY_MATRIX_V, float4(wp, 1.0)).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float shore = saturate(i.lakeD / max(_ShoreFade, 1e-3));

                float2 g;
                Swell(i.worldPos.xz, _Time.y, g);
                float swellK = saturate(i.lakeD / max(_SwellFade, 1e-3)) * _WaveNormal;

                float t = _Time.y * _WaveSpeed;
                float2 xz = i.worldPos.xz;
                float2 uv1 = xz * 0.10 + float2( 0.020,  0.010) * t;
                float2 uv2 = xz * 0.05 + float2(-0.010, -0.030) * t;
                float2 r3  = float2(xz.x * 0.866 - xz.y * 0.5, xz.x * 0.5 + xz.y * 0.866);
                float2 uv3 = r3 * 0.23 + float2(0.035, -0.015) * t;
                half2 rip = (UnpackNormal(tex2D(_NormalMap, uv1)).xy + UnpackNormal(tex2D(_NormalMap, uv2)).xy
                             + 0.6 * UnpackNormal(tex2D(_NormalMap, uv3)).xy) / 2.6;
                float far = saturate(1.0 - i.depth / max(_RippleFar, 1.0));
                rip *= _NormalScale * lerp(0.4, 1.0, far) * lerp(0.5, 1.0, shore);

                // [Z12] 잔물결은 반사 좌표를 잘게 찢어 별이 긁힌 선처럼 보였다 → 흔들림·프레넬엔 잔물결 몫을 줄인 면을 쓴다
                rip *= _RippleDistort;
                float3 n = normalize(float3(rip.x - g.x * swellK, 1.0, rip.y - g.y * swellK));
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 거울 텍스처 — 화면 좌표를 면 기울기만큼 흔든다. 멀수록 화면상 흔들림이 커지지 않게 거리로 나눔
                float2 suv = i.screenPos.xy / i.screenPos.w;
                suv += n.xz * _Distort * saturate(8.0 / max(i.depth, 1.0));
                fixed3 refl = unity_StereoEyeIndex == 0 ? tex2D(_ReflectionTex0, suv).rgb : tex2D(_ReflectionTex1, suv).rgb;

                float fresnel = _FresnelMin + (1.0 - _FresnelMin) * pow(1.0 - saturate(dot(viewDir, n)), _FresnelPower);
                float a = fresnel * _MirrorStrength * lerp(0.35, 1.0, shore)
                        * smoothstep(0.0, 1.0, saturate(i.lakeD / max(_ShoreAlpha, 1e-3)));

                UNITY_APPLY_FOG(i.fogCoord, refl);
                return fixed4(refl, saturate(a));
            }
            ENDCG
        }
    }
    FallBack Off
}
