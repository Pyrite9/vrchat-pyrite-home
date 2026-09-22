// Nature/Terrain/Standard 와 동일 + 구운 조명 색조(PyriteNight.cginc)
//  · 레이어 4장까지는 이 패스 하나로 끝난다 (지형 레이어 = 4)
//  · basemapDistance 가 1000 이라 원거리 베이스맵 셰이더는 쓰이지 않는다
Shader "Pyrite/TerrainNight"
{
    Properties
    {
        [HideInInspector] _MainTex ("BaseMap (RGB)", 2D) = "white" {}
        [HideInInspector] _Color ("Main Color", Color) = (1,1,1,1)
        [HideInInspector] _TerrainHolesTexture("Holes Map (RGB)", 2D) = "white" {}

        _NightTint ("Night Tint (캠프 밖)", Color) = (1,1,1,1)
        _CampTint  ("Camp Tint (캠프 안)",  Color) = (1,1,1,1)
        _CampCenter("Camp Center (xz)", Vector) = (-10.5, 0, 54.5, 0)
        _CampR0    ("Camp Inner Radius", Float) = 8
        _CampR1    ("Camp Outer Radius", Float) = 16
    }

    SubShader
    {
        Tags { "Queue" = "Geometry-100" "RenderType" = "Opaque" "TerrainCompatible" = "True" }

        CGPROGRAM
        #pragma surface surf PyriteNight vertex:SplatmapVert finalcolor:SplatmapFinalColor addshadow fullforwardshadows exclude_path:deferred exclude_path:prepass
        #pragma instancing_options assumeuniformscaling nomatrices nolightprobe nolightmap forwardadd
        #pragma multi_compile_fog
        #pragma target 3.0

        #pragma multi_compile_local_fragment __ _ALPHATEST_ON
        #pragma multi_compile_local __ _NORMALMAP

        #include "UnityPBSLighting.cginc"

        #define TERRAIN_STANDARD_SHADER
        #define TERRAIN_INSTANCED_PERPIXEL_NORMAL
        #define TERRAIN_SURFACE_OUTPUT SurfaceOutputStandard
        #include "TerrainSplatmapCommon.cginc"
        #include "PyriteNight.cginc"

        half _Metallic0; half _Metallic1; half _Metallic2; half _Metallic3;
        half _Smoothness0; half _Smoothness1; half _Smoothness2; half _Smoothness3;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half4 splat_control;
            half weight;
            fixed4 mixedDiffuse;
            half4 defaultSmoothness = half4(_Smoothness0, _Smoothness1, _Smoothness2, _Smoothness3);
            SplatmapMix(IN, defaultSmoothness, splat_control, weight, mixedDiffuse, o.Normal);
            o.Albedo = mixedDiffuse.rgb;
            o.Alpha = weight;
            o.Smoothness = mixedDiffuse.a;
            o.Metallic = dot(splat_control, half4(_Metallic0, _Metallic1, _Metallic2, _Metallic3));
        }
        ENDCG

        UsePass "Hidden/Nature/Terrain/Utilities/PICKING"
        UsePass "Hidden/Nature/Terrain/Utilities/SELECTION"
    }

    Dependency "AddPassShader"    = "Hidden/TerrainEngine/Splatmap/Standard-AddPass"
    Dependency "BaseMapShader"    = "Hidden/TerrainEngine/Splatmap/Standard-Base"
    Dependency "BaseMapGenShader" = "Hidden/TerrainEngine/Splatmap/Standard-BaseGen"

    Fallback "Nature/Terrain/Diffuse"
}
