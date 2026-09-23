// 설정 패널 바탕 — 흑요석 유리 + 금선 테두리 (둥근 사각형 SDF, 자체 발광)
//  _Aspect = 가로/세로. uv 0..1 → 패널 좌표(가로 _Aspect)
Shader "Pyrite/HoloPanel"
{
    Properties
    {
        _Aspect ("Aspect (w/h)", Float) = 1.7778
        _Glass ("Glass Color", Color) = (0.02, 0.025, 0.032, 0.78)
        [HDR] _Border ("Border Color", Color) = (1.9, 1.45, 0.62, 1)
        _BorderW ("Border Width (h units)", Float) = 0.012
        _Radius ("Corner Radius (h units)", Float) = 0.05
        _Inner ("Inner Line Offset", Float) = 0.035
        _Scan ("Scanline", Float) = 0.04
        _Fade ("Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half _Aspect, _BorderW, _Radius, _Inner, _Scan, _Fade;
            half4 _Glass, _Border;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert (appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o; }
            float sdRound(float2 p, float2 b, float r) { float2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
            half4 frag (v2f i) : SV_Target
            {
                float2 p = (i.uv - 0.5) * float2(_Aspect, 1.0);
                float2 hb = float2(_Aspect, 1.0) * 0.5;
                float d = sdRound(p, hb - 0.005, _Radius);                  // 바깥
                float aa = fwidth(d) * 1.2;
                half inside = 1.0 - smoothstep(-aa, aa, d);
                half outer = (1.0 - smoothstep(0.0, aa, abs(d + _BorderW * 0.5) - _BorderW * 0.5)) * inside;
                float d2 = sdRound(p, hb - 0.005 - _Inner, max(_Radius - _Inner, 0.005));
                half inner = (1.0 - smoothstep(0.0, aa, abs(d2) - 0.0015)) * 0.45;
                half glow = exp(-max(-d, 0.0) / 0.03) * 0.25 * inside;       // 테두리 안쪽 은은한 빛
                half scan = 1.0 + _Scan * sin(i.uv.y * 420.0 + _Time.y * 2.0);
                half top = lerp(1.0, 1.35, smoothstep(0.2, 1.0, i.uv.y));   // 위쪽 살짝 밝게
                half3 col = _Glass.rgb * top * scan + _Border.rgb * (outer + inner + glow);
                half a = saturate(_Glass.a * inside + outer + inner + glow) * _Fade;
                return half4(col, a);
            }
            ENDCG
        }
    }
}
