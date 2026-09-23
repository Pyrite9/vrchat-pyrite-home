// 깊이만 쓰는 가림막 — 배 안(뱃전 높이의 평면)에 두면, 뒤에 그려지는 반투명 물·물 반사가 배 안에서 안 보인다
//  배(불투명, 2000)가 먼저 그려진 뒤(2010) 깊이만 쓰므로 배 안쪽 모습은 그대로 남는다
Shader "Pyrite/DepthMask"
{
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" "IgnoreProjector" = "True" }
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
            float4 vert (float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            fixed4 frag () : SV_Target { return 0; }
            ENDCG
        }
    }
}
