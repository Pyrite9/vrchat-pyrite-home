// 밤 어둠 — "구운 조명만" 어둡게 한다
//
//  라이트맵·라이트 프로브는 노을 한 벌뿐이고 런타임 교체가 막혀 있다.
//  그래서 셰이더에서 간접광(gi.indirect = 라이트맵 + 프로브 + 반사)에만 색을 곱한다.
//  실시간 광원(모닥불·랜턴 포인트 라이트)은 ForwardAdd 패스에서 따로 더해지므로 영향을 받지 않는다.
//  → 캠프 불빛은 그대로, 캠프 밖의 구운 노을빛만 꺼진다.
//
//  캠프 반경 안쪽은 _CampTint, 바깥은 _NightTint. 사이는 smoothstep 으로 부드럽게.
#ifndef PYRITE_NIGHT_INCLUDED
#define PYRITE_NIGHT_INCLUDED

#include "UnityPBSLighting.cginc"

half4  _NightTint;
half4  _CampTint;
float4 _CampCenter;
float  _CampR0;
float  _CampR1;

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
    gi.indirect.diffuse  *= t;
    gi.indirect.specular *= t;
}

#endif
