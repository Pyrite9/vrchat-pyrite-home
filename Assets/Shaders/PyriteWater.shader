// Pyrite/Water — サカナ「VRC向け水面シェーダー」(WaterShader_BuiltIn_VR) 를 바탕으로 고친 호수 셰이더
//   원본: サカナ-sakanasan- https://booth.pm/ja/items/7882111 (개인·법인 상업 이용, 동봉 배포 허용)
//
// 원본 대비 바뀐 것
//  [F2] 큰 물결 — 방향·파장이 다른 사인파 4개(파장 5.5~17m, 합 높이 ~6cm).
//       높이만 움직이면 거의 안 보이므로 기울기(해석적 미분)로 면 방향까지 흔든다 → 하늘·별 반사가 출렁인다.
//       면 기울기는 _WaveNormal 배로 과장 (높이는 작게, 반사는 크게).
//  [F3] 잔물결 노멀맵 2겹 → 3겹, 서로 다른 방향으로 흐름. 멀수록 약하게(지글거림 방지).
//  [E2] 수심 텍스처(_DepthTex, 지형 높이맵에서 구움) — 카메라 깊이 텍스처 없이 수심을 안다.
//       얕은 곳은 투명해지며 바닥이 비치고, 반사·물색이 줄어 물가 선이 부드럽게 땅으로 이어진다.
//       물결 높이도 얕은 곳에서 0 으로 — 물이 모래 위로 튀어나오지 않는다.
//  [F1] 정점 간격 1m 격자 메시 전제 (Unity Plane 은 정점 간격이 11.6m 라 물결을 표현 못 한다)
Shader "Pyrite/Water"
{
    Properties
    {
        [Header(Color Settings)]
        _BaseColor("Shallow Color (Base)", Color) = (0.2, 0.6, 0.7, 1.0)
        _DeepColor("Deep Water Color", Color) = (0.1, 0.3, 0.4, 1.0)
        _Alpha("Alpha (Transparency)", Range(0, 1)) = 1

        [Header(Ripples)]
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        _WaveSpeed("Ripple Speed", Float) = 0.4
        _NormalScale("Ripple Strength", Range(0, 1)) = 0.35
        _RippleFar("Ripple Far Fade (m)", Float) = 90

        [Header(Swell)]
        _WaveHeight("Swell Height (x)", Float) = 1
        _WaveTime("Swell Speed (x)", Float) = 0.55
        _WaveNormal("Swell Slope Boost", Float) = 3

        [Header(Surface)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.95
        _FresnelPower("Fresnel Power", Range(0.1, 10.0)) = 3.0
        _RefractionStrength("Refraction Strength", Range(0, 0.1)) = 0.03
        _WaterDepth("Water Depth Effect", Range(0, 1)) = 0.52

        [Header(Shore)]
        [NoScaleOffset] _DepthTex("Lake Depth (R = depth / DepthMax)", 2D) = "black" {}
        _DepthRect("Depth Rect (xmin, zmin, 1/w, 1/h)", Vector) = (-64, -78, 0.0078125, 0.0078125)
        _DepthMax("Depth Max (m)", Float) = 4
        _ShoreFade("Shore Color Fade (m)", Float) = 0.8
        _ShoreAlpha("Shore Alpha Fade (m)", Float) = 0.35
        _SwellFade("Swell Fade Depth (m)", Float) = 0.6

        [Header(Player Ripples)]
        _PlayerRipple("Player Ripple Normal (x)", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200

        GrabPass { "_WaterBackground" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
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
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;   // 물결로 밀기 전 위치 (xz 로 수심·물결을 다시 계산)
                float4 grabPos : TEXCOORD1;
                float depth : TEXCOORD2;       // 카메라 거리
                UNITY_FOG_COORDS(3)
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _NormalMap;
            sampler2D _WaterBackground;
            sampler2D _DepthTex;

            float4 _BaseColor, _DeepColor;
            float _Alpha, _NormalScale, _WaveSpeed, _RippleFar;
            float _WaveHeight, _WaveTime, _WaveNormal;
            float _Smoothness, _FresnelPower, _RefractionStrength, _WaterDepth;
            float4 _DepthRect;
            float _DepthMax, _ShoreFade, _ShoreAlpha, _SwellFade;

            // [Z16] 사람이 만든 파문 — 전역 _UdonLakeRipple(PyriteLakeRipple 의 시뮬레이션, R = 높이). 범위는 수심 텍스처와 같다.
            //       전역이 없으면(에디터) 상수 텍스처라 기울기 0 → 영향 없음
            sampler2D _UdonLakeRipple;
            float _PlayerRipple;
            float2 PlayerRippleGrad(float2 xz)
            {
                float2 uv = (xz - float2(-64, -78)) / 128.0;
                if (any(uv < 0) || any(uv > 1)) return 0;
                const float e = 1.0 / 1024.0;
                float hx = tex2Dlod(_UdonLakeRipple, float4(uv + float2(e, 0), 0, 0)).r - tex2Dlod(_UdonLakeRipple, float4(uv - float2(e, 0), 0, 0)).r;
                float hz = tex2Dlod(_UdonLakeRipple, float4(uv + float2(0, e), 0, 0)).r - tex2Dlod(_UdonLakeRipple, float4(uv - float2(0, e), 0, 0)).r;
                return float2(hx, hz) / (2.0 * 0.125);         // m/m
            }

            // 수심 (m) — 텍스처 밖은 0 (= 땅)
            float LakeDepth(float2 xz)
            {
                float2 uv = (xz - _DepthRect.xy) * _DepthRect.zw;
                if (any(uv < 0) || any(uv > 1)) return 0;
                return tex2Dlod(_DepthTex, float4(uv, 0, 0)).r * _DepthMax;
            }

            static const float2 SW_D[4] = { float2(0.940, 0.342), float2(0.643, 0.766), float2(0.993, -0.122), float2(0.208, 0.978) };
            static const float  SW_L[4] = { 17.0, 12.7, 8.3, 5.5 };
            static const float  SW_A[4] = { 0.018, 0.020, 0.015, 0.010 };
            static const float  SW_P[4] = { 0.0, 1.7, 4.1, 2.6 };

            // 큰 물결 4개 — 높이와 기울기(∂h/∂x, ∂h/∂z)
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

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldPos = wp;

                float2 g;
                float h = Swell(wp.xz, _Time.y, g);
                float fade = saturate(LakeDepth(wp.xz) / max(_SwellFade, 1e-3));
                wp.y += h * fade;

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                o.grabPos = ComputeGrabScreenPos(o.pos);
                o.depth = -mul(UNITY_MATRIX_V, float4(wp, 1.0)).z;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float lakeD = LakeDepth(i.worldPos.xz);
                float shore = saturate(lakeD / max(_ShoreFade, 1e-3));

                // 큰 물결 기울기 → 면 방향
                float2 g;
                Swell(i.worldPos.xz, _Time.y, g);
                float swellK = saturate(lakeD / max(_SwellFade, 1e-3)) * _WaveNormal;

                // 잔물결 3겹
                float t = _Time.y * _WaveSpeed;
                float2 xz = i.worldPos.xz;
                float2 uv1 = xz * 0.10 + float2( 0.020,  0.010) * t;
                float2 uv2 = xz * 0.05 + float2(-0.010, -0.030) * t;
                float2 r3  = float2(xz.x * 0.866 - xz.y * 0.5, xz.x * 0.5 + xz.y * 0.866);
                float2 uv3 = r3 * 0.23 + float2(0.035, -0.015) * t;
                half3 n1 = UnpackNormal(tex2D(_NormalMap, uv1));
                half3 n2 = UnpackNormal(tex2D(_NormalMap, uv2));
                half3 n3 = UnpackNormal(tex2D(_NormalMap, uv3));
                half2 rip = (n1.xy + n2.xy + 0.6 * n3.xy) / 2.6;   // 원본 normalize(n1+n2) 와 같은 크기대
                float far = saturate(1.0 - i.depth / max(_RippleFar, 1.0));
                rip *= _NormalScale * lerp(0.4, 1.0, far) * lerp(0.5, 1.0, shore);

                float2 pr = PlayerRippleGrad(i.worldPos.xz) * _PlayerRipple;
                float3 worldNormal = normalize(float3(rip.x - g.x * swellK - pr.x, 1.0, rip.y - g.y * swellK - pr.y));
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 굴절 (배경 = 물 뒤/아래 장면)
                float2 distortion = worldNormal.xz * _RefractionStrength * shore;
                float2 refractUV = (i.grabPos.xy + distortion * i.grabPos.w) / i.grabPos.w;
                fixed3 backgroundColor = tex2D(_WaterBackground, refractUV).rgb;

                // 물색 — 멀수록/깊을수록 깊은 색
                float distanceFactor = saturate(i.depth * _WaterDepth * 0.1);
                float deepK = max(distanceFactor, saturate(lakeD / 3.0));
                fixed3 waterColor = lerp(_BaseColor.rgb, _DeepColor.rgb, deepK);
                fixed3 refractedColor = lerp(backgroundColor, waterColor, _WaterDepth * shore);

                // 반사 (리플렉션 프로브 — 시간대별 큐브맵)
                float3 reflDir = reflect(-viewDir, worldNormal);
                float mip = (1.0 - _Smoothness) * 6.0;
                half4 spec = UNITY_SAMPLE_TEXCUBE_LOD(unity_SpecCube0, reflDir, mip);
                fixed3 reflectionColor = DecodeHDR(spec, unity_SpecCube0_HDR);
                float fresnel = pow(1.0 - saturate(dot(viewDir, worldNormal)), _FresnelPower);
                fresnel *= lerp(0.35, 1.0, shore);

                fixed3 finalColor = lerp(refractedColor, reflectionColor, fresnel);
                UNITY_APPLY_FOG(i.fogCoord, finalColor);

                float a = _Alpha * smoothstep(0.0, 1.0, saturate(lakeD / max(_ShoreAlpha, 1e-3)));
                return fixed4(finalColor, a);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
