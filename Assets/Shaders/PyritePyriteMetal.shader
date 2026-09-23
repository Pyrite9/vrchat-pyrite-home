// 황철석 — Standard 금속 + MatCap
//
//  물리 기반 금속만으로는 "비치는 게 어두우면 어둡게" 보인다. 캠프 쪽에서 보면 결정 면에 비치는 건
//  어두운 절벽과 호수뿐이라 어두운 황동 = 카키/나무결로 읽혔다.
//  MatCap 은 보는 방향 기준으로 가짜 환경을 붙여서, 주변이 어두워도 금속 하이라이트가 항상 잡힌다.
//  세기(_MatCapStrength)는 Udon 이 프리셋마다 SetFloat 로 바꾼다 — 밤에는 달빛 받은 정도로만.
//  _MatCapTint : MatCap 은 "주변 환경의 반사" 대역이다. 밤엔 주변이 청색이니 밤하늘 색을 곱한다
//                (노랑 알베도 × 청색 = 차분한 청동). 노랑 그대로 두면 스스로 빛나는 것처럼 읽힌다.
//  _SpecNoTint : 반사 프로브는 이미 프리셋별 큐브맵이므로 밤 색조를 다시 곱하지 않는다 (PyriteNight.cginc)
//
//  평평한 큐브 면은 법선이 하나라 MatCap 한 점만 찍혀 단색이 된다.
//  조선(줄무늬) 노멀맵으로 법선을 흔들어야 면 위에 반사 띠가 생긴다 → MatCap 전용 노멀 세기를 따로 둔다.
//
//  2026-09-23 밤 보강 (기본값 0 = 이전과 같음) — 밤엔 결정이 "색칠한 깍두기"로 보였다
//   _RimColor/_RimPower     : 가장자리 금빛 (1-N·V)^p — 실루엣이 금속으로 읽히게
//   _GlintDir/_GlintColor/_GlintSharp : 지정 방향(달) 가짜 반사 하이라이트 — 방향광 세기(0.04)와 무관하게 달빛이 모서리에 맺힌다
//   _SparkleStrength/_SparkleScale    : 월드 격자 셀마다 임의 법선 → 보는 각도에 따라 반짝이는 작은 점
Shader "Pyrite/PyriteMetal"
{
    Properties
    {
        _Color ("Albedo (금속 반사색)", Color) = (0.80, 0.71, 0.46, 1)
        _MainTex ("Albedo Tex", 2D) = "white" {}
        [Gamma] _Metallic ("Metallic", Range(0,1)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.86
        [Normal] _BumpMap ("Striation Normal", 2D) = "bump" {}
        _BumpScale ("Normal Scale (조명)", Float) = 0.35
        _MatCapNormal ("Normal Scale (MatCap)", Float) = 1.2
        _StriationTile ("Striation Tiling", Float) = 3
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)

        _MatCap ("MatCap", 2D) = "gray" {}
        _MatCapStrength ("MatCap Strength (프리셋별)", Range(0,3)) = 1
        _MatCapBoost ("MatCap Boost", Float) = 1.5
        _MatCapTint ("MatCap Env Tint (프리셋별 — 밤엔 하늘색)", Color) = (1,1,1,1)
        _SpecNoTint ("Reflection: skip night tint", Float) = 1

        [HDR] _RimColor ("Rim Color", Color) = (0,0,0,1)
        _RimPower ("Rim Power", Float) = 3
        _GlintDir ("Glint Dir (world, toward light)", Vector) = (0,1,0,0)
        [HDR] _GlintColor ("Glint Color", Color) = (0,0,0,1)
        _GlintSharp ("Glint Sharpness", Float) = 60
        _SparkleStrength ("Sparkle Strength", Float) = 0
        _SparkleScale ("Sparkle Cells / m", Float) = 6

        // 밤 어둠 — 결정도 정적이라 노을 라이트맵을 받는다. 지형과 같은 색조를 곱한다.
        _NightTint ("Night Tint (캠프 밖)", Color) = (1,1,1,1)
        _CampTint  ("Camp Tint (캠프 안)",  Color) = (1,1,1,1)
        _CampCenter("Camp Center (xz)", Vector) = (-10.5, 0, 54.5, 0)
        _CampR0    ("Camp Inner Radius", Float) = 8
        _CampR1    ("Camp Outer Radius", Float) = 16
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf PyriteNight fullforwardshadows exclude_path:deferred exclude_path:prepass
        #pragma multi_compile_instancing
        #pragma target 3.0

        #include "UnityPBSLighting.cginc"
        #include "PyriteNight.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _MatCap;
        fixed4 _Color;
        half _Metallic, _Glossiness, _BumpScale, _MatCapNormal, _StriationTile;
        half4 _EmissionColor;
        half _MatCapStrength, _MatCapBoost;
        half4 _MatCapTint;
        half4 _RimColor, _GlintColor, _GlintDir;
        half _RimPower, _GlintSharp, _SparkleStrength, _SparkleScale;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldNormal;
            float3 worldPos;
            INTERNAL_DATA
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half4 nt = tex2D(_BumpMap, IN.uv_MainTex * _StriationTile);

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Normal = UnpackScaleNormal(nt, _BumpScale);

            // MatCap — 뷰 공간 법선으로 샘플
            float3 nM = UnpackScaleNormal(nt, _MatCapNormal);
            float3 wn = normalize(WorldNormalVector(IN, nM));
            float3 vn = normalize(mul((float3x3)UNITY_MATRIX_V, wn));
            half3 mc = tex2D(_MatCap, vn.xy * 0.49 + 0.5).rgb;

            o.Emission = _EmissionColor.rgb + mc * c.rgb * _MatCapTint.rgb * (_MatCapStrength * _MatCapBoost);

            // 밤 보강 — 월드 공간
            float3 V = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float3 wnL = normalize(WorldNormalVector(IN, UnpackScaleNormal(nt, _BumpScale)));
            half ndv = saturate(dot(wnL, V));
            o.Emission += _RimColor.rgb * c.rgb * pow(1.0 - ndv, _RimPower);
            float3 R = reflect(-V, wn);
            float3 gd = normalize(_GlintDir.xyz + 1e-4);
            o.Emission += _GlintColor.rgb * c.rgb * pow(saturate(dot(R, gd)), _GlintSharp);
            if (_SparkleStrength > 0)
            {
                float3 cell = floor(IN.worldPos * _SparkleScale);
                float3 h = frac(sin(float3(dot(cell, float3(127.1, 311.7, 74.7)), dot(cell, float3(269.5, 183.3, 246.1)), dot(cell, float3(113.5, 271.9, 124.6)))) * 43758.5453);
                float3 fn = normalize(wn + (h - 0.5) * 1.2);           // 셀마다 기울어진 미세 결정면
                float3 Rs = reflect(-V, fn);
                half sp = pow(saturate(dot(Rs, gd)), 400) + pow(saturate(dot(Rs, normalize(float3(h.y - 0.5, 0.6, h.z - 0.5)))), 900) * 0.5;
                o.Emission += sp * _SparkleStrength * c.rgb * _GlintColor.rgb;
            }
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Standard"
}
