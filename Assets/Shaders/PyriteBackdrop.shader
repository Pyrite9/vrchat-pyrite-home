// Pyrite/Backdrop — 침실 둘레 큰 구(안쪽 면)에 월드에서 찍은 등장방형 파노라마를 무한 원경으로 그린다
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
                return col;
            }
            ENDCG
        }
    }
}
