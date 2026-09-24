// Tools ▸ Pyrite2 ▸ Z36a. Judge Audit (Ctrl+Alt+Shift+1) / Z36b. Judge Views (Ctrl+Alt+Shift+0)
//  월드 콘테스트 심사용 전수 점검. 씬은 바꾸지 않는다(Z36b 는 임시 캡슐을 만들었다 지운다)
//   Z36a → Logs/pyrite_judge.txt : 설명자·스폰, 렌더 규모, 조명·프로브, 텍스처·메시 메모리, 오디오, 파티클, Udon·동기화,
//          픽업·스테이션·거울, 후처리, 누락(스크립트·머티리얼·셰이더), 라이트맵
//   Z36b → Assets/_preview/judge/<시점>_<시각>.png : 눈높이 7 시점 × 새벽 5.8 / 정오 12 / 노을 18.6 / 밤 22, 아바타 조명 캡슐
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteJudge
{
    const string LOG = "Logs/pyrite_judge.txt";
    static StringBuilder sb;

    static string PathOf(Transform t) { var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }
    static string MB(long b) { return (b / 1048576f).ToString("0.0") + " MB"; }

    [MenuItem("Tools/Pyrite2/Z36a. Judge Audit %&#1", false, 100)]
    public static void Audit()
    {
        sb = new StringBuilder("[Z36a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, sb.ToString());
    }

    static void Inner()
    {
        var scene = SceneManager.GetActiveScene();
        var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToList();
        sb.AppendLine("objects " + all.Count + ", active " + all.Count(t => t.gameObject.activeInHierarchy) + ", EditorOnly-tagged " + all.Count(t => t.CompareTag("EditorOnly")));

        // ── A. 설명자·스폰
        sb.AppendLine("\n## A. descriptor / spawn");
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        if (desc == null) sb.AppendLine("  !! VRCSceneDescriptor 없음");
        else
        {
            var rc = desc.ReferenceCamera != null ? desc.ReferenceCamera.GetComponent<Camera>() : null;
            sb.AppendLine(string.Format("  respawnHeightY {0}, objectBehaviourAtRespawn {1}, spawnOrder {2}, forbidUserPortals {3}, refCam {4}",
                desc.RespawnHeightY, Props(desc, "ObjectBehaviourAtRespawn", "objectBehaviourAtRespawn"), desc.spawnOrder, desc.ForbidUserPortals,
                rc != null ? rc.name + " near " + rc.nearClipPlane + " far " + rc.farClipPlane + " hdr " + rc.allowHDR + " msaa " + rc.allowMSAA : "none"));
            var spawns = (desc.spawns == null || desc.spawns.Length == 0) ? new[] { desc.transform } : desc.spawns.Where(s => s != null).ToArray();
            foreach (var s in spawns)
            {
                RaycastHit hit; bool g = Physics.Raycast(s.position + Vector3.up * 1f, Vector3.down, out hit, 20f, ~0, QueryTriggerInteraction.Ignore);
                sb.AppendLine(string.Format("  spawn {0} pos {1} yaw {2:0} → ground {3} ({4:0.00} m below, {5})", s.name, s.position.ToString("F2"), s.eulerAngles.y,
                    g ? hit.collider.name : "NONE", g ? s.position.y - hit.point.y : 0f, g ? LayerMask.LayerToName(hit.collider.gameObject.layer) : "-"));
                var over = Physics.OverlapCapsule(s.position + Vector3.up * 0.3f, s.position + Vector3.up * 1.6f, 0.25f, ~0, QueryTriggerInteraction.Ignore);
                if (over.Length > 0) sb.AppendLine("    !! 스폰 캡슐과 겹침: " + string.Join(", ", over.Select(c => c.name)));
            }
        }

        // ── B. 렌더 규모
        sb.AppendLine("\n## B. rendering");
        var rends = Object.FindObjectsOfType<Renderer>(false).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
        long tris = 0; var perRoot = new Dictionary<string, long>();
        var mats = new HashSet<Material>(); var shaders = new HashSet<Shader>();
        int shadowCasters = 0;
        foreach (var r in rends)
        {
            Mesh m = null;
            if (r is MeshRenderer) { var mf = r.GetComponent<MeshFilter>(); m = mf ? mf.sharedMesh : null; }
            else if (r is SkinnedMeshRenderer) m = ((SkinnedMeshRenderer)r).sharedMesh;
            long t = 0; if (m != null) for (int i = 0; i < m.subMeshCount; i++) t += m.GetIndexCount(i) / 3;
            tris += t;
            string root = r.transform.root.name; perRoot[root] = (perRoot.TryGetValue(root, out var v) ? v : 0) + t;
            foreach (var mt in r.sharedMaterials) if (mt != null) { mats.Add(mt); if (mt.shader) shaders.Add(mt.shader); }
            if (r.shadowCastingMode != ShadowCastingMode.Off && !(r is ParticleSystemRenderer)) shadowCasters++;
        }
        var terr = Object.FindObjectsOfType<Terrain>();
        sb.AppendLine(string.Format("  active renderers {0}, mesh tris {1:N0}, materials {2}, shaders {3}, shadow casters {4}, terrains {5}", rends.Count, tris, mats.Count, shaders.Count, shadowCasters, terr.Length));
        foreach (var t in terr) sb.AppendLine(string.Format("  terrain {0}: heightmap {1}, pixelError {2}, basemapDist {3}, detail {4}/{5}, trees {6}, drawInstanced {7}",
            t.name, t.terrainData.heightmapResolution, t.heightmapPixelError, t.basemapDistance, t.detailObjectDistance, t.detailObjectDensity, t.terrainData.treeInstanceCount, t.drawInstanced));
        sb.AppendLine("  tris by root: " + string.Join(", ", perRoot.OrderByDescending(k => k.Value).Take(12).Select(k => k.Key + " " + (k.Value / 1000f).ToString("0.0") + "k")));
        var badSh = shaders.Where(s => !s.isSupported || s.name.Contains("InternalError") || s.name.Contains("Hidden/")).Select(s => s.name).ToList();
        if (badSh.Count > 0) sb.AppendLine("  !! 문제 셰이더: " + string.Join(", ", badSh));
        sb.AppendLine("  shaders: " + string.Join(", ", shaders.Select(s => s.name).OrderBy(n => n)));

        // ── C. 조명
        sb.AppendLine("\n## C. lights / probes / lightmaps");
        foreach (var l in Object.FindObjectsOfType<Light>(true))
            sb.AppendLine(string.Format("  light {0} {1} {2} active {3} int {4:0.00} range {5:0.0} shadows {6} culling {7}", PathOf(l.transform), l.type, l.lightmapBakeType, l.isActiveAndEnabled, l.intensity, l.range, l.shadows, l.cullingMask == -1 ? "all" : l.cullingMask.ToString()));
        var lp = LightmapSettings.lightProbes;
        if (lp == null || lp.count == 0) sb.AppendLine("  !! 라이트 프로브 없음");
        else
        {
            var pos = lp.positions; var b = new Bounds(pos[0], Vector3.zero); foreach (var p in pos) b.Encapsulate(p);
            sb.AppendLine("  light probes " + lp.count + " bounds " + b.min.ToString("F0") + " .. " + b.max.ToString("F0"));
            foreach (var (n, q) in new[] { ("camp", new Vector3(-10f, 3.4f, 52f)), ("dock end", new Vector3(-9.5f, 1.5f, 31f)), ("lake center", new Vector3(0f, 1f, -14f)), ("far shore", new Vector3(5f, 2.5f, -45f)), ("tent", new Vector3(-4f, 3.2f, 58f)) })
            {
                float d = pos.Min(p => Vector3.Distance(p, q));
                sb.AppendLine(string.Format("    nearest probe to {0} {1}: {2:0.0} m", n, q.ToString("F0"), d));
            }
        }
        foreach (var rp in Object.FindObjectsOfType<ReflectionProbe>(true))
            sb.AppendLine(string.Format("  refl probe {0} {1} res {2} size {3} box {4} active {5}", PathOf(rp.transform), rp.mode, rp.resolution, rp.size.ToString("F0"), rp.boxProjection, rp.isActiveAndEnabled));
        long lmMem = 0; int lmN = 0;
        foreach (var ld in LightmapSettings.lightmaps) foreach (var tx in new Texture[] { ld.lightmapColor, ld.lightmapDir, ld.shadowMask }) if (tx != null) { lmMem += Profiler.GetRuntimeMemorySizeLong(tx); lmN++; }
        sb.AppendLine(string.Format("  lightmaps {0} sets, {1} textures, {2} (editor memory)", LightmapSettings.lightmaps.Length, lmN, MB(lmMem)));
        sb.AppendLine(string.Format("  fog {0} {1} density {2}, ambient {3}, skybox {4}", RenderSettings.fog, RenderSettings.fogMode, RenderSettings.fogDensity, RenderSettings.ambientMode, RenderSettings.skybox ? RenderSettings.skybox.shader.name : "none"));
        sb.AppendLine(string.Format("  occlusion data {0:0.00} MB", StaticOcclusionCulling.umbraDataSize / 1048576f));

        // ── D. 텍스처·메시 메모리 (씬에서 참조하는 것)
        sb.AppendLine("\n## D. textures / meshes");
        var texs = new HashSet<Texture>();
        foreach (var mt in Object.FindObjectsOfType<Renderer>(true).SelectMany(r => r.sharedMaterials).Concat(new[] { RenderSettings.skybox }).Where(m => m != null).Distinct())
            foreach (var id in mt.GetTexturePropertyNameIDs()) { var tx = mt.GetTexture(id); if (tx != null && !(tx is RenderTexture)) texs.Add(tx); }
        foreach (var img in Object.FindObjectsOfType<UnityEngine.UI.Graphic>(true)) if (img.mainTexture != null) texs.Add(img.mainTexture);
        long texMem = 0; var tl = new List<(string, long, string)>();
        foreach (var tx in texs)
        {
            long m = Profiler.GetRuntimeMemorySizeLong(tx); texMem += m;
            string fmt = tx is Texture2D t2 ? t2.format.ToString() : tx.GetType().Name;
            tl.Add((AssetDatabase.GetAssetPath(tx) + " " + tx.width + "x" + tx.height + " " + fmt, m, fmt));
        }
        sb.AppendLine(string.Format("  textures {0}, total {1} (editor memory; 빌드는 압축 설정 따라 다름)", texs.Count, MB(texMem)));
        foreach (var x in tl.OrderByDescending(x => x.Item2).Take(18)) sb.AppendLine("    " + MB(x.Item2) + "  " + x.Item1);
        var unc = tl.Where(x => x.Item3.Contains("RGBA32") || x.Item3.Contains("RGB24") || x.Item3.Contains("ARGB32") || x.Item3.Contains("RGBAHalf") || x.Item3.Contains("RGBAFloat")).ToList();
        sb.AppendLine("  uncompressed textures " + unc.Count + " (" + MB(unc.Sum(x => x.Item2)) + "): " + string.Join(" | ", unc.OrderByDescending(x => x.Item2).Take(8).Select(x => x.Item1)));
        var meshes = new HashSet<Mesh>();
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>(true)) if (mf.sharedMesh) meshes.Add(mf.sharedMesh);
        foreach (var sm in Object.FindObjectsOfType<SkinnedMeshRenderer>(true)) if (sm.sharedMesh) meshes.Add(sm.sharedMesh);
        foreach (var mc in Object.FindObjectsOfType<MeshCollider>(true)) if (mc.sharedMesh) meshes.Add(mc.sharedMesh);
        long meshMem = meshes.Sum(m => Profiler.GetRuntimeMemorySizeLong(m));
        sb.AppendLine(string.Format("  meshes {0}, {1}, read/write {2}", meshes.Count, MB(meshMem), meshes.Count(m => m.isReadable)));
        foreach (var m in meshes.OrderByDescending(m => m.vertexCount).Take(10)) sb.AppendLine(string.Format("    {0} verts {1:N0} tris {2:N0} {3} {4}", m.name, m.vertexCount, m.triangles.Length / 3, MB(Profiler.GetRuntimeMemorySizeLong(m)), AssetDatabase.GetAssetPath(m)));

        // ── E. 오디오
        sb.AppendLine("\n## E. audio");
        foreach (var a in Object.FindObjectsOfType<AudioSource>(true))
        {
            var c = a.clip; string ci = "no clip";
            if (c != null) { var ai = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(c)) as AudioImporter; ci = string.Format("{0} {1:0.0}s {2}ch {3}", c.name, c.length, c.channels, ai != null ? ai.defaultSampleSettings.loadType + "/" + ai.defaultSampleSettings.compressionFormat + " q" + ai.defaultSampleSettings.quality.ToString("0.00") : ""); }
            bool spat = a.GetComponent("VRCSpatialAudioSource") != null;
            sb.AppendLine(string.Format("  {0} active {1} vol {2:0.00} spatial {3:0.0} loop {4} awake {5} dist {6:0}-{7:0} {8} vrcSpatial {9} | {10}", PathOf(a.transform), a.isActiveAndEnabled, a.volume, a.spatialBlend, a.loop, a.playOnAwake, a.minDistance, a.maxDistance, a.rolloffMode, spat, ci));
        }

        // ── F. 파티클
        sb.AppendLine("\n## F. particles");
        int maxSum = 0;
        foreach (var ps in Object.FindObjectsOfType<ParticleSystem>(true))
        {
            var mn = ps.main; var em = ps.emission; maxSum += ps.gameObject.activeInHierarchy ? mn.maxParticles : 0;
            sb.AppendLine(string.Format("  {0} active {1} max {2} rate {3:0.0} life {4:0.0} prewarm {5} collision {6} lights {7} mode {8}", PathOf(ps.transform), ps.gameObject.activeInHierarchy, mn.maxParticles, em.rateOverTime.constantMax, mn.startLifetime.constantMax, mn.prewarm, ps.collision.enabled, ps.lights.enabled, ps.GetComponent<ParticleSystemRenderer>().renderMode));
        }
        sb.AppendLine("  active max particles sum " + maxSum);

        // ── G. Udon·동기화
        sb.AppendLine("\n## G. udon / sync");
        var ubs = Object.FindObjectsOfType<VRC.Udon.UdonBehaviour>(true);
        sb.AppendLine(string.Format("  UdonBehaviours {0}, sync: {1}", ubs.Length, string.Join(", ", ubs.GroupBy(u => u.SyncMethod.ToString()).Select(g => g.Key + " " + g.Count()))));
        foreach (var u in ubs.Where(u => u.programSource == null)) sb.AppendLine("  !! program 없음: " + PathOf(u.transform) + " (active " + u.gameObject.activeInHierarchy + ")");
        foreach (var g in ubs.Where(u => u.programSource != null).GroupBy(u => u.programSource.name).OrderByDescending(g => g.Count())) sb.AppendLine("    " + g.Key + " ×" + g.Count() + " (" + g.First().SyncMethod + ")");
        sb.AppendLine("  VRCObjectSync " + Object.FindObjectsOfType<VRCObjectSync>(true).Length);

        // ── H. 픽업·스테이션·거울·UI
        sb.AppendLine("\n## H. pickups / stations / mirrors / UI");
        foreach (var p in Object.FindObjectsOfType<VRCPickup>(true))
        {
            var rb = p.GetComponent<Rigidbody>(); var os = p.GetComponent<VRCObjectSync>(); var col = p.GetComponent<Collider>();
            sb.AppendLine(string.Format("  pickup {0} layer {1} rb {2} kinematic {3} gravity {4} objectSync {5} collider {6} autoHold {7} text '{8}' y {9:0.00}",
                PathOf(p.transform), LayerMask.LayerToName(p.gameObject.layer), rb != null, rb != null && rb.isKinematic, rb != null && rb.useGravity, os != null, col != null ? col.GetType().Name + (col.isTrigger ? "(trigger)" : "") : "NONE", p.AutoHold, p.InteractionText, p.transform.position.y));
        }
        foreach (var s in Object.FindObjectsOfType<VRC.SDK3.Components.VRCStation>(true))
        {
            var u = s.GetComponent<VRC.Udon.UdonBehaviour>();
            sb.AppendLine(string.Format("  station {0} active {1} mobility {2} seated {3} canExit {4} enter {5} exit {6} udon {7} collider {8}", PathOf(s.transform), s.gameObject.activeInHierarchy, s.PlayerMobility, s.seated, s.disableStationExit ? "no" : "yes",
                s.stationEnterPlayerLocation ? s.stationEnterPlayerLocation.name : "-", s.stationExitPlayerLocation ? s.stationExitPlayerLocation.name : "-", u == null ? "NONE" : (u.programSource == null ? "program NONE" : u.programSource.name), s.GetComponent<Collider>() != null));
        }
        foreach (var m in Object.FindObjectsOfType<VRCMirrorReflection>(true))
            sb.AppendLine(string.Format("  mirror {0} active {1} layers {2} | {3}", PathOf(m.transform), m.gameObject.activeInHierarchy, m.m_ReflectLayers.value, Dump(m)));
        sb.AppendLine("  VRCUiShape " + Object.FindObjectsOfType<VRCUiShape>(true).Length + ", canvases " + Object.FindObjectsOfType<Canvas>(true).Length);

        // ── I. 후처리
        sb.AppendLine("\n## I. post-processing");
        foreach (var v in Object.FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>(true))
            sb.AppendLine(string.Format("  volume {0} global {1} weight {2:0.00} priority {3} profile {4} [{5}]", PathOf(v.transform), v.isGlobal, v.weight, v.priority, v.sharedProfile ? v.sharedProfile.name : "none",
                v.sharedProfile ? string.Join(", ", v.sharedProfile.settings.Where(s => s.active).Select(s => s.GetType().Name)) : ""));
        foreach (var l in Object.FindObjectsOfType<UnityEngine.Rendering.PostProcessing.PostProcessLayer>(true)) sb.AppendLine("  layer on " + l.name + " AA " + l.antialiasingMode + " volumeLayer " + l.volumeLayer.value);

        // ── J. 누락
        sb.AppendLine("\n## J. missing");
        int miss = 0; foreach (var t in all) { int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject); if (n > 0) { miss += n; if (miss < 10) sb.AppendLine("  missing script: " + PathOf(t)); } }
        sb.AppendLine("  missing scripts " + miss);
        int nullMat = 0; foreach (var r in rends) if (r.sharedMaterials.Any(m => m == null)) { nullMat++; if (nullMat < 10) sb.AppendLine("  null material: " + PathOf(r.transform)); }
        sb.AppendLine("  renderers with null material " + nullMat);
        int noMesh = Object.FindObjectsOfType<MeshFilter>(false).Count(f => f.sharedMesh == null && f.GetComponent<Renderer>() && f.GetComponent<Renderer>().enabled && !(f.GetComponent<TMPro.TMP_Text>()));
        sb.AppendLine("  active mesh filters without mesh " + noMesh);

        // ── K0. 걸어서 밖으로 나갈 수 있나 — 호수 중심에서 10° 마다 1 m 씩 밖으로, 지면(물 위 걷기 판 제외)이 1 m 에 1.19 m(50°) 넘게 오르면 벽, 지면이 없으면 허공
        sb.AppendLine("\n## K0. walk-out test from lake center (1 m steps, wall = rise > 50°, void = no ground)");
        {
            Physics.SyncTransforms();
            var lines = new List<string>();
            for (int a = 0; a < 360; a += 10)
            {
                var d = new Vector3(Mathf.Sin(a * Mathf.Deg2Rad), 0f, Mathf.Cos(a * Mathf.Deg2Rad));
                float prev = float.NaN; string res = "reach 250";
                for (int r = 0; r <= 250; r++)
                {
                    var p = new Vector3(-10f, 0f, -14f) + d * r;
                    float gy = float.NaN;
                    foreach (var hh in Physics.RaycastAll(new Vector3(p.x, 300f, p.z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore))
                        if (hh.collider.name != "WaterWalk" && (float.IsNaN(gy) || hh.point.y > gy)) gy = hh.point.y;
                    if (float.IsNaN(gy)) { res = "VOID at r " + r + " (last ground y " + (float.IsNaN(prev) ? "-" : prev.ToString("0.0")) + ")"; break; }
                    if (!float.IsNaN(prev) && gy - prev > 1.19f) { res = "wall at r " + r + " (y " + prev.ToString("0.0") + "→" + gy.ToString("0.0") + ")"; break; }
                    prev = Mathf.Max(gy, 0.05f);   // 물 위는 WaterWalk 높이
                }
                lines.Add(a + ": " + res);
            }
            foreach (var l in lines) sb.AppendLine("  " + l);
        }

        // ── K. 월드 경계 — 스폰 주변 원형으로 걸어 나가면 바닥이 끊기는 곳
        sb.AppendLine("\n## K. walkable ground ring (raycast down every 10° at r 60/90/120 m from lake center)");
        foreach (float rad in new[] { 60f, 90f, 120f })
        {
            int hit = 0; var gaps = new List<int>();
            for (int a = 0; a < 360; a += 10)
            {
                var p = new Vector3(-10f + Mathf.Sin(a * Mathf.Deg2Rad) * rad, 200f, -14f + Mathf.Cos(a * Mathf.Deg2Rad) * rad);
                if (Physics.Raycast(p, Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore)) hit++; else gaps.Add(a);
            }
            sb.AppendLine(string.Format("  r {0}: ground {1}/36, no ground at az {2}", rad, hit, gaps.Count == 0 ? "-" : string.Join(",", gaps)));
        }
    }

    // 직렬화 필드 이름을 모를 때: 이름 후보를 찾아 값 문자열로
    static string Props(Object o, params string[] names)
    {
        var so = new SerializedObject(o);
        foreach (var n in names) { var p = so.FindProperty(n); if (p != null) return n + "=" + Val(p); }
        return "?";
    }
    static string Dump(Object o)
    {
        var so = new SerializedObject(o); var it = so.GetIterator(); var parts = new List<string>();
        if (it.NextVisible(true)) do { if (it.depth == 0 && it.name != "m_Script") parts.Add(it.name + "=" + Val(it)); } while (it.NextVisible(false));
        return string.Join(" ", parts);
    }
    static string Val(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Float: return p.floatValue.ToString("0.###");
            case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumDisplayNames.Length ? p.enumDisplayNames[p.enumValueIndex] : p.intValue.ToString();
            case SerializedPropertyType.LayerMask: return p.intValue.ToString();
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue ? p.objectReferenceValue.name : "null";
            default: return p.propertyType.ToString();
        }
    }

    // ───────── 렌더 ─────────
    struct V { public string n; public Vector3 eye, look; public float fov; }
    static readonly V[] Views =
    {
        new V{ n="1_spawn",      eye=Vector3.zero, look=Vector3.zero, fov=60f },   // 설명자 스폰에서 스폰 방향
        new V{ n="2_camp",       eye=new Vector3(-12.5f, 0f, 55.5f), look=new Vector3(-9.5f, 2.2f, 50.5f), fov=60f },
        new V{ n="3_camp_lake",  eye=new Vector3(-10.0f, 0f, 49.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=60f },
        new V{ n="4_dock_back",  eye=new Vector3(-9.6f, 0.51f, 30.9f), look=new Vector3(-10.0f, 3.5f, 55.0f), fov=60f },
        new V{ n="5_on_lake",    eye=new Vector3(2f, 0.05f, 0f), look=new Vector3(-10f, 3f, 52f), fov=60f },
        new V{ n="6_east_shore", eye=new Vector3(12f, 0f, 36f), look=new Vector3(-13f, 2f, -60f), fov=60f },
        new V{ n="9_south_gap",  eye=new Vector3(0f, 0.05f, -14f), look=new Vector3(-10f, 6f, -110f), fov=60f },
        new V{ n="7_west_wall",  eye=new Vector3(25f, 0f, 45f), look=new Vector3(-78f, 5f, 20f), fov=60f },
    };
    static readonly float[] Hours = { 5.8f, 12f, 18.6f, 22f };

    [MenuItem("Tools/Pyrite2/Z36b. Judge Views %&#0", false, 101)]
    public static void Views_()
    {
        var log = new StringBuilder("[Z36b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        const string OUT = "Assets/_preview/judge/"; Directory.CreateDirectory(OUT);
        var desc = Object.FindObjectOfType<VRCSceneDescriptor>();
        var sp = desc != null && desc.spawns != null && desc.spawns.Length > 0 && desc.spawns[0] != null ? desc.spawns[0] : (desc != null ? desc.transform : null);
        // 아바타 조명 확인용 캡슐 (Standard 회색, 라이트 프로브) — 캠프와 부두
        var capMat = new Material(Shader.Find("Standard")); capMat.color = new Color(0.72f, 0.72f, 0.72f); capMat.SetFloat("_Glossiness", 0.3f);
        var caps = new List<GameObject>();
        foreach (var cp in new[] { new Vector3(-11.8f, 0f, 53.8f), new Vector3(0f, 0.05f, -14f) })
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Capsule); c.name = "__JudgeCapsule"; Object.DestroyImmediate(c.GetComponent<Collider>());
            float gy = cp.y > 0.1f ? cp.y : Ground(cp);
            c.transform.position = new Vector3(cp.x, gy + 0.85f, cp.z); c.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
            var r = c.GetComponent<MeshRenderer>(); r.sharedMaterial = capMat; r.lightProbeUsage = LightProbeUsage.BlendProbes; r.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            caps.Add(c);
        }
        try
        {
            foreach (var h in Hours)
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                foreach (var v in Views)
                {
                    Vector3 eye, look;
                    if (v.n == "1_spawn" && sp != null) { eye = sp.position + Vector3.up * 1.6f; look = eye + sp.forward * 10f; }
                    else { eye = v.eye; eye.y = (v.eye.y > 0f ? v.eye.y : Ground(v.eye)) + 1.6f; look = v.look; }
                    Shot(cam, eye, look, v.fov, OUT + v.n + "_" + h.ToString("00.0") + ".png");
                }
                foreach (var c in caps)   // 캡슐 근접
                {
                    var tp = c.transform.position; var eye = tp + new Vector3(1.2f, 0.4f, -1.6f);
                    Shot(cam, eye, tp, 45f, OUT + "8_avatar_" + (tp.z > 45f ? "camp" : "lake") + "_" + h.ToString("00.0") + ".png");
                }
            }
            log.AppendLine("  7 views + 2 avatar capsules × hours " + string.Join(",", Hours));
            log.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            foreach (var c in caps) Object.DestroyImmediate(c);
            Object.DestroyImmediate(capMat);
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, log.ToString());
    }


    // ───────── Z36c 성능 가정 비교 (Game 뷰 UnityStats) — 기본 / 점광 그림자 끔 / 꽃 끔 / 둘 다 끔, 3 시점, 밤 22 시
    static int cStep, cWait; static List<string> cRes; static Vector3 cp0; static Quaternion cr0; static float cf0;
    static readonly (string n, Vector3 eye, Vector3 look)[] CViews =
    {
        ("camp",      new Vector3(-12.5f, 3.65f, 55.5f), new Vector3(-9.5f, 2.2f, 50.5f)),
        ("camp_lake", new Vector3(-10.0f, 3.65f, 49.0f), new Vector3(-10.0f, 3.0f, -40.0f)),
        ("west_wall", new Vector3(25f, 3.3f, 45f), new Vector3(-78f, 5f, 20f)),
    };
    static Light[] cPoint; static LightShadows[] cPointSh; static Renderer[] cFlowers;

    [MenuItem("Tools/Pyrite2/Z36c. Judge Perf What-if %&#j", false, 102)]
    public static void PerfWhatIf()
    {
        var cam = Camera.main; cp0 = cam.transform.position; cr0 = cam.transform.rotation; cf0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>(); cyc.ResetCache(); cyc.EvaluateAt(22f);
        cPoint = Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Point && l.shadows != LightShadows.None).ToArray();
        cPointSh = cPoint.Select(l => l.shadows).ToArray();
        var ff = GameObject.Find("FlowerField"); cFlowers = ff != null ? ff.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray() : new Renderer[0];
        cRes = new List<string> { "[Z36c] " + System.DateTime.Now.ToString("HH:mm:ss") + " point lights with shadows: " + string.Join(", ", cPoint.Select(l => l.name + " " + l.shadows)) + ", flower renderers " + cFlowers.Length };
        cStep = 0; cWait = 0;
        var gv = System.Type.GetType("UnityEditor.GameView,UnityEditor"); if (gv != null) EditorWindow.GetWindow(gv, false, null, true);
        EditorApplication.update -= CTick; EditorApplication.update += CTick;
    }

    static void CSet(int cfg)
    {
        for (int i = 0; i < cPoint.Length; i++) cPoint[i].shadows = (cfg == 1 || cfg == 3) ? LightShadows.None : cPointSh[i];
        foreach (var r in cFlowers) r.enabled = !(cfg == 2 || cfg == 3);
    }

    static void CTick()
    {
        var cam = Camera.main; int vi = cStep / 4, cfg = cStep % 4;
        if (cWait == 0)
        {
            if (vi >= CViews.Length)
            {
                EditorApplication.update -= CTick; CSet(0);
                cam.transform.SetPositionAndRotation(cp0, cr0); cam.fieldOfView = cf0;
                var cyc = Object.FindObjectOfType<PyriteDayCycle>(); cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
                cRes.Add("RESULT: DONE"); File.AppendAllText(LOG, string.Join("\n", cRes) + "\n"); return;
            }
            var v = CViews[vi]; cam.transform.SetPositionAndRotation(v.eye, Quaternion.LookRotation(v.look - v.eye)); cam.fieldOfView = 60f;
            CSet(cfg);
        }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (++cWait < 8) return;
        string[] names = { "base", "pointShadowsOff", "flowersOff", "bothOff" };
        cRes.Add(string.Format("  {0,-10} {1,-16} tris {2,9:N0} batches {3,5} setpass {4,4} shadowCasters {5,5}", CViews[vi].n, names[cfg], UnityStats.triangles, UnityStats.batches, UnityStats.setPassCalls, UnityStats.shadowCasters));
        cWait = 0; cStep++;
    }


    // ───────── Z36d 노을 시리즈 — 캠프 눈높이에서 해 지는 방위(sunSetAz)로, 17.0~19.6 시, 후처리 켬/끔
    [MenuItem("Tools/Pyrite2/Z36d. Sunset Series", false, 103)]
    public static void SunsetSeries()
    {
        var log = new StringBuilder("[Z36d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var ppl = cam.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessLayer>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        const string OUT = "Assets/_preview/judge/sunset/"; Directory.CreateDirectory(OUT);
        var sky = RenderSettings.skybox;
        try
        {
            float az = cyc.sunSetAz * Mathf.Deg2Rad;
            var eye = new Vector3(-10.0f, 3.65f, 49.0f);
            var dir = new Vector3(Mathf.Sin(az), Mathf.Tan(12f * Mathf.Deg2Rad), Mathf.Cos(az));
            foreach (var h in new[] { 17.0f, 17.5f, 18.0f, 18.33f, 18.6f, 18.8f, 19.0f, 19.2f, 19.4f, 19.6f })
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                log.AppendLine(string.Format("  {0:00.00}h sunEl {1,6:0.0}  sky _SunElevation {2:0.000} _Exposure {3:0.00} _HorizonHaze {4:0.00} _MieStrength {5:0.00} _MieG {6:0.00} cloud {7:0.00}  ppDay {8:0.00} ppNight {9:0.00}",
                    h, cyc.sunElNow, sky.GetFloat("_SunElevation"), sky.GetFloat("_Exposure"), sky.GetFloat("_HorizonHaze"), sky.GetFloat("_MieStrength"), sky.GetFloat("_MieG"), sky.GetFloat("_CloudCoverage"),
                    cyc.ppDay != null ? cyc.ppDay.weight : -1f, cyc.ppNight != null ? cyc.ppNight.weight : -1f));
                foreach (bool pp in new[] { true, false })
                {
                    if (ppl != null) ppl.enabled = pp;
                    Shot(cam, eye, eye + dir, 60f, OUT + h.ToString("00.00") + (pp ? "_pp" : "_raw") + ".png");
                }
            }
            log.AppendLine("  sunSetAz " + cyc.sunSetAz + ", setHour " + cyc.setHour + ", view el 12°");
            log.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { log.AppendLine("EXCEPTION " + e); }
        finally
        {
            if (ppl != null) ppl.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        Directory.CreateDirectory("Logs"); File.AppendAllText(LOG, log.ToString());
    }


    // ───────── Z36e 꽃 거리 Play 확인 — Play(ClientSim) 중에 FlowerField 의 PyriteFlowerCull.distance 를 160/80/40/20 으로 바꿔 켜진 렌더러 수·Game 뷰 삼각형
    static int eStep, eWait; static List<string> eRes; static readonly float[] EDist = { 160f, 80f, 40f, 20f };
    [MenuItem("Tools/Pyrite2/Z36e. Flower Cull Play Test", false, 104)]
    public static void FlowerPlay()
    {
        eRes = new List<string> { "[Z36e] " + System.DateTime.Now.ToString("HH:mm:ss") + " playing " + Application.isPlaying };
        if (!Application.isPlaying) { File.AppendAllText(LOG, string.Join("\n", eRes) + "\n"); return; }
        eStep = 0; eWait = 0;
        EditorApplication.update -= ETick; EditorApplication.update += ETick;
    }
    static void ETick()
    {
        var ff = GameObject.Find("FlowerField");
        var ub = ff != null ? ff.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name.Contains("FlowerCull")) : null;
        if (ub == null) { eRes.Add("FlowerCull UdonBehaviour 없음"); EditorApplication.update -= ETick; File.AppendAllText(LOG, string.Join("\n", eRes) + "\n"); return; }
        if (eStep >= EDist.Length)
        {
            ub.SetProgramVariable("distance", 160f); foreach (var r in ff.GetComponentsInChildren<Renderer>(true)) r.enabled = true;
            EditorApplication.update -= ETick; eRes.Add("RESULT: DONE"); File.AppendAllText(LOG, string.Join("\n", eRes) + "\n"); return;
        }
        if (eWait == 0) { ub.SetProgramVariable("distance", EDist[eStep]); if (EDist[eStep] >= 160f) { foreach (var r in ff.GetComponentsInChildren<Renderer>()) r.enabled = true; } }
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (++eWait < 90) return;   // Update 가 0.5 초마다 판정
        var rs = ff.GetComponentsInChildren<Renderer>(true);
        var lp = VRC.SDKBase.Networking.LocalPlayer;
        eRes.Add(string.Format("  distance {0,4} m: renderers on {1}/{2}, player {3}, game view tris {4:N0}", EDist[eStep], rs.Count(r => r.enabled), rs.Length, lp != null ? lp.GetPosition().ToString("F0") : "-", UnityStats.triangles));
        eWait = 0; eStep++;
    }


    // ───────── Z36f 첫 방문 Play 확인 — 입장 후 패널·팝업 상태 → 프로젝터 끄기 → 저장 여부
    static int fStep; static double fT; static List<string> fRes;
    [MenuItem("Tools/Pyrite2/Z36f. First Visit Play Test", false, 105)]
    public static void FirstVisitPlay()
    {
        fRes = new List<string> { "[Z36f] " + System.DateTime.Now.ToString("HH:mm:ss") + " playing " + Application.isPlaying };
        if (!Application.isPlaying) { File.AppendAllText(LOG, string.Join("\n", fRes) + "\n"); return; }
        fStep = 0; fT = EditorApplication.timeSinceStartup;
        EditorApplication.update -= FTick; EditorApplication.update += FTick;
    }
    static string FState()
    {
        var root = GameObject.Find("SettingsProjector");
        var ubs = root.GetComponents<VRC.Udon.UdonBehaviour>();
        var pj = ubs.FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteProjector");
        var fv = ubs.FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteFirstVisit");
        var ui = root.transform.Find("SettingsUI"); var gp = ui != null ? ui.Find("GuidePopup") : null;
        return string.Format("projector isOn {0}, SettingsUI active {1}, GuidePopup active {2}, firstVisit autoOpened {3} done {4}",
            pj != null ? pj.GetProgramVariable("isOn") : "?", ui != null && ui.gameObject.activeInHierarchy, gp != null && gp.gameObject.activeInHierarchy,
            fv != null ? fv.GetProgramVariable("autoOpened") : "?", fv != null ? fv.GetProgramVariable("done") : "?");
    }
    static void FTick()
    {
        double t = EditorApplication.timeSinceStartup - fT;
        if (fStep == 0) { fRes.Add("  t0: " + FState()); fStep = 1; }
        else if (fStep == 1 && t > 1.0)
        {
            var root = GameObject.Find("SettingsProjector");
            var pj = root.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteProjector");
            if (pj != null) pj.SendCustomEvent("TurnOff");
            fRes.Add("  TurnOff sent"); fStep = 2; fT = EditorApplication.timeSinceStartup;
        }
        else if (fStep == 2 && t > 2.0)
        {
            fRes.Add("  after close: " + FState()); fRes.Add("RESULT: DONE");
            EditorApplication.update -= FTick; File.AppendAllText(LOG, string.Join("\n", fRes) + "\n");
        }
    }


    // ───────── Z36g 개인 설정 저장 Play 확인 — 1회차: 값 바꾸고 저장 / 2회차(Logs/pyrite_prefs_marker.txt 있음): 다시 들어와 읽기
    static double gT; static int gStep; static List<string> gRes;
    const string G_MARK = "Logs/pyrite_prefs_marker.txt";
    [MenuItem("Tools/Pyrite2/Z36g. Prefs Play Test", false, 106)]
    public static void PrefsPlay()
    {
        gRes = new List<string> { "[Z36g] " + System.DateTime.Now.ToString("HH:mm:ss") + " playing " + Application.isPlaying + " mode " + (File.Exists(G_MARK) ? "READ" : "WRITE") };
        if (!Application.isPlaying) { File.AppendAllText(LOG, string.Join("\n", gRes) + "\n"); return; }
        gStep = 0; gT = EditorApplication.timeSinceStartup;
        EditorApplication.update -= GTick; EditorApplication.update += GTick;
    }
    static string GState()
    {
        var root = GameObject.Find("SettingsProjector"); var ui = root.transform.Find("SettingsUI");
        var st = root.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteSettings");
        var fc = GameObject.Find("FlowerField").GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name.Contains("FlowerCull"));
        var fire = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.name == "Fire_Light" && l.transform.parent != null && l.transform.parent.name == "campfire");
        var pb = GameObject.Find("SettingsPP/PP_UserBright")?.GetComponent<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
        return string.Format("flowerDist slider {0} cull {1}, lightShadows toggle {2} fire {3}, bright slider {4:0.00} ppBright {5:0.00}, lang {6}, restored {7}",
            ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name == "FlowerDist").value, fc != null ? fc.GetProgramVariable("distance") : "?",
            ui.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).First(x => x.name == "LightShadows").isOn, fire != null ? fire.shadows.ToString() : "?",
            ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name.StartsWith("Bright")).value, pb != null ? pb.weight : -1f,
            st.GetProgramVariable("lang"), st.GetProgramVariable("restored"));
    }
    static void GTick()
    {
        double t = EditorApplication.timeSinceStartup - gT;
        var root = GameObject.Find("SettingsProjector"); var ui = root.transform.Find("SettingsUI");
        var st = root.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteSettings");
        bool read = File.Exists(G_MARK);
        if (gStep == 0 && t > 1.0)
        {
            gRes.Add("  start: " + GState());
            if (!read)
            {
                ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name == "FlowerDist").value = 4f; st.SendCustomEvent("OnFlowerDist");
                ui.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).First(x => x.name == "LightShadows").isOn = false; st.SendCustomEvent("OnLightShadows");
                ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name.StartsWith("Bright")).value = 0.5f; st.SendCustomEvent("OnBright");
                st.SendCustomEvent("SetKO");
                gRes.Add("  set: flowerDist 4, lightShadows off, bright 0.5, KO");
            }
            gStep = 1; gT = EditorApplication.timeSinceStartup;
        }
        else if (gStep == 1 && t > 2.5)
        {
            gRes.Add("  after 2.5 s: " + GState());
            if (!read) File.WriteAllText(G_MARK, "wrote"); else File.Delete(G_MARK);
            gRes.Add("RESULT: DONE"); EditorApplication.update -= GTick; File.AppendAllText(LOG, string.Join("\n", gRes) + "\n");
        }
    }


    // Z36h — Play 중 개인 설정을 기본값으로 되돌리고 저장 (Z36g 테스트 뒤 ClientSim 저장소 정리용)
    [MenuItem("Tools/Pyrite2/Z36h. Prefs Reset (Play)", false, 107)]
    public static void PrefsReset()
    {
        if (!Application.isPlaying) return;
        var root = GameObject.Find("SettingsProjector"); var ui = root.transform.Find("SettingsUI");
        var st = root.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null && u.programSource.name == "PyriteSettings");
        ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name == "FlowerDist").value = 16f; st.SendCustomEvent("OnFlowerDist");
        ui.GetComponentsInChildren<UnityEngine.UI.Toggle>(true).First(x => x.name == "LightShadows").isOn = true; st.SendCustomEvent("OnLightShadows");
        ui.GetComponentsInChildren<UnityEngine.UI.Slider>(true).First(x => x.name.StartsWith("Bright")).value = 0f; st.SendCustomEvent("OnBright");
        st.SendCustomEvent("SetEN");
        File.AppendAllText(LOG, "[Z36h] " + System.DateTime.Now.ToString("HH:mm:ss") + " reset sent (flower 16, light shadows on, bright 0, EN)\n");
    }


    // Z36i — Play 중 효과음: 머그 Fill/Sip, 꼬치 Eat 을 불러 AudioSource 가 울리는지
    static int iStep; static double iT; static List<string> iRes;
    [MenuItem("Tools/Pyrite2/Z36i. SFX Play Test", false, 108)]
    public static void SfxPlay()
    {
        iRes = new List<string> { "[Z36i] " + System.DateTime.Now.ToString("HH:mm:ss") + " playing " + Application.isPlaying };
        if (!Application.isPlaying) { File.AppendAllText(LOG, string.Join("\n", iRes) + "\n"); return; }
        iStep = 0; iT = EditorApplication.timeSinceStartup; EditorApplication.update -= ITick; EditorApplication.update += ITick;
    }
    static VRC.Udon.UdonBehaviour UB(string path) { var g = GameObject.Find(path); return g != null ? g.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null) : null; }
    static void ITick()
    {
        if (EditorApplication.timeSinceStartup - iT < 0.12) return;
        iT = EditorApplication.timeSinceStartup;
        string[] act = { "CampProps/Mug_0/Visual/Body/../../State|Fill", "CampProps/Mug_0/State|Sip", "CampProps/Skewer_0/State|Eat", "CampProps/Skewer_0/State|Renew" };
        if (iStep > 0)
        {
            var prev = act[iStep - 1].Split('|')[0]; var g = FindState(prev);
            var a = g != null ? g.GetComponent<AudioSource>() : null;
            iRes.Add(string.Format("  {0}: audio {1} playing {2}", act[iStep - 1], a != null, a != null && a.isPlaying));
        }
        if (iStep >= act.Length) { EditorApplication.update -= ITick; iRes.Add("RESULT: DONE"); File.AppendAllText(LOG, string.Join("\n", iRes) + "\n"); return; }
        var p = act[iStep].Split('|'); var go = FindState(p[0]);
        var ub = go != null ? go.GetComponents<VRC.Udon.UdonBehaviour>().FirstOrDefault(u => u.programSource != null) : null;
        if (ub != null) ub.SendCustomEvent(p[1]); else iRes.Add("  없음 " + p[0]);
        iStep++;
    }
    static GameObject FindState(string path)
    {
        var parts = path.Split('/'); var root = GameObject.Find("CampProps"); if (root == null) return null;
        var holder = root.transform.Find(parts[1]); if (holder == null) return null;
        var st = holder.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "State");
        return st != null ? st.gameObject : null;
    }

    static float Ground(Vector3 p)
    {
        Physics.SyncTransforms();
        foreach (var h in Physics.RaycastAll(new Vector3(p.x, 60f, p.z), Vector3.down, 120f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            if (h.collider.name != "WaterWalk") return h.point.y;
        return 0.05f;
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string path)
    {
        cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
