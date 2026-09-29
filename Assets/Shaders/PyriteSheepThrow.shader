// Pyrite/SheepThrow — 빈백 양털. Pyrite/Blanket(침대 이불)과 같은 들어올림: 앉은 사람 다리 뼈 선분(_Seg, 월드)으로 정점을 위로 올린다
//  _Seg[2i] = (a.xyz, 반지름), _Seg[2i+1] = (b.xyz, 0), _SegCount 개. 정점 색 R = 들어올림 허용 (1 = 전부)
//  표면: 알파 컷아웃 텍스처(양털 윤곽) + Standard. 앞뒷면 (Cull Off, 뒷면 법선 뒤집음)
Shader "Pyrite/SheepThrow"
{
    Properties
    {
        _Color ("Color", Color) = (0.93, 0.89, 0.81, 1)
        _MainTex ("Albedo (A = outline)", 2D) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.04
        _Thick ("Thickness over body", Float) = 0.03
        _Skirt ("Skirt width", Float) = 0.14
        _SegCount ("Segment count", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows alphatest:_Cutoff
        #pragma target 3.5
        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        float _Thick;
        float _Skirt;
        float _SegCount;
        float4 _Seg[32];
        struct Input { float2 uv_MainTex; float vface : VFACE; };

        float BodyH (float3 p, float y0)
        {
            float h = y0;
            [loop] for (int i = 0; i < 16; i++)
            {
                if (i >= (int)_SegCount) break;
                float4 a = _Seg[i * 2];
                float4 b = _Seg[i * 2 + 1];
                float r = a.w;
                float2 ab = b.xz - a.xz;
                float t = saturate(dot(p.xz - a.xz, ab) / max(dot(ab, ab), 1e-5));
                float d = length(p.xz - (a.xz + ab * t));
                float cy = lerp(a.y, b.y, t);
                float top = cy + r + _Thick;
                float s = 1 - smoothstep(r * 0.55, r + _Skirt, d);
                h = max(h, lerp(y0, max(top, y0), s));
            }
            return h;
        }

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            float m = v.color.r;
            if (m > 0.001 && _SegCount > 0.5)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float y0 = wp.y;
                float e = 0.02;
                float h = BodyH(wp, y0);
                float hx = BodyH(wp + float3(e, 0, 0), y0 + 0) ;
                float hz = BodyH(wp + float3(0, 0, e), y0);
                float lift = (h - y0) * m;
                if (lift > 0.0005)
                {
                    wp.y += lift;
                    float3 nw = normalize(float3(-(hx - h) / e * m, 1, -(hz - h) / e * m));
                    float3 n0 = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                    float k = saturate(lift / 0.03);                 // 조금 들린 곳은 원래 법선과 섞음
                    v.normal = normalize(mul((float3x3)unity_WorldToObject, normalize(lerp(n0, nw, k))));
                    v.vertex = mul(unity_WorldToObject, float4(wp, 1));
                }
            }
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Normal = float3(0, 0, IN.vface > 0 ? 1 : -1);
        }
        ENDCG
    }
    FallBack "Legacy Shaders/Transparent/Cutout/Diffuse"
}
