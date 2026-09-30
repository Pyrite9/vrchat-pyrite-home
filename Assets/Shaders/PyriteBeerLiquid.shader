// Pyrite/BeerLiquid — 병 속 맥주 (2026-09-30)
//  수면 = 월드 수평면: 물체 로컬 (0, _Level, 0) 을 지나는 수평면 위는 잘라낸다 → 병을 기울여도 수면은 수평
//  Cull Off: 잘린 윗부분으로 보이는 안쪽 면(뒷면)이 수면처럼 보이게 _TopColor + 발광
//  표면 셰이더라 VR 단일 패스 인스턴싱 매크로는 자동 (8절 함정: 직접 쓴 vert/frag 는 UNITY_VERTEX_OUTPUT_STEREO 필요)
Shader "Pyrite/BeerLiquid"
{
    Properties
    {
        _Color ("Beer", Color) = (0.46, 0.23, 0.035, 1)
        _TopColor ("Surface", Color) = (0.78, 0.58, 0.24, 1)
        _Level ("Level (object y)", Float) = 0.192
        _Glossiness ("Smoothness", Range(0,1)) = 0.85
        _Emission ("Glow", Range(0,2)) = 0.08
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
        float _Level;
        half _Glossiness, _Emission;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = mul(unity_ObjectToWorld, float4(0, _Level, 0, 1)).xyz;
            clip(p.y - IN.worldPos.y);
            bool back = IN.facing < 0;
            fixed3 c = back ? _TopColor.rgb : _Color.rgb;
            o.Albedo = c;
            o.Smoothness = back ? 0.2 : _Glossiness;
            o.Metallic = 0;
            o.Emission = c * _Emission;   // 갈색 유리 너머로도 호박색이 조금 읽히게 (1차 0.35 는 밤에 빛남)
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
