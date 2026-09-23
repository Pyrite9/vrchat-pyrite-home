// Tools ▸ Pyrite ▸ Z2. Sun Elevation Test (preview only)
//  노을·새벽 태양 고도 후보별로 캠프/물가 시점 렌더 → Assets/_preview/sun/
//  스카이 머티리얼·방향광 각도는 끝나면 원래대로 되돌린다 (씬·에셋 변경 없음)
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyriteSunTest
{
    const string OUT = "Assets/_preview/sun/";
    static readonly float[] DUSK = { 0.10f, 0.16f, 0.21f, 0.26f };
    static readonly float[] DAWN = { 0.045f, 0.12f, 0.17f };

    [MenuItem("Tools/Pyrite/Z2. Sun Elevation Test (preview)", false, 6)]
    public static void Run()
    {
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod == null || tod.sun == null || tod.skybox == null) { Debug.LogError("[Z2] ToD/sun/skybox 없음"); return; }
        Directory.CreateDirectory(OUT);
        var rot0 = tod.sun.transform.rotation;
        float e0 = tod.skybox[0].GetFloat("_SunElevation"), e2 = tod.skybox[2].GetFloat("_SunElevation");
        try
        {
            Shot(tod, 0, DUSK);
            Shot(tod, 2, DAWN);
        }
        finally
        {
            tod.skybox[0].SetFloat("_SunElevation", e0);
            tod.skybox[2].SetFloat("_SunElevation", e2);
            tod.sun.transform.rotation = rot0;
            tod.index = 0; tod.Apply();
        }
        AssetDatabase.Refresh();
        Debug.Log("[Z2] 저장 완료 — " + OUT);
    }

    static void Shot(PyriteTimeOfDay tod, int preset, float[] elevs)
    {
        var m = tod.skybox[preset];
        float az = m.GetFloat("_SunAzimuth");
        foreach (var e in elevs)
        {
            m.SetFloat("_SunElevation", e);
            float deg = Mathf.Asin(e) * Mathf.Rad2Deg;
            // 방향광은 태양 반대 방향으로 비춘다 (현재 씬: x=5.74, y=4 ↔ 스카이 az 184)
            tod.sun.transform.rotation = Quaternion.Euler(deg, az - 180f, 0f);
            PyriteViews.CaptureSet("camp_lake shore", new[] { preset });
            foreach (var v in new[] { "camp_lake", "shore" })
                File.Copy(PyriteViews.OUT + v + "_" + preset + ".png", OUT + v + "_p" + preset + "_e" + e.ToString("F3") + ".png", true);
        }
    }
}
#endif
