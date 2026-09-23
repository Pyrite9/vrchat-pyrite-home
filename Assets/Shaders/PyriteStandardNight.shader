// Standard 의 부분집합 + 구운 조명 색조(PyriteNight.cginc)
//  부두처럼 라이트맵을 받는 정적 오브젝트용. 표면 셰이더라 Meta 패스가 자동 생성돼 라이트맵 베이크에도 쓸 수 있다.
Shader "Pyrite/StandardNight"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        [Gamma] _Metallic ("Metallic", Range(0,1)) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1
        _OcclusionMap ("Occlusion (G)", 2D) = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0,1)) = 1
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)

        _NightTint ("Night Tint (캠프 밖)", Color) = (1,1,1,1)
        _CampTint  ("Camp Tint (캠프 안)",  Color) = (1,1,1,1)
        _CampCenter("Camp Center (xz)", Vector) = (-10.5, 0, 54.5, 0)
        _CampR0    ("Camp Inner Radius", Float) = 8
        _CampR1    ("Camp Outer Radius", Float) = 16
        _NightAmbTilt ("Night Sky Light Tilt (0 = 기존)", Range(0, 2)) = 0
        _NightAmbDir  ("Night Sky Light Dir (수평, 달 쪽)", Vector) = (-0.423, 0, -0.906, 0)
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
        sampler2D _OcclusionMap;
        half _OcclusionStrength;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;
        half _BumpScale;
        half4 _EmissionColor;

        struct Input { float2 uv_MainTex; };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            o.Occlusion = lerp(1, tex2D(_OcclusionMap, IN.uv_MainTex).g, _OcclusionStrength);   // 간접광(라이트맵·프로브)에만 — 밤 부두는 거의 간접광이라 여기서 입체감이 난다
            o.Emission = _EmissionColor.rgb;
            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Standard"
}
