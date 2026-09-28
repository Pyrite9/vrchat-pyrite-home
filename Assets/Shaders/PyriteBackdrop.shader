// Pyrite/Backdrop — 침실 둘레 큰 구(안쪽 면)에 월드에서 찍은 등장방형 파노라마를 무한 원경으로 그린다. _LiveOn 이면 캠프 실시간 카메라 화면을 우선
//  방향 = 카메라 → 픽셀 (시차 없음). _Yaw 로 침실 +Z(창) ↔ 월드 방향을 맞춘다
Shader "Pyrite/Backdrop"
{
    Properties
    {
        _Pano ("Equirect panorama", 2D) = "black" {}
        _Yaw ("Yaw (deg)", Float) = 180
        _Exposure ("Exposure", Float) = 1
        _FlipU ("Flip U", Float) = 0
        _OffU ("U offset", Float) = 0
        _Live ("Live camera RT", 2D) = "black" {}
        _LiveOn ("Live on", Float) = 0
        _LiveFwd ("Live cam forward (world)", Vector) = (0, 0, 1, 0)
        _LiveRight ("Live cam right (world)", Vector) = (1, 0, 0, 0)
        _LiveUp ("Live cam up (world)", Vector) = (0, 1, 0, 0)
        _LiveTan ("Live cam tan half fov (h, v)", Vector) = (1, 1, 0, 0)
        _LiveEdge ("Live edge fade", Float) = 0.06
        _Dim ("Sleep dim", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" }
        Cull Front
        ZWrite On
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _Pano;
            float _Yaw;
            float _Exposure;
            float _FlipU;
            float _OffU;
            sampler2D _Live;
            float _LiveOn;
            float4 _LiveFwd;
            float4 _LiveRight;
            float4 _LiveUp;
            float4 _LiveTan;
            float _LiveEdge;
            float _Dim;
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = mul(unity_ObjectToWorld, v.vertex).xyz - _WorldSpaceCameraPos;
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float a = radians(_Yaw);
                float s = sin(a);
                float c = cos(a);
                d = float3(c * d.x + s * d.z, d.y, -s * d.x + c * d.z);
                float u = atan2(d.x, d.z) / (2 * UNITY_PI) + 0.5;
                if (_FlipU > 0.5) u = 1 - u;
                u = frac(u + _OffU);
                float v = asin(clamp(d.y, -1, 1)) / UNITY_PI + 0.5;
                fixed4 col = tex2Dlod(_Pano, float4(u, v, 0, 0));
                col.rgb *= _Exposure;
                // 실시간 창 카메라(캠프 텐트 앞): 방향 d(월드)를 카메라 화면에 투영, 화면 밖·가장자리는 파노라마로
                if (_LiveOn > 0.5)
                {
                    float z = dot(d, _LiveFwd.xyz);
                    if (z > 0.01)
                    {
                        float2 q = float2(dot(d, _LiveRight.xyz), dot(d, _LiveUp.xyz)) / z / _LiveTan.xy;
                        float2 e = saturate((1 - abs(q)) / _LiveEdge);
                        float k = e.x * e.y;
                        if (k > 0)
                        {
                            fixed4 live = tex2Dlod(_Live, float4(q * 0.5 + 0.5, 0, 0));
                            col.rgb = lerp(col.rgb, live.rgb, k);
                        }
                    }
                }
                col.rgb *= _Dim;
                return col;
            }
            ENDCG
        }
    }
}
