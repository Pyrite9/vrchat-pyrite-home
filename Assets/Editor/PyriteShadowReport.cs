// Tools ▸ Pyrite ▸ Z5. Shadow Report (읽기 전용 + 캠프 근접 렌더)
//  그림자 경로 진단: 품질 설정 / 태양 bakingOutput / 캠프 소품 정적·라이트맵·그림자 / 셰이더
//  → Logs/pyrite_shadow.txt, Assets/_preview/shadow_camp_<i>.png
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteShadowReport
{
    [MenuItem("Tools/Pyrite/Z5. Shadow Report", false, 9)]
    public static void Run()
    {
        var sb = new StringBuilder();
        int q0 = QualitySettings.GetQualityLevel();
        sb.AppendLine("quality level " + QualitySettings.GetQualityLevel() + " " + QualitySettings.names[QualitySettings.GetQualityLevel()]);
        for (int q = 0; q < QualitySettings.names.Length; q++)
        {
            QualitySettings.SetQualityLevel(q, false);
            sb.AppendLine("  [" + q + "] " + QualitySettings.names[q] + " shadows=" + QualitySettings.shadows + " dist=" + QualitySettings.shadowDistance
                + " mask=" + QualitySettings.shadowmaskMode + " res=" + QualitySettings.shadowResolution + " cascades=" + QualitySettings.shadowCascades
                + " pixelLights=" + QualitySettings.pixelLightCount);
        }
        QualitySettings.SetQualityLevel(q0, false);
        var ls = Lightmapping.lightingSettings;
        sb.AppendLine("lighting mixed=" + ls.mixedBakeMode + " bakedGI=" + ls.bakedGI + " realtimeGI=" + ls.realtimeGI + " dir=" + ls.directionalityMode);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var sun = tod != null ? tod.sun : Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            var bo = sun.bakingOutput;
            sb.AppendLine("sun " + sun.name + " enabled=" + sun.enabled + " active=" + sun.gameObject.activeInHierarchy + " shadows=" + sun.shadows + " strength=" + sun.shadowStrength
                + " bias=" + sun.shadowBias + " nbias=" + sun.shadowNormalBias + " near=" + sun.shadowNearPlane + " mode=" + sun.lightmapBakeType + " render=" + sun.renderMode
                + " cull=" + sun.cullingMask + " intensity=" + sun.intensity + " euler=" + sun.transform.eulerAngles.ToString("F2"));
            sb.AppendLine("  bakingOutput isBaked=" + bo.isBaked + " type=" + bo.lightmapBakeType + " mixed=" + bo.mixedLightingMode + " maskCh=" + bo.occlusionMaskChannel + " probeCh=" + bo.probeOcclusionLightIndex);
        }
        foreach (var l in Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional && l != sun))
            sb.AppendLine("other directional " + l.name + " enabled=" + l.enabled + " shadows=" + l.shadows);
        var cam = Camera.main;
        if (cam != null) sb.AppendLine("Main Camera far=" + cam.farClipPlane + " near=" + cam.nearClipPlane + " path=" + cam.actualRenderingPath);

        var camp = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Camp");
        if (camp != null)
            foreach (var r in camp.GetComponentsInChildren<MeshRenderer>(true).Take(20))
                sb.AppendLine("camp " + r.name + " static=" + GameObjectUtility.GetStaticEditorFlags(r.gameObject) + " cast=" + r.shadowCastingMode + " recv=" + r.receiveShadows
                    + " lm=" + r.lightmapIndex + " gi=" + r.receiveGI + " shader=" + (r.sharedMaterial ? r.sharedMaterial.shader.name : "-"));
        var t = Terrain.activeTerrain;
        if (t != null) sb.AppendLine("terrain lm=" + t.lightmapIndex + " cast=" + t.shadowCastingMode + " mat=" + (t.materialTemplate ? t.materialTemplate.shader.name : "-"));
        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        if (dock != null) foreach (var r in dock.GetComponentsInChildren<MeshRenderer>()) sb.AppendLine("dock " + r.name + " cast=" + r.shadowCastingMode + " lm=" + r.lightmapIndex + " shader=" + r.sharedMaterial.shader.name);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_shadow.txt", sb.ToString());

        // 캠프 근접 렌더 — 노을(0)
        if (tod != null) { tod.index = 0; tod.Apply(); }
        if (cam != null)
        {
            var p0 = cam.transform.position; var r0 = cam.transform.rotation; var f0 = cam.fieldOfView; var tt = cam.targetTexture;
            try
            {
                Shot(cam, new Vector3(-4f, 4.5f, 60f), new Vector3(-11f, 1.8f, 54f), "Assets/_preview/shadow_camp_0.png");
                Shot(cam, new Vector3(-10f, 3.0f, 36f), new Vector3(-10f, 0.8f, 44f), "Assets/_preview/shadow_dock_0.png");
                // 직사광 기여 확인 — 태양을 끄고 같은 시점
                if (sun != null)
                {
                    float i0 = sun.intensity; sun.intensity = 0f;
                    Shot(cam, new Vector3(-4f, 4.5f, 60f), new Vector3(-11f, 1.8f, 54f), "Assets/_preview/shadow_camp_nosun.png");
                    sun.intensity = i0;
                    // 그림자 끄고 (실시간·베이크 둘 다 영향 없는지)
                    var sh0 = sun.shadows; sun.shadows = LightShadows.None;
                    Shot(cam, new Vector3(-4f, 4.5f, 60f), new Vector3(-11f, 1.8f, 54f), "Assets/_preview/shadow_camp_noshadow.png");
                    sun.shadows = sh0;
                }
            }
            finally { cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = tt; }
        }
        AssetDatabase.Refresh();
        Debug.Log("[Z5] Logs/pyrite_shadow.txt");
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 60f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
