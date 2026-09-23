// 깊이만 쓰는 가림막 — 배 안(뱃전 높이의 평면)에 두면, 뒤에 그려지는 반투명 물·물 반사가 배 안에서 안 보인다
//  2999(Transparent-1): 불투명·컷아웃(아바타 대부분 2000~2450)이 다 그려진 뒤, 물(3000~)보다 먼저 깊이만 쓴다
//  2010 이던 때는 컷아웃 셰이더(2450) 아바타의 발이 배 안에서 잘렸다
Shader "Pyrite/DepthMask"
{
    SubShader
    {
        Tags { "Queue" = "Transparent-1" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
        ColorMask 0
        ZWrite On
        ZTest LEqual
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            // VR 단일 패스 인스턴싱: 스테레오 매크로가 없으면 한쪽 눈에만 가림막이 생긴다
            struct appdata { float4 vertex : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v) { v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.pos = UnityObjectToClipPos(v.vertex); return o; }
            fixed4 frag (v2f i) : SV_Target { return 0; }
            ENDCG
        }
    }
}
