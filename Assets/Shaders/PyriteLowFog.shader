// 낮은 호수 안개 — 수면 위 0~0.7 m 에 겹친 수평 판 여러 장. 판마다 세계 XZ 노이즈를 다른 속도로 흘려 조각조각 끊긴 안개가 된다
//  · 정점 색 a = 층 무게(아래 짙고 위 옅음), r = 층 노이즈 오프셋 씨앗
//  · _Mask = 호수(물 위)만 1, 뭍 쪽으로 부드럽게 0 (에디터가 바닥 높이를 레이캐스트로 구워 둔다)
//  · _Alpha 는 PyriteMist 가 시간대로 조절. 카메라 _NearFade m 안은 옅게(몸 주변 판 모서리 방지)
Shader "Pyrite/LowFog"
{
    Properties
    {
        _Noise ("Noise (tileable)", 2D) = "white" {}
        _Mask ("Lake mask", 2D) = "white" {}
        _MaskRect ("Mask rect (minX, minZ, sizeX, sizeZ)", Vector) = (-60, -74, 120, 120)
        _Color ("Color", Color) = (0.6, 0.66, 0.76, 1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _Density ("Layer density", Range(0,1)) = 0.2
        _Scale ("Noise scale (m)", Float) = 38
        _Threshold ("Noise threshold", Range(0,1)) = 0.42
        _Contrast ("Noise contrast", Float) = 2.4
        _Wind ("Wind (xz m/s, 2nd octave xz)", Vector) = (0.12, 0.05, -0.05, 0.09)
        _NearFade ("Near fade (m)", Float) = 2.5
    }
    SubShader
    {
        Tags { "Queue" = "Transparent+50" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _Noise; sampler2D _Mask; float4 _MaskRect;
            half4 _Color; half _Alpha, _Density, _Threshold, _Contrast; float _Scale, _NearFade; float4 _Wind;
            struct appdata { float4 vertex : POSITION; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 wp : TEXCOORD0; half4 color : COLOR; UNITY_FOG_COORDS(1) UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.wp = mul(unity_ObjectToWorld, v.vertex).xyz; o.color = v.color;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                float t = _Time.y; float seed = i.color.r * 7.31;
                float2 uv = i.wp.xz / _Scale;
                half n1 = tex2D(_Noise, uv + seed + _Wind.xy * t / _Scale).r;
                half n2 = tex2D(_Noise, uv * 2.3 + seed * 1.7 + _Wind.zw * t / _Scale).r;
                half n = n1 * 0.65 + n2 * 0.35;
                half a = saturate((n - _Threshold) * _Contrast);
                half m = tex2D(_Mask, (i.wp.xz - _MaskRect.xy) / _MaskRect.zw).r;
                a *= m * i.color.a * _Density * _Color.a * _Alpha;
                float d = distance(i.wp, _WorldSpaceCameraPos);
                if (_NearFade > 0) a *= saturate((d - _NearFade * 0.3) / (_NearFade * 0.7));
                half4 c = half4(_Color.rgb, a);
                UNITY_APPLY_FOG(i.fogCoord, c);
                return c;
            }
            ENDCG
        }
    }
}
