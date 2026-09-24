// 부드러운 반투명 파티클 — 호수 물안개, 물수제비 물결 고리. 전체 알파(_Alpha)는 스크립트가 시간대로 조절
//  가까우면(카메라 _NearFade m 안) 옅어진다 → 안개 판 속을 걸어도 판 모서리가 얼굴에 안 걸림
Shader "Pyrite/SoftAlpha"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _NearFade ("Near fade (m)", Float) = 0
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
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; half4 _Color; half _Alpha; float _NearFade;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float dist : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert (appdata v)
            {
                v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f, o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color;
                o.dist = distance(mul(unity_ObjectToWorld, v.vertex).xyz, _WorldSpaceCameraPos);
                return o;
            }
            half4 frag (v2f i) : SV_Target
            {
                half4 t = tex2D(_MainTex, i.uv);
                half a = t.a * i.color.a * _Color.a * _Alpha;
                if (_NearFade > 0) a *= saturate((i.dist - _NearFade * 0.3) / (_NearFade * 0.7));
                return half4(t.rgb * i.color.rgb * _Color.rgb, a);
            }
            ENDCG
        }
    }
}
