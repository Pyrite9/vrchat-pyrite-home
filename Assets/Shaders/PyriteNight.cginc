// 밤 어둠 — "구운 조명만" 어둡게 한다
//
//  라이트맵·라이트 프로브는 노을 한 벌뿐이고 런타임 교체가 막혀 있다.
//  그래서 셰이더에서 간접광(gi.indirect = 라이트맵 + 프로브 + 반사)에만 색을 곱한다.
//  실시간 광원(모닥불·랜턴 포인트 라이트)은 ForwardAdd 패스에서 따로 더해지므로 영향을 받지 않는다.
//  → 캠프 불빛은 그대로, 캠프 밖의 구운 노을빛만 꺼진다.
//
//  캠프 반경 안쪽은 _CampTint, 바깥은 _NightTint. 사이는 smoothstep 으로 부드럽게.
//
//  [E1] 밤 하늘빛 — 라이트맵은 "간접광만" 들어 있다(노을 직사광은 실시간). 밤에 색조를 곱하면
//       거의 0 이 되어 물가 모래가 새까맣게 뜬다. 밤 하늘이 주는 빛을 반구 조명으로 더한다.
//       _NightAmbSky(위를 보는 면) / _NightAmbGround(아래를 보는 면). 노을·새벽은 0.
//  [A]  _SpecNoTint = 1 이면 반사(specular)에는 색조를 곱하지 않는다.
//       반사 프로브는 이미 프리셋별 큐브맵으로 교체되므로(T) 여기에 또 곱하면 두 번 어두워진다.
//       금속(황철석)은 반사가 전부라 1 로 둔다. 선언 안 한 셰이더는 0 → 기존 동작.
//  [Z13] 밤 하늘빛도 AO(s.Occlusion)를 받는다 — 판자 틈·결 골이 밤에 드러나게. AO 맵이 없는 셰이더는 1 이라 그대로.
//       _NightAmbTilt > 0 이면 하늘빛 반구를 달 쪽(_NightAmbDir, 수평 방향)으로 기울인다 → 노멀맵 음영이 밤에도 보인다.
//       선언 안 한/0 인 머티리얼(지형·황철석)은 기존과 같다.
#ifndef PYRITE_NIGHT_INCLUDED
#define PYRITE_NIGHT_INCLUDED

#include "UnityPBSLighting.cginc"

half4  _NightTint;
half4  _CampTint;
float4 _CampCenter;
float  _CampR0;
float  _CampR1;
half4  _NightAmbSky;
half4  _NightAmbGround;
half   _SpecNoTint;
half   _NightAmbTilt;
float4 _NightAmbDir;

inline half3 PyriteNightTint(float3 wp)
{
    float d = distance(wp.xz, _CampCenter.xz);
    float k = 1.0 - smoothstep(_CampR0, _CampR1, d);
    return lerp(_NightTint.rgb, _CampTint.rgb, k);
}

inline half4 LightingPyriteNight(SurfaceOutputStandard s, half3 viewDir, UnityGI gi)
{
    return LightingStandard(s, viewDir, gi);
}

inline void LightingPyriteNight_GI(SurfaceOutputStandard s, UnityGIInput data, inout UnityGI gi)
{
    LightingStandard_GI(s, data, gi);
    half3 t = PyriteNightTint(data.worldPos);
    float3 skyN = normalize(float3(0, 1, 0) + _NightAmbDir.xyz * _NightAmbTilt);
    half  up = saturate(dot(s.Normal, skyN) * 0.5 + 0.5);
    // 기울였을 때 위를 보는 면이 어두워지지 않게, 평평한 면 기준으로 다시 맞춘다
    half  up0 = saturate(skyN.y * 0.5 + 0.5);
    up = saturate(up + (1.0 - up0));
    gi.indirect.diffuse  = gi.indirect.diffuse * t + lerp(_NightAmbGround.rgb, _NightAmbSky.rgb, up) * s.Occlusion;
    gi.indirect.specular *= lerp(t, half3(1, 1, 1), saturate(_SpecNoTint));
}

#endif
