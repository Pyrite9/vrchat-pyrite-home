// Pyrite/BeerLiquid — 병 속 맥주 (2026-09-30)
//  수면 = 월드 수평면. 위는 잘라낸다 → 병을 기울여도 수면은 수평
//  Cull Off: 잘린 윗부분으로 보이는 안쪽 면(뒷면)이 수면처럼 보이게 _TopColor + 발광
//  23:5x 관리자 인게임: 병을 뒤집으면 액체가 반대로 차오름 → 원인: 수면을 "축 위 (0, _Level, 0) 을 지나는 면"으로 두면
//   뒤집혔을 때 아래쪽 = 빈 목 쪽이 남는다(가득 병 180° = 목 1.5 cm 만, 한 모금 병 180° = 거의 가득)
//   → 부피 보존 표 _LevelMap (Z53g 가 만듦): u = 기울기(0..180°), v = 똑바로 섰을 때 수면 높이 / _LevelMax → 수면의 월드 높이(원점 기준, m)
//   _MapOn 0 이면 예전 방식 (Z53h 되돌림)
//  표면 셰이더라 VR 단일 패스 인스턴싱 매크로는 자동 (8절 함정: 직접 쓴 vert/frag 는 UNITY_VERTEX_OUTPUT_STEREO 필요)
Shader "Pyrite/BeerLiquid"
{
    Properties
    {
        _Color ("Beer", Color) = (0.72, 0.40, 0.07, 1)
        _TopColor ("Surface", Color) = (0.95, 0.82, 0.52, 1)
        _Level ("Level (object y, upright)", Float) = 0.192
        _Glossiness ("Smoothness", Range(0,1)) = 0.75
        _Emission ("Glow", Range(0,2)) = 0.35
        [NoScaleOffset] _LevelMap ("Level Map (tilt x level → plane height m)", 2D) = "black" {}
        _LevelMax ("Level Map max level", Float) = 0.2072
        _MapN ("Level Map size", Float) = 64
        [Toggle] _MapOn ("Use Level Map", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma multi_compile_instancing

        struct Input { float3 worldPos; float facing : VFACE; };
        fixed4 _Color, _TopColor;
        float _Level, _LevelMax, _MapN, _MapOn;
        half _Glossiness, _Emission;
        sampler2D _LevelMap;

        float PlaneY()
        {
            float3 up = float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21);
            float s = max(length(up), 1e-5);
            float oldY = mul(unity_ObjectToWorld, float4(0, _Level, 0, 1)).y;
            float tilt = acos(clamp(up.y / s, -1, 1)) / UNITY_PI;          // 0 = 똑바로, 1 = 거꾸로
            float2 uv = (float2(tilt, saturate(_Level / _LevelMax)) * (_MapN - 1) + 0.5) / _MapN;
            float h = tex2Dlod(_LevelMap, float4(uv, 0, 0)).r;
            float newY = unity_ObjectToWorld._m13 + h * s;
            return lerp(oldY, newY, _MapOn);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            clip(PlaneY() - IN.worldPos.y);
            bool back = IN.facing < 0;
            fixed3 c = back ? _TopColor.rgb : _Color.rgb;
            o.Albedo = c;
            o.Smoothness = back ? 0.2 : _Glossiness;
            o.Metallic = 0;
            o.Emission = c * (back ? _Emission * 1.6 : _Emission);   // 갈색 유리 너머로도 호박색이 읽히게
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
