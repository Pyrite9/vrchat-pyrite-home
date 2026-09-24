// 망원경 접안 화면 — 원 안에 RT, 가장자리 어둡게, 원 밖은 clip. 조명 영향 없음(렌즈 속 상)
Shader "Pyrite/ScopeEyepiece"
{
    Properties
    {
        _MainTex ("View RT", 2D) = "black" {}
        _Brightness ("Brightness", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Brightness;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata_base v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = v.texcoord.xy; return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                float2 d = i.uv - 0.5;
                float r = length(d) * 2.0;
                clip(1.0 - r);
                fixed3 c = tex2D(_MainTex, i.uv).rgb * _Brightness;
                c *= lerp(0.35, 1.0, 1.0 - smoothstep(0.55, 1.0, r));   // 렌즈 가장자리 어둡게
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
}
