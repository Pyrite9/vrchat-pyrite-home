// Pyrite/TentCanvas — 텐트 천(안쪽 면). 오브젝트 공간 둥근 사각형 창 구멍을 clip 으로 뚫는다 (TPU 창 자리)
Shader "Pyrite/TentCanvas"
{
    Properties
    {
        _Color ("Color", Color) = (0.62, 0.47, 0.31, 1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.12
        _Weave ("Weave strength", Range(0,0.3)) = 0.08
        _WinC ("Window center (x,y) object", Vector) = (0, 1.05, 0, 0)
        _WinH ("Window half size (x,y)", Vector) = (0.9, 0.6, 0, 0)
        _WinR ("Window corner radius", Float) = 0.22
        _WinSide ("Window side z sign (0 = none)", Float) = 1
        _WinMode ("Window shape (0 round rect, 1 pole arch)", Float) = 0
        _WinE ("Arch ellipse half axes (x,y)", Vector) = (2.78, 3.05, 0, 0)
        _WinY ("Arch bottom/top y", Vector) = (0.15, 2.7, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Back
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.0
        fixed4 _Color;
        half _Glossiness;
        half _Weave;
        float4 _WinC;
        float4 _WinH;
        float _WinR;
        float _WinSide;
        float _WinMode;
        float4 _WinE;
        float4 _WinY;
        struct Input { float3 opos; };
        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.opos = v.vertex.xyz;
        }
        float sdRR (float2 p, float2 h, float r)
        {
            float2 q = abs(p) - h + r;
            return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
        }
        float rmax (float a, float b, float r)
        {
            float2 q = float2(a + r, b + r);
            return length(max(q, 0)) + min(max(q.x, q.y), 0) - r;
        }
        // 폴 따라 창: 앞면 xy 투영에서 타원(폴 − 여유) ∩ 아래·위 자름, 모서리 둥글게 (PyriteBedroomV3.WinSd 와 같은 식)
        float sdArch (float2 p)
        {
            float2 e = _WinE.xy;
            float k0 = length(p / e);
            float k1 = length(p / (e * e));
            float dE = k0 * (k0 - 1) / max(k1, 1e-4);
            return rmax(rmax(dE, _WinY.x - p.y, _WinR), p.y - _WinY.y, _WinR);
        }
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 p = IN.opos;
            if (_WinSide != 0 && p.z * _WinSide > 0)
                clip(_WinMode > 0.5 ? sdArch(p.xy) : sdRR(p.xy - _WinC.xy, _WinH.xy, _WinR));
            float w = sin(p.x * 520) * sin(p.y * 520) + sin(p.z * 520) * sin(p.y * 520);
            float n = frac(sin(dot(floor(p * 40), float3(12.9898, 78.233, 37.719))) * 43758.5453);
            o.Albedo = _Color.rgb * (1 + _Weave * 0.5 * w + (n - 0.5) * _Weave * 0.6);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
