// PyriteHome 월드 빌드 보조 도구 (에디터 전용)
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBuildTools
{
    const string MESH_DIR   = "Assets/Meshes/";
    const string MAT_DIR    = "Assets/Materials/";
    const string PREFAB_DIR = "Assets/Prefabs/";

    static readonly StaticEditorFlags STATIC_FLAGS =
        StaticEditorFlags.ContributeGI
      | StaticEditorFlags.OccluderStatic
      | StaticEditorFlags.OccludeeStatic
      | StaticEditorFlags.BatchingStatic
      | StaticEditorFlags.ReflectionProbeStatic;

    static void Dirty()
    {
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static GameObject EnsureRoot(string name)
    {
        GameObject go = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) { go = r; break; }
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "create " + name);
        }
        go.transform.position = Vector3.zero;
        go.transform.rotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        return go;
    }

    static void ApplyStatic(GameObject go)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true))
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, STATIC_FLAGS);
    }

    static GameObject SpawnModel(string modelName, string matName, Transform parent)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(MESH_DIR + modelName + ".obj");
        if (model == null) { Debug.LogError("[Pyrite] 모델 없음: " + MESH_DIR + modelName + ".obj"); return null; }

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
        inst.name = modelName;
        inst.transform.SetParent(parent, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        inst.transform.localScale = Vector3.one;

        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT_DIR + matName + ".mat");
        if (mat == null) Debug.LogError("[Pyrite] 머티리얼 없음: " + MAT_DIR + matName + ".mat");
        else foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true)) r.sharedMaterial = mat;

        ApplyStatic(inst);
        Undo.RegisterCreatedObjectUndo(inst, "spawn " + modelName);
        return inst;
    }


    /// 절벽에 박힌 결정 전용 재질 — 그늘진 벽에서 밝은 하늘을 그대로 반사하지 않도록
    /// 알베도를 낮추고 메탈릭/광택을 내린 버전. 지상 군집용 M_Pyrite는 건드리지 않는다.
    static void EnsureCliffMaterial()
    {
        var path = MAT_DIR + "M_Pyrite_Cliff.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var src = AssetDatabase.LoadAssetAtPath<Material>(MAT_DIR + "M_Pyrite.mat");
        if (mat == null)
        {
            if (src == null) { Debug.LogError("[Pyrite] M_Pyrite 없음"); return; }
            mat = new Material(src);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (src != null) mat.shader = src.shader;
        mat.SetColor("_Color", new Color(0.412f, 0.352f, 0.181f, 1f));
        mat.SetFloat("_Metallic", 0.85f);
        mat.SetFloat("_Glossiness", 0.45f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/Pyrite/1. Place Crystal Meshes")]
    public static void PlaceCrystalMeshes()
    {
        var root = EnsureRoot("Crystals");
        foreach (var c in root.transform.Cast<Transform>().ToList())
            if (c.name.StartsWith("PyriteIn") || c.name.StartsWith("PyriteAccent"))
                Undo.DestroyObjectImmediate(c.gameObject);

        EnsureCliffMaterial();
        SpawnModel("PyriteInCliff",  "M_Pyrite_Cliff",   root.transform);
        SpawnModel("PyriteInGround", "M_Pyrite_Tarnish", root.transform);
        SpawnModel("PyriteAccent",   "M_Pyrite_Iris",    root.transform);
        Dirty();
        Debug.Log("[Pyrite] 결정 메시 3종 배치 완료");
    }

    struct Landmark { public string prefab; public Vector3 pos; public float yaw; public float scale; }

    [MenuItem("Tools/Pyrite/2. Place Landmark Clusters")]
    public static void PlaceLandmarkClusters()
    {
        var root = EnsureRoot("Crystals");
        foreach (var c in root.transform.Cast<Transform>().ToList())
            if (c.name.StartsWith("Landmark_"))
                Undo.DestroyObjectImmediate(c.gameObject);

        var specs = new List<Landmark>
        {
            new Landmark{ prefab="PyriteCluster_C", pos=new Vector3(-13f, 0.10f, -73f), yaw= 25f, scale=2.4f },
            new Landmark{ prefab="PyriteCluster_C", pos=new Vector3( 56f, 0.22f,  18f), yaw=200f, scale=2.0f },
            new Landmark{ prefab="PyriteCluster_B", pos=new Vector3(-52f, 0.62f,  26f), yaw=115f, scale=1.6f },
        };

        for (int i = 0; i < specs.Count; i++)
        {
            var s = specs[i];
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_DIR + s.prefab + ".prefab");
            if (pf == null) { Debug.LogError("[Pyrite] 프리팹 없음: " + s.prefab); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(pf);
            inst.name = "Landmark_" + (i + 1) + "_" + s.prefab;
            inst.transform.SetParent(root.transform, false);
            inst.transform.localPosition = s.pos;
            inst.transform.localRotation = Quaternion.Euler(0f, s.yaw, 0f);
            inst.transform.localScale = Vector3.one * s.scale;
            ApplyStatic(inst);
            Undo.RegisterCreatedObjectUndo(inst, "landmark");
        }
        Dirty();
        Debug.Log("[Pyrite] 랜드마크 군집 3개 배치 완료");
    }


    // ======================= 꽃밭 =======================
    const string FLORA_DIR = "Assets/Flora/";

    static Material MakeCutoutMaterial(string matPath, string texPath)
    {
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null) { Debug.LogError("[Pyrite] 텍스처 없음: " + texPath); return null; }

        var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
        if (ti != null)
        {
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.anisoLevel = 4;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
            ti.SaveAndReimport();
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Standard"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.shader = Shader.Find("Standard");
        mat.SetFloat("_Mode", 1f);                                   // Cutout
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
        mat.SetInt("_ZWrite", 1);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.DisableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 2450;
        mat.SetFloat("_Cutoff", 0.45f);
        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_Glossiness", 0.18f);
        mat.SetFloat("_Metallic", 0f);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject MakeDetailPrefab(string objPath, string prefabPath, Material mat)
    {
        Mesh mesh = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(objPath))
            if (o is Mesh m) { mesh = m; break; }
        if (mesh == null) { Debug.LogError("[Pyrite] 메시 없음: " + objPath); return null; }

        var go = new GameObject(System.IO.Path.GetFileNameWithoutExtension(prefabPath));
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

        var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        Object.DestroyImmediate(go);
        return prefab;
    }

    [MenuItem("Tools/Pyrite/3. Build Flower Prefabs")]
    public static void BuildFlowerPrefabs()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Flora"))
            AssetDatabase.CreateFolder("Assets", "Flora");

        var mat = MakeCutoutMaterial(FLORA_DIR + "M_Nemophila.mat", FLORA_DIR + "T_Nemophila_Atlas.png");
        if (mat == null) return;
        MakeDetailPrefab(FLORA_DIR + "Nemophila_Small.obj", FLORA_DIR + "P_Nemophila_Small.prefab", mat);
        MakeDetailPrefab(FLORA_DIR + "Nemophila_Clump.obj", FLORA_DIR + "P_Nemophila_Clump.prefab", mat);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Pyrite] 꽃 프리팹/머티리얼 생성 완료");
    }

    [MenuItem("Tools/Pyrite/4. Paint Flower Field")]
    public static void PaintFlowerField()
    {
        Terrain terrain = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { terrain = r.GetComponentInChildren<Terrain>(); if (terrain != null) break; }
        if (terrain == null) { Debug.LogError("[Pyrite] Terrain 못 찾음"); return; }
        var td = terrain.terrainData;

        var small = AssetDatabase.LoadAssetAtPath<GameObject>(FLORA_DIR + "P_Nemophila_Small.prefab");
        var clump = AssetDatabase.LoadAssetAtPath<GameObject>(FLORA_DIR + "P_Nemophila_Clump.prefab");
        if (small == null || clump == null) { Debug.LogError("[Pyrite] 꽃 프리팹 먼저 생성해 (메뉴 3번)"); return; }

        var binPath = System.IO.Path.Combine(
            System.IO.Directory.GetParent(Application.dataPath).FullName, "FlowerDensity.bin");
        if (!System.IO.File.Exists(binPath)) { Debug.LogError("[Pyrite] 없음: " + binPath); return; }
        var bytes = System.IO.File.ReadAllBytes(binPath);
        int dres = System.BitConverter.ToInt32(bytes, 0);
        int need = 4 + dres * dres * 2;
        if (bytes.Length != need) { Debug.LogError("[Pyrite] bin 크기 불일치: " + bytes.Length + " != " + need); return; }

        Undo.RegisterCompleteObjectUndo(td, "paint flowers");

        td.SetDetailResolution(dres, 16);
#if UNITY_2022_2_OR_NEWER
        td.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
#endif

        DetailPrototype Proto(GameObject pf, float wmin, float wmax, float hmin, float hmax)
        {
            return new DetailPrototype
            {
                prototype = pf,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = wmin, maxWidth = wmax,
                minHeight = hmin, maxHeight = hmax,
                noiseSpread = 4f,
                healthyColor = Color.white,
                dryColor = new Color(0.86f, 0.90f, 0.96f, 1f),
                alignToGround = 0.35f,
                positionJitter = 1f,
                density = 1f
            };
        }
        td.detailPrototypes = new[]
        {
            Proto(small, 0.85f, 1.20f, 0.88f, 1.22f),
            Proto(clump, 0.85f, 1.15f, 0.88f, 1.18f),
        };

        var l0 = new int[dres, dres];
        var l1 = new int[dres, dres];
        long t0 = 0, t1 = 0;
        for (int iz = 0; iz < dres; iz++)
            for (int ix = 0; ix < dres; ix++)
            {
                int o = 4 + (iz * dres + ix) * 2;
                int a = bytes[o], b = bytes[o + 1];
                // SetDetailLayer 배열은 [x, y] 순서 (Unity 문서 예제 기준)
                l0[ix, iz] = a; l1[ix, iz] = b;
                t0 += a; t1 += b;
            }
        td.RefreshPrototypes();
        td.SetDetailLayer(0, 0, 0, l0);
        td.SetDetailLayer(0, 0, 1, l1);

        terrain.detailObjectDistance = 110f;
        terrain.detailObjectDensity = 1f;
        terrain.terrainData.wavingGrassStrength = 0.28f;
        terrain.terrainData.wavingGrassSpeed = 0.35f;
        terrain.terrainData.wavingGrassAmount = 0.30f;
        terrain.terrainData.wavingGrassTint = new Color(0.88f, 0.92f, 1f, 1f);

        EditorUtility.SetDirty(td);
        Dirty();
        Debug.Log(string.Format("[Pyrite] 꽃밭 페인트 완료 — res {0}, small {1}, clump {2}, 합 {3}", dres, t0, t1, t0 + t1));
    }

    [MenuItem("Tools/Pyrite/5. Toggle Flower Field Off/On")]
    public static void ToggleFlowers()
    {
        Terrain terrain = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        { terrain = r.GetComponentInChildren<Terrain>(); if (terrain != null) break; }
        if (terrain == null) return;
        terrain.drawTreesAndFoliage = !terrain.drawTreesAndFoliage;
        Debug.Log("[Pyrite] 디테일 렌더 " + (terrain.drawTreesAndFoliage ? "ON" : "OFF"));
    }

    [MenuItem("Tools/Pyrite/9. Dump Scene Report &0")]
    public static void DumpSceneReport()
    {
        var sb = new StringBuilder();
        var scene = SceneManager.GetActiveScene();
        sb.AppendLine("SCENE: " + scene.path + "   [tools build v7]");
        int totalTris = 0, totalRenderers = 0;
        var mats = new HashSet<string>();

        void Walk(Transform t, int depth)
        {
            var go = t.gameObject;
            sb.Append(new string(' ', depth * 2));
            sb.Append(go.activeSelf ? "" : "(off) ");
            sb.Append(go.name);
            var p = t.localPosition; var e = t.localEulerAngles; var s = t.localScale;
            sb.AppendFormat("  pos({0:0.####},{1:0.####},{2:0.####}) rot({3:0.##},{4:0.##},{5:0.##}) scl({6:0.###},{7:0.###},{8:0.###})",
                p.x, p.y, p.z, e.x, e.y, e.z, s.x, s.y, s.z);

            var flags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((int)flags != 0) sb.Append("  [static:" + flags + "]");

            var mf = go.GetComponent<MeshFilter>();
            var mr = go.GetComponent<MeshRenderer>();
            if (mf != null && mf.sharedMesh != null)
            {
                int tri = mf.sharedMesh.triangles.Length / 3;
                totalTris += tri;
                sb.Append("  mesh=" + mf.sharedMesh.name + " tris=" + tri);
                sb.Append(" uv2=" + (mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0 ? "yes" : "NO"));
            }
            if (mr != null)
            {
                totalRenderers++;
                var names = mr.sharedMaterials.Select(m => m == null ? "<null>" : m.name).ToArray();
                foreach (var n in names) mats.Add(n);
                sb.Append("  mat=[" + string.Join(",", names) + "]");
                if (!mr.enabled) sb.Append(" (renderer off)");
            }
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) sb.Append("  MeshCollider" + (mc.convex ? "(convex)" : "") + (mc.enabled ? "" : "(off)"));
            if (go.GetComponent<BoxCollider>() != null) sb.Append("  BoxCollider");
            if (go.GetComponent<Terrain>() != null) sb.Append("  TERRAIN");
            if (go.GetComponent<ReflectionProbe>() != null) sb.Append("  ReflectionProbe");
            if (go.GetComponent<Light>() != null) sb.Append("  Light(" + go.GetComponent<Light>().type + "," + go.GetComponent<Light>().lightmapBakeType + ")");
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) { sb.Append("  <MISSING SCRIPT>"); continue; }
                var tn = comp.GetType().Name;
                if (tn.StartsWith("VRC") || tn.Contains("Station") || tn.Contains("Udon")) sb.Append("  <" + tn + ">");
            }
            sb.AppendLine();
            foreach (Transform c in t) Walk(c, depth + 1);
        }

        foreach (var r in scene.GetRootGameObjects()) Walk(r.transform, 0);

        sb.AppendLine();
        sb.AppendLine("TOTAL renderers=" + totalRenderers + " tris=" + totalTris);
        sb.AppendLine("MATERIALS: " + string.Join(", ", mats.OrderBy(x => x)));
        sb.AppendLine("Lightmaps baked: " + LightmapSettings.lightmaps.Length);
        {
            var tr = Terrain.activeTerrain;
            if (tr == null) foreach (var rr in scene.GetRootGameObjects())
            { tr = rr.GetComponentInChildren<Terrain>(); if (tr != null) break; }
            if (tr == null) sb.AppendLine("--- TERRAIN NOT FOUND ---"); else {
            var td2 = tr.terrainData;
            sb.AppendLine("--- TERRAIN DETAIL ---");
            sb.AppendLine("drawTreesAndFoliage=" + tr.drawTreesAndFoliage
                + " detailObjectDistance=" + tr.detailObjectDistance
                + " detailObjectDensity=" + tr.detailObjectDensity
                + " drawHeightmap=" + tr.drawHeightmap
                + " detailW/H=" + td2.detailWidth + "/" + td2.detailHeight
                + " resPerPatch=" + td2.detailResolutionPerPatch
#if UNITY_2022_2_OR_NEWER
                + " scatterMode=" + td2.detailScatterMode
#endif
                );
            var protos = td2.detailPrototypes;
            sb.AppendLine("prototypes=" + protos.Length);
            for (int i = 0; i < protos.Length; i++)
            {
                var dp = protos[i];
                long sum = 0;
                var lay = td2.GetDetailLayer(0, 0, td2.detailWidth, td2.detailHeight, i);
                int maxv = 0;
                foreach (var v in lay) { sum += v; if (v > maxv) maxv = v; }
                sb.AppendLine("  [" + i + "] proto=" + (dp.prototype == null ? "<NULL>" : dp.prototype.name)
                    + " useMesh=" + dp.usePrototypeMesh
                    + " render=" + dp.renderMode
                    + " instancing=" + dp.useInstancing
                    + " w=" + dp.minWidth + ".." + dp.maxWidth
                    + " h=" + dp.minHeight + ".." + dp.maxHeight
                    + " sum=" + sum + " max=" + maxv);
                if (dp.prototype != null)
                {
                    var pmf = dp.prototype.GetComponent<MeshFilter>();
                    var pmr = dp.prototype.GetComponent<MeshRenderer>();
                    sb.AppendLine("      mesh=" + (pmf == null || pmf.sharedMesh == null ? "<NONE>" : pmf.sharedMesh.name + " tris=" + pmf.sharedMesh.triangles.Length / 3)
                        + " mat=" + (pmr == null || pmr.sharedMaterial == null ? "<NONE>" : pmr.sharedMaterial.name + " shader=" + pmr.sharedMaterial.shader.name));
                }
            }
            sb.AppendLine("terrain material=" + (tr.materialTemplate == null ? "<none>" : tr.materialTemplate.name + " shader=" + tr.materialTemplate.shader.name));
            } }
        sb.AppendLine("Ambient intensity: " + RenderSettings.ambientIntensity);
        sb.AppendLine("Skybox: " + (RenderSettings.skybox == null ? "<none>" : RenderSettings.skybox.name));

        var path = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).FullName, "pyrite_report.txt");
        System.IO.File.WriteAllText(path, sb.ToString());
        Debug.Log("[Pyrite] 리포트 저장: " + path);
    }
}
#endif
