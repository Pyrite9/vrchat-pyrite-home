// Tools ▸ Pyrite2 ▸ Z30c. Place Boat  /  Z30d. Boat Revert
//  ootwn 木製ボート (2.0 × 0.94 × 5.16 m, 556 tris, 뱃머리 = 로컬 −Z, 선미 판 = +Z) 를 부두 끝 서쪽(계선주·랜턴 쪽)에 띄운다
//   부두 x −11.01..−8.99, 끝 z 30.9 → 보트 중심선 x −12.13, 선체 z 30.2..35.4 (부두와 나란히, 뱃머리가 호수 쪽)
//   수면 y 0 → 피벗 y 0.25 (선체 밑 −0.15, 안쪽 바닥 +0.04, 뱃전 +0.79)
//   머티리얼: 부두 M_DockWood(Pyrite/StandardNight) 를 복사한 M_Boat 에 보트 텍스처(색·NormalGL·AO) → 밤 색조가 부두와 같다. nightMats 에 등록
//   충돌: 선체 MeshCollider(정적) — 배 안에 서 있을 수 있다. 앉기 2곳(가운데 가로판·선미 자리) = VRCStation(ChairSeat_1 설정 복사), 호수 쪽(−Z)을 본다
//   앉는 높이는 선체에 임시 콜라이더를 붙여 중심선을 위에서 레이캐스트해 찾는다
//  렌더 Assets/_preview/boat/place_*.png
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VRC.SDK3.Components;

public static class PyriteBoatBuild
{
    const string PREFAB = "Assets/WoodBoat/WoodBoat.prefab";
    const string MAT = "Assets/Materials/M_Boat.mat";
    static readonly Vector3 POS = new Vector3(-12.13f, 0.25f, 32.35f);   // 0.12 에선 안쪽 바닥(로컬 −0.21)이 수면 아래라 배 안에 물이 보였다 → 바닥 +0.04, 흘수 15 cm

    [MenuItem("Tools/Pyrite2/Z30c. Place Boat", false, 42)]
    public static void Build()
    {
        var sb = new StringBuilder("[Z30c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB);
        var dockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_DockWood.mat");
        var chair = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == "ChairSeat_1");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        if (pf == null || chair == null || cyc == null) { sb.AppendLine("prefab / ChairSeat_1 / DayCycle 없음"); Flush(sb); return; }
        Remove(sb);

        // 텍스처 (노멀은 NormalMap 으로 임포트 — 유료 폴더 안 .meta 는 저장소에 안 올라간다)
        const string TX = "Assets/WoodBoat/Textures/WoodFloor003_1K-JPG_";
        var nImp = AssetImporter.GetAtPath(TX + "NormalGL.jpg") as TextureImporter;
        if (nImp != null && nImp.textureType != TextureImporterType.NormalMap) { nImp.textureType = TextureImporterType.NormalMap; nImp.SaveAndReimport(); }
        var tCol = AssetDatabase.LoadAssetAtPath<Texture2D>(TX + "Color.jpg");
        var tNrm = AssetDatabase.LoadAssetAtPath<Texture2D>(TX + "NormalGL.jpg");
        var tAO = AssetDatabase.LoadAssetAtPath<Texture2D>(TX + "AmbientOcclusion.jpg");

        // 머티리얼
        Material m;
        if (dockMat != null) { m = new Material(dockMat); sb.AppendLine("M_Boat ← M_DockWood (" + dockMat.shader.name + ")"); }
        else { m = new Material(Shader.Find("Standard")); sb.AppendLine("M_DockWood 없음 → Standard"); }
        m.name = "M_Boat";
        m.mainTexture = tCol; m.mainTextureScale = Vector2.one; m.mainTextureOffset = Vector2.zero;
        if (m.HasProperty("_BumpMap")) { m.SetTexture("_BumpMap", tNrm); m.SetTextureScale("_BumpMap", Vector2.one); m.EnableKeyword("_NORMALMAP"); }
        if (m.HasProperty("_OcclusionMap")) { m.SetTexture("_OcclusionMap", tAO); m.SetTextureScale("_OcclusionMap", Vector2.one); }
        if (m.HasProperty("_Color")) m.color = new Color(0.78f, 0.70f, 0.60f);
        if (AssetDatabase.LoadAssetAtPath<Material>(MAT) != null) AssetDatabase.DeleteAsset(MAT);
        AssetDatabase.CreateAsset(m, MAT);
        m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (!cyc.nightMats.Contains(m)) { cyc.nightMats = cyc.nightMats.Append(m).ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc); }

        // 배치
        var dock = GameObject.Find("Dock");
        var boat = (GameObject)PrefabUtility.InstantiatePrefab(pf, dock != null ? dock.transform : null);
        boat.name = "Boat";
        Undo.RegisterCreatedObjectUndo(boat, "boat");
        boat.transform.SetPositionAndRotation(POS, Quaternion.identity);
        foreach (var r in boat.GetComponentsInChildren<MeshRenderer>(true)) { r.sharedMaterials = r.sharedMaterials.Select(_ => m).ToArray(); r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes; }
        foreach (var t in boat.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        var hull = boat.GetComponentsInChildren<MeshFilter>(true).First(f => f.sharedMesh != null && f.sharedMesh.name == "WoodBoat");
        var mc = hull.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = hull.sharedMesh;
        Physics.SyncTransforms();

        // 앉는 높이: 중심선 레이캐스트 (선체 콜라이더만)
        var hits = new List<(float z, float y)>();
        for (float z = -2.0f; z <= 2.9f; z += 0.05f)
        {
            var o = boat.transform.TransformPoint(new Vector3(0f, 2f, z));
            if (mc.Raycast(new Ray(o, Vector3.down), out var h, 4f)) hits.Add((z, boat.transform.InverseTransformPoint(h.point).y));
        }
        float floor = hits.Count > 0 ? hits.Min(x => x.y) : -0.3f;
        sb.AppendLine("centerline hits: " + string.Join(" ", hits.Where((x, i) => i % 4 == 0).Select(x => x.z.ToString("0.0") + ":" + x.y.ToString("0.00"))) + " | floor " + floor.ToString("0.00"));
        // 바닥보다 12 cm 이상 높은 연속 구간 = 가로판
        var segs = new List<(float z0, float z1, float y)>();
        foreach (var x in hits)
        {
            if (x.y < floor + 0.12f) continue;
            if (segs.Count > 0 && x.z - segs[segs.Count - 1].z1 < 0.08f && Mathf.Abs(x.y - segs[segs.Count - 1].y) < 0.05f) { var s = segs[segs.Count - 1]; segs[segs.Count - 1] = (s.z0, x.z, Mathf.Max(s.y, x.y)); }
            else segs.Add((x.z, x.z, x.y));
        }
        sb.AppendLine("seats(segments): " + string.Join(", ", segs.Select(s => string.Format("z {0:0.00}..{1:0.00} y {2:0.00}", s.z0, s.z1, s.y))));
        var seats = segs.Where(s => s.z1 - s.z0 >= 0.12f).OrderByDescending(s => s.z1 - s.z0).Take(2).OrderBy(s => s.z0).ToList();
        if (seats.Count == 0) seats.Add((0.2f, 0.4f, floor + 0.3f));

        var src = chair.GetComponent<VRCStation>();
        int k = 0;
        foreach (var s in seats)
        {
            k++;
            float zc = (s.z0 + s.z1) * 0.5f;
            var go = new GameObject("BoatSeat_" + k);
            go.transform.SetParent(boat.transform, false);
            go.transform.localPosition = new Vector3(0f, s.y, zc);
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);                 // 호수 쪽(−Z)을 본다
            var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.05f, 0f); bc.size = new Vector3(0.9f, 0.14f, Mathf.Max(0.25f, s.z1 - s.z0));
            var st = go.AddComponent<VRCStation>();
            EditorUtility.CopySerialized(src, st);
            var sp = new GameObject("SeatPoint").transform; sp.SetParent(go.transform, false); sp.localPosition = new Vector3(0f, -0.08f, 0f);
            var ep = new GameObject("ExitPoint").transform; ep.SetParent(boat.transform, false);
            ep.position = new Vector3(-10.0f, 0.55f, boat.transform.TransformPoint(new Vector3(0, 0, zc)).z);   // 부두 위로 내린다
            st.stationEnterPlayerLocation = sp; st.stationExitPlayerLocation = ep;
            EditorUtility.SetDirty(st);
            sb.AppendLine(string.Format("  BoatSeat_{0} local z {1:0.00} y {2:0.00} → world {3}", k, zc, s.y, go.transform.position.ToString("F2")));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Shots(cyc, boat, sb);
        AssetDatabase.SaveAssets();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite2/Z30d. Boat Revert", false, 43)]
    public static void Revert()
    {
        var sb = new StringBuilder("[Z30d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        Remove(sb);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    static void Remove(StringBuilder sb)
    {
        var old = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == "Boat");
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("기존 Boat 삭제"); }
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (cyc != null && m != null && cyc.nightMats.Contains(m)) { cyc.nightMats = cyc.nightMats.Where(x => x != m).ToArray(); UdonSharpEditorUtility.CopyProxyToUdon(cyc); EditorUtility.SetDirty(cyc); }
    }

    static void Shots(PyriteDayCycle cyc, GameObject boat, StringBuilder sb)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var c = boat.transform.position;
        Directory.CreateDirectory("Assets/_preview/boat/");
        var views = new (string n, Vector3 e, Vector3 l, float f)[]
        {
            ("dock", new Vector3(-10.0f, 2.1f, 40.5f), c + new Vector3(0f, 0.2f, 0.5f), 55f),
            ("near", c + new Vector3(2.6f, 1.9f, 4.2f), c + new Vector3(0f, 0.1f, 0.3f), 50f),
            ("camp", new Vector3(-10.4f, 3.4f, 50.5f), c, 45f),
        };
        try
        {
            cyc.ResetCache();
            foreach (var h in new[] { PyriteDayCycleSetup.EDITOR_HOUR, 21f, 12f })
            {
                cyc.EvaluateAt(h);
                foreach (var v in views) Shot(cam, v.e, v.l, v.f, string.Format("Assets/_preview/boat/place_{0:00}_{1}.png", (int)h, v.n));
            }
            sb.AppendLine("  shots place_{18,21,12}_{dock,near,camp}");
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1280, H = 720;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tx = new Texture2D(W, H, TextureFormat.RGB24, false);
        tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_boat.txt", sb.ToString()); }
}
#endif
