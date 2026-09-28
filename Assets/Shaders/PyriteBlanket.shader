// Pyrite/Blanket — 체크무늬 울 담요. 누운 사람 뼈대 선분(_Seg, 월드)을 받아 정점을 들어 올린다(몸이 있는 곳이 불룩)
//  _Seg[2i] = (a.xyz, 반지름), _Seg[2i+1] = (b.xyz, 0), _SegCount 개. 정점 색 R = 들어올림 허용(윗면 1, 늘어진 가장자리 0), G = 떨어질 때 가장자리 흔들림
//  _Drop 0 → 1: 위 0.5~0.9 m 에서 내려앉음. PyriteBlanket(U#) 가 매 프레임 채운다
Shader "Pyrite/Blanket"
{
    Properties
    {
        _ColA ("Base", Color) = (0.40, 0.10, 0.09, 1)
        _ColB ("Band", Color) = (0.10, 0.18, 0.13, 1)
        _ColC ("Line", Color) = (0.86, 0.76, 0.52, 1)
        _Scale ("Plaid repeat (m)", Float) = 0.5
        _Drop ("Drop 0..1", Range(0,1)) = 1
        _Thick ("Thickness over body", Float) = 0.025
        _Skirt ("Skirt width", Float) = 0.14
        _SegCount ("Segment count", Float) = 0
        _Fuzz ("Fuzz", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma target 3.5
        fixed4 _ColA;
        fixed4 _ColB;
        fixed4 _ColC;
        float _Scale;
        float _Drop;
        float _Thick;
        float _Skirt;
        float _SegCount;
        float _Fuzz;
        float4 _Seg[96];
        struct Input { float2 pl; float vface : VFACE; };

        float BodyH (float3 p, float y0)
        {
            float h = y0;
            [loop] for (int i = 0; i < 48; i++)
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
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float m = v.color.r;
            if (m > 0.001 && _SegCount > 0.5)
            {
                float y0 = wp.y;
                float e = 0.03;
                float h = BodyH(wp, y0);
                float hx = BodyH(wp + float3(e, 0, 0), y0);
                float hz = BodyH(wp + float3(0, 0, e), y0);
                wp.y = lerp(y0, h, m);
                float3 nw = normalize(float3(-(hx - h) / e * m, 1, -(hz - h) / e * m));
                v.normal = normalize(mul((float3x3)unity_WorldToObject, nw));
            }
            wp.y += (1 - _Drop) * (0.5 + 0.4 * v.color.g);
            v.vertex = mul(unity_WorldToObject, float4(wp, 1));
            o.pl = v.texcoord.xy / _Scale;
        }

        float band (float x, float w) { return 1 - smoothstep(w * 0.8, w, abs(frac(x) - 0.5)); }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 u = IN.pl;
            float3 c = _ColA.rgb;
            c = lerp(c, _ColB.rgb, 0.6 * band(u.x, 0.17));
            c = lerp(c, _ColB.rgb, 0.6 * band(u.y, 0.17));
            c = lerp(c, _ColC.rgb, 0.5 * max(band(u.x + 0.5, 0.02), band(u.y + 0.5, 0.02)));
            float tw = frac((u.x + u.y) * 60);
            c *= 0.93 + 0.07 * step(0.5, tw);
            float n = frac(sin(dot(floor(u * 300), float2(12.9898, 78.233))) * 43758.5453);
            c *= 1 + (n - 0.5) * 0.12 * _Fuzz;
            o.Albedo = c;
            o.Smoothness = 0.05;
            o.Metallic = 0;
            o.Normal = float3(0, 0, IN.vface > 0 ? 1 : -1);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
