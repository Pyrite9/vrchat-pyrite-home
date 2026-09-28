// PyriteBedroomProbe.cs — 텐트 침실 1단계 실측 (읽기 전용, 씬을 바꾸지 않는다)
// 메뉴: Tools ▸ Pyrite3 ▸ Z49a. Tent Bedroom Probe
// 출력: Logs/pyrite_bedroom_probe.txt
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public static class PyriteBedroomProbe
{
    const string LOG = "Logs/pyrite_bedroom_probe.txt";

    [MenuItem("Tools/Pyrite3/Z49a. Tent Bedroom Probe", false, 4900)]
    public static void Run()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Pyrite tent bedroom probe  " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        // 1. Scene descriptor
        sb.AppendLine("\n## SceneDescriptor");
        var descs = Object.FindObjectsOfType<VRC.SDKBase.VRC_SceneDescriptor>(true);
        foreach (var d in descs)
        {
            sb.AppendLine("obj " + PathOf(d.transform) + " pos " + V(d.transform.position));
            sb.AppendLine("RespawnHeightY " + d.RespawnHeightY);
            sb.AppendLine("ReferenceCamera " + (d.ReferenceCamera ? PathOf(d.ReferenceCamera.transform) : "null"));
            if (d.spawns != null)
                for (int i = 0; i < d.spawns.Length; i++)
                    sb.AppendLine("spawn[" + i + "] " + (d.spawns[i] ? PathOf(d.spawns[i]) + " " + V(d.spawns[i].position) : "null"));
            if (d.ReferenceCamera)
            {
                var c = d.ReferenceCamera.GetComponent<Camera>();
                if (c != null) sb.AppendLine("refcam near " + c.nearClipPlane + " far " + c.farClipPlane + " clear " + c.clearFlags);
            }
        }
        if (descs.Length == 0) sb.AppendLine("NONE");

        // 2. Render settings
        sb.AppendLine("\n## RenderSettings");
        sb.AppendLine("fog " + RenderSettings.fog + " mode " + RenderSettings.fogMode + " density " + RenderSettings.fogDensity
            + " start " + RenderSettings.fogStartDistance + " end " + RenderSettings.fogEndDistance + " color " + RenderSettings.fogColor);
        sb.AppendLine("skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name + " / " + RenderSettings.skybox.shader.name : "null"));
        sb.AppendLine("ambientMode " + RenderSettings.ambientMode + " intensity " + RenderSettings.ambientIntensity
            + " sky " + RenderSettings.ambientSkyColor + " eq " + RenderSettings.ambientEquatorColor + " ground " + RenderSettings.ambientGroundColor);
        sb.AppendLine("sun " + (RenderSettings.sun ? PathOf(RenderSettings.sun.transform) : "null"));
        sb.AppendLine("defaultReflection " + RenderSettings.defaultReflectionMode + " res " + RenderSettings.defaultReflectionResolution);

        // 3. Quality / lighting
        sb.AppendLine("\n## Quality / Lighting");
        sb.AppendLine("quality " + QualitySettings.names[QualitySettings.GetQualityLevel()]
            + " shadowDistance " + QualitySettings.shadowDistance + " cascades " + QualitySettings.shadowCascades
            + " shadows " + QualitySettings.shadows + " pixelLights " + QualitySettings.pixelLightCount);
        LightingSettings ls = null; try { ls = Lightmapping.lightingSettings; } catch { }
        if (ls != null) sb.AppendLine("lightingSettings " + ls.name + " mixed " + ls.mixedBakeMode + " res " + ls.lightmapResolution + " maxSize " + ls.lightmapMaxSize);
        sb.AppendLine("lightmaps " + LightmapSettings.lightmaps.Length + " probes " + (LightmapSettings.lightProbes ? LightmapSettings.lightProbes.count : 0));
        foreach (var l in Object.FindObjectsOfType<Light>(true))
        {
            if (l.type != LightType.Directional) continue;
            sb.AppendLine("dirLight " + PathOf(l.transform) + " active " + l.gameObject.activeInHierarchy + " mode " + l.lightmapBakeType
                + " intensity " + l.intensity + " shadows " + l.shadows + " rot " + V(l.transform.eulerAngles));
        }

        // 4. Post-processing volumes
        sb.AppendLine("\n## PostProcessVolumes");
        foreach (var v in Object.FindObjectsOfType<PostProcessVolume>(true))
            sb.AppendLine(PathOf(v.transform) + " active " + v.gameObject.activeInHierarchy + " global " + v.isGlobal
                + " priority " + v.priority + " weight " + v.weight + " layer " + LayerMask.LayerToName(v.gameObject.layer)
                + " profile " + (v.sharedProfile ? v.sharedProfile.name : "null") + " blend " + v.blendDistance);
        foreach (var pl in Object.FindObjectsOfType<PostProcessLayer>(true))
            sb.AppendLine("layer on " + PathOf(pl.transform) + " volumeMask " + pl.volumeLayer.value + " trigger " + (pl.volumeTrigger ? pl.volumeTrigger.name : "null"));

        // 5. World extents
        sb.AppendLine("\n## Extents (active renderers / colliders)");
        var rends = Object.FindObjectsOfType<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        Bounds rb = new Bounds(); bool first = true;
        foreach (var r in rends)
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            if (first) { rb = r.bounds; first = false; } else rb.Encapsulate(r.bounds);
        }
        sb.AppendLine("renderers " + rends.Length + " bounds min " + V(rb.min) + " max " + V(rb.max));
        foreach (var r in rends.Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer))
                               .OrderByDescending(r => r.bounds.max.magnitude).Take(8))
            sb.AppendLine("  far renderer " + PathOf(r.transform) + " max " + V(r.bounds.max) + " min " + V(r.bounds.min));
        var cols = Object.FindObjectsOfType<Collider>().Where(c => c.enabled && c.gameObject.activeInHierarchy).ToArray();
        Bounds cb = new Bounds(); first = true;
        foreach (var c in cols) { if (first) { cb = c.bounds; first = false; } else cb.Encapsulate(c.bounds); }
        sb.AppendLine("colliders " + cols.Length + " bounds min " + V(cb.min) + " max " + V(cb.max));
        foreach (var c in cols.OrderByDescending(c => c.bounds.max.magnitude).Take(5))
            sb.AppendLine("  far collider " + PathOf(c.transform) + " max " + V(c.bounds.max) + " min " + V(c.bounds.min));

        // 6. Audio reach
        sb.AppendLine("\n## Audio (maxDistance)");
        foreach (var a in Object.FindObjectsOfType<AudioSource>(true).OrderByDescending(a => a.maxDistance).Take(12))
            sb.AppendLine(PathOf(a.transform) + " pos " + V(a.transform.position) + " max " + a.maxDistance + " rolloff " + a.rolloffMode
                + " spatialBlend " + a.spatialBlend + " clip " + (a.clip ? a.clip.name : "null"));

        // 7. Tent candidates
        sb.AppendLine("\n## Tent candidates (name contains tent)");
        foreach (var t in Object.FindObjectsOfType<Transform>(true).Where(t => t.name.ToLower().Contains("tent")))
        {
            var r = t.GetComponent<Renderer>();
            var col = t.GetComponent<Collider>();
            sb.AppendLine(PathOf(t) + " active " + t.gameObject.activeInHierarchy + " pos " + V(t.position)
                + (r ? " rb " + V(r.bounds.min) + ".." + V(r.bounds.max) + " static " + GameObjectUtility.GetStaticEditorFlags(t.gameObject) : "")
                + (col ? " collider " + col.GetType().Name + " trigger " + col.isTrigger : "")
                + " children " + t.childCount + " udon " + (t.GetComponent("UdonBehaviour") != null));
        }

        // 8. Lie station controller
        sb.AppendLine("\n## Lie controller");
        foreach (var g in AssetDatabase.FindAssets("AC_LieDown t:RuntimeAnimatorController"))
            sb.AppendLine(AssetDatabase.GUIDToAssetPath(g) + " guid " + g);

        // 9. Candidate box positions
        sb.AppendLine("\n## Candidate box (8x5x8 m) overlap / distance");
        Vector3[] cand = { new Vector3(2000, 0, 0), new Vector3(0, 0, 2000), new Vector3(-2000, 0, 0), new Vector3(2000, 200, 0), new Vector3(1500, 0, 0) };
        foreach (var p in cand)
        {
            var hits = Physics.OverlapBox(p + Vector3.up * 2.5f, new Vector3(4, 2.5f, 4), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            float dR = Mathf.Sqrt(rb.SqrDistance(p));
            float dC = Mathf.Sqrt(cb.SqrDistance(p));
            sb.AppendLine("cand " + V(p) + " overlaps " + hits.Length + " distToRenderBounds " + dR.ToString("F1") + " distToColliderBounds " + dC.ToString("F1"));
        }

        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[PyriteBedroomProbe] wrote " + LOG);
        EditorGUIUtility.systemCopyBuffer = sb.ToString(); // 결과를 클립보드로 (Claude 가 읽음)
    }

    static string V(Vector3 v) { return "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")"; }
    static string PathOf(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
