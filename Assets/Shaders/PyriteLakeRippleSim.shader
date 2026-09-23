// Pyrite/LakeRippleSim — 호수 파문 시뮬레이션 (CustomRenderTexture, 이중 버퍼)
//  R = 지금 높이, G = 한 스텝 전 높이. 2D 파동 방정식(4 이웃 라플라시안) + 감쇠.
//   h' = (2h − h_prev + k·∇²h) · damp      k ≤ 0.5 에서 안정. 파속 ≈ √k 텍셀/스텝
//  범위 = 수심 텍스처와 같다: x −64..64, z −78..50 (128 m, 1024² → 12.5 cm/텍셀)
//  땅(수심 < 0.15 m)에서는 0 으로 눌러 물가에서 멈추게.
//  _Drops[i] = (월드 x, 월드 z, 세기/스텝, 반경 m) — PyriteLakeRipple(Udon)이 매 프레임 넣는다.
Shader "Pyrite/LakeRippleSim"
{
    Properties
    {
        [NoScaleOffset] _DepthTex("Lake Depth (R = depth / 4 m)", 2D) = "black" {}
        _Speed("Wave k (≤0.5)", Range(0, 0.45)) = 0.05
        _Damp("Damping / step", Range(0.9, 1)) = 0.985
    }
    SubShader
    {
        Lighting Off
        Blend One Zero
        Pass
        {
            Name "Update"
            CGPROGRAM
            #include "UnityCustomRenderTexture.cginc"
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #pragma target 3.0

            sampler2D _DepthTex;
            float _Speed, _Damp;
            float4 _Drops[8];
            float _DropCount;
            static const float4 RECT = float4(-64, -78, 1.0 / 128, 1.0 / 128);

            float4 frag(v2f_customrendertexture i) : SV_Target
            {
                float2 uv = i.globalTexcoord.xy;
                float2 d = float2(1.0 / _CustomRenderTextureWidth, 1.0 / _CustomRenderTextureHeight);
                float4 c = tex2D(_SelfTexture2D, uv);
                float l = tex2D(_SelfTexture2D, uv - float2(d.x, 0)).r;
                float r = tex2D(_SelfTexture2D, uv + float2(d.x, 0)).r;
                float b = tex2D(_SelfTexture2D, uv - float2(0, d.y)).r;
                float t = tex2D(_SelfTexture2D, uv + float2(0, d.y)).r;
                float h = c.r, p = c.g;
                float n = (2.0 * h - p + _Speed * (l + r + b + t - 4.0 * h)) * _Damp;

                float2 w = uv / RECT.zw + RECT.xy;                 // 월드 xz
                [unroll] for (int k = 0; k < 8; k++)
                {
                    if (k < _DropCount)
                    {
                        float2 dd = w - _Drops[k].xy;
                        float rr = max(_Drops[k].w, 0.05);
                        n -= _Drops[k].z * exp(-dot(dd, dd) / (rr * rr));
                    }
                }
                float depth = tex2D(_DepthTex, uv).r * 4.0;
                n *= saturate(depth / 0.15);
                return float4(n, h, 0, 0);
            }
            ENDCG
        }
    }
}
