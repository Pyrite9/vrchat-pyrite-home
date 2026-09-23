// Tools ▸ Pyrite ▸ Z11. Lake Mirror (realtime reflection)  /  Z11b. Revert
//  호수에 실시간 거울 반사. 물 본체(Pyrite/Water, 시간대별 머티리얼 교체)는 그대로 두고
//  같은 격자를 한 장 더 겹친 LakeMirror 오브젝트에 VRCMirrorReflection + Custom Shader(Pyrite/WaterMirror).
//   · 축: VRChat 거울은 -transform.forward 가 법선 (Z10b 인게임 실측) → 오브젝트 (90,0,0), 메시는 그 로컬로 구움
//   · 수심은 정점 색 R — 거울이 머티리얼을 새로 만들어도 텍스처 없이 물가 페이드가 동작
//   · 물·거울 둘 다 Water 레이어(4) — VRChat 은 Water 레이어를 거울에 그리지 않는다(물 밑면이 반사에 끼지 않게)
//   · 반사 레이어: Default, TransparentFX, Interactive, Player, Environment, Pickup, PickupNoEnv, Walkthrough, MirrorReflection
//     (Water·UI·UiMenu·Stereo·PlayerLocal 제외). 픽셀 라이트 끔, 해상도 1024
//   · 기존 반짝이 레이어(M_WaterShimmer 렌더러)는 끈다
//   · 부두 뿌리에 토글 스위치(로컬, 기본 켜짐) — PyriteMirrorToggle.startOn
//   · Z10b 인게임 테스트 거울은 지운다
//  에디터에선 VRChat 거울이 안 그려진다 → 확인은 Build & Test. 로그 Logs/pyrite_lakemirror.txt
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteLakeMirror
{
    const string LOG = "Logs/pyrite_lakemirror.txt";
    const string MESH = "Assets/Meshes/PyriteLakeMirror.asset";
    const string MAT = "Assets/Materials/M_WaterMirror.mat";
    const string ROOT = "LakeMirror";
    const string SWITCH = "LakeMirrorSwitch";
    const int WATER_LAYER = 4;
    const int REFLECT = (1 << 0) | (1 << 1) | (1 << 8) | (1 << 9) | (1 << 11) | (1 << 13) | (1 << 14) | (1 << 17) | (1 << 18);
    static readonly Quaternion ROT = Quaternion.Euler(90f, 0f, 0f);
    static readonly StringBuilder log = new StringBuilder();

    [MenuItem("Tools/Pyrite/Z11. Lake Mirror (realtime reflection)", false, 20)]
    public static void Run()
    {
        log.Clear(); log.AppendLine("[Z11] " + System.DateTime.Now.ToString("HH:mm:ss"));
        PyriteMirrorProbe.Remove();
        log.AppendLine("Z10 테스트 거울 제거");
        RemoveOurs();

        var water = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Water");
        var mf = water == null ? null : water.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh != null && f.sharedMesh.name == "PyriteLakeGrid");
        if (mf == null) { log.AppendLine("PyriteLakeGrid 없음 — 중단"); Flush(); return; }
        var wmr = mf.GetComponent<MeshRenderer>();
        var wmat = wmr.sharedMaterial;
        log.AppendLine(string.Format("물 {0} layer {1} -> {2}, mat {3}", PathOf(mf.gameObject), mf.gameObject.layer, WATER_LAYER, wmat.name));
        Undo.RecordObject(mf.gameObject, "water layer");
        mf.gameObject.layer = WATER_LAYER;

        // 수심 텍스처(CPU 에서 읽기)
        var dtex = wmat.GetTexture("_DepthTex") as Texture2D;
        Texture2D depth = null;
        if (dtex != null)
        {
            depth = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            depth.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(dtex)));
        }
        var rect = wmat.GetVector("_DepthRect");
        float dmax = wmat.GetFloat("_DepthMax");
        log.AppendLine(string.Format("수심 텍스처 {0} {1}x{2} rect {3} max {4}", dtex ? dtex.name : "null", depth ? depth.width : 0, depth ? depth.height : 0, rect, dmax));

        // 메시 — 월드 좌표 → 회전 로컬 좌표 (local = ROT^-1 * world, 위치 원점)
        var src = mf.sharedMesh;
        var inv = Quaternion.Inverse(ROT);
        var v = src.vertices.Select(p => inv * mf.transform.TransformPoint(p)).ToArray();
        var cols = src.vertices.Select(p =>
        {
            var w = mf.transform.TransformPoint(p);
            float d = 0f;
            if (depth != null)
            {
                float u = (w.x - rect.x) * rect.z, t = (w.z - rect.y) * rect.w;
                if (u >= 0 && u <= 1 && t >= 0 && t <= 1) d = depth.GetPixelBilinear(u, t).r;
            }
            return new Color(d, 0f, 0f, 1f);
        }).ToArray();
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MESH);
        bool isNew = mesh == null;
        if (isNew) mesh = new Mesh();
        mesh.Clear();
        mesh.name = "PyriteLakeMirror";
        mesh.indexFormat = src.indexFormat;
        mesh.vertices = v;
        mesh.colors = cols;
        mesh.normals = Enumerable.Repeat(inv * Vector3.up, v.Length).ToArray();
        mesh.triangles = src.triangles;
        mesh.RecalculateBounds();
        var bb = mesh.bounds; bb.Expand(1f); mesh.bounds = bb;
        if (isNew) AssetDatabase.CreateAsset(mesh, MESH);
        EditorUtility.SetDirty(mesh);
        log.AppendLine(string.Format("메시 {0}v {1}t, 수심>0 정점 {2}", v.Length, src.triangles.Length / 3, cols.Count(c => c.r > 0.001f)));

        // 머티리얼
        var sh = Shader.Find("Pyrite/WaterMirror");
        if (sh == null) { log.AppendLine("Pyrite/WaterMirror 셰이더 없음 — 중단"); Flush(); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, MAT); }
        mat.shader = sh;
        if (wmat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", wmat.GetTexture("_NormalMap"));
        foreach (var p in new[] { "_NormalScale", "_WaveSpeed", "_RippleFar", "_WaveHeight", "_WaveTime", "_WaveNormal", "_FresnelPower", "_ShoreFade", "_ShoreAlpha", "_SwellFade", "_DepthMax" })
            if (wmat.HasProperty(p)) mat.SetFloat(p, wmat.GetFloat(p));
        EditorUtility.SetDirty(mat);

        // 오브젝트
        var go = new GameObject(ROOT);
        Undo.RegisterCreatedObjectUndo(go, "lake mirror");
        go.layer = WATER_LAYER;
        go.transform.SetPositionAndRotation(Vector3.zero, ROT);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        log.AppendLine(string.Format("LakeMirror fwd {0} (법선 = -fwd = {1})", go.transform.forward, -go.transform.forward));

        // VRCMirrorReflection
        System.Type mt = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) { mt = asm.GetType("VRC.SDK3.Components.VRCMirrorReflection"); if (mt != null) break; }
        if (mt == null) { log.AppendLine("VRCMirrorReflection 타입 없음 — 중단"); Flush(); return; }
        var comp = go.AddComponent(mt);
        foreach (var f in mt.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            string n = f.Name, ln = n.ToLowerInvariant();
            object before = f.GetValue(comp), after = before;
            try
            {
                if (ln.Contains("reflectlayers")) after = (LayerMask)REFLECT;
                else if (ln.Contains("shader") && f.FieldType == typeof(Shader)) after = sh;
                else if (ln.Contains("disablepixellights")) after = true;
                else if (ln.Contains("resolution") && f.FieldType.IsEnum)
                {
                    var name = System.Enum.GetNames(f.FieldType).FirstOrDefault(x => x.Contains("1024"));
                    if (name != null) after = System.Enum.Parse(f.FieldType, name);
                }
                if (!Equals(before, after)) f.SetValue(comp, after);
            }
            catch (System.Exception e) { log.AppendLine("  !! " + n + " " + e.Message); }
            log.AppendLine(string.Format("  {0} ({1}) = {2}{3}", n, f.FieldType.Name, f.GetValue(comp), Equals(before, f.GetValue(comp)) ? "" : "   <- " + before));
        }
        EditorUtility.SetDirty(comp);
        ConfigureSerialized(comp, sh);

        // 반짝이 레이어 끄기
        foreach (var r in Object.FindObjectsOfType<Renderer>(true).Where(r => r.sharedMaterial != null && r.sharedMaterial.name == "M_WaterShimmer"))
        {
            Undo.RecordObject(r.gameObject, "shimmer off");
            log.AppendLine("반짝이 끔: " + PathOf(r.gameObject) + " (was active " + r.gameObject.activeSelf + ")");
            r.gameObject.SetActive(false);
        }

        BuildSwitch(go);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        log.AppendLine("RESULT: DONE"); Flush();
    }

    static void BuildSwitch(GameObject mirror)
    {
        var dock = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "Dock");
        var dmr = dock == null ? null : dock.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "default");
        var t = Terrain.activeTerrain;
        Vector3 pos;
        if (dmr != null)
        {
            var b = dmr.bounds;
            pos = new Vector3(b.min.x - 0.6f, 0f, b.max.z - 1.0f);
            // 뿌리 쪽이 물이면 땅이 나올 때까지 물가 쪽(+Z)으로
            for (int k = 0; k < 12 && t != null && t.SampleHeight(pos) + t.transform.position.y < 0.15f; k++) pos.z += 0.5f;
        }
        else pos = new Vector3(-11.5f, 0f, 46f);
        if (t != null) pos.y = t.SampleHeight(pos) + t.transform.position.y;

        var brass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Brass_Mirror.mat");
        var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Wood_Mirror.mat");
        var lampOff = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_MirrorLamp_Off.mat");
        var lampOn = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_MirrorLamp_On.mat");

        var root = new GameObject(SWITCH);
        Undo.RegisterCreatedObjectUndo(root, "lake mirror switch");
        root.transform.position = pos;
        root.transform.rotation = Quaternion.LookRotation(Vector3.back);     // 물가에서 걸어오는 사람(+Z 쪽)을 향해 앞면(-Z 로컬 기준 박스 앞)
        Prim(PrimitiveType.Cylinder, root.transform, "Post", new Vector3(0f, 0.5f, 0f), new Vector3(0.09f, 0.5f, 0.09f), wood);
        var sw = new GameObject("Switch"); sw.transform.SetParent(root.transform, false);
        sw.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        Prim(PrimitiveType.Cube, sw.transform, "Knob", Vector3.zero, new Vector3(0.14f, 0.12f, 0.14f), brass);
        var lamp = Prim(PrimitiveType.Sphere, sw.transform, "Lamp", new Vector3(0f, 0.09f, 0f), new Vector3(0.06f, 0.06f, 0.06f), lampOn);
        var box = sw.AddComponent<BoxCollider>(); box.size = new Vector3(0.26f, 0.30f, 0.26f); box.center = new Vector3(0f, 0.03f, 0f);

        var tog = UdonSharpUndo.AddComponent<PyriteMirrorToggle>(sw);
        tog.mirror = mirror; tog.lamp = lamp.GetComponent<Renderer>(); tog.lampOff = lampOff; tog.lampOn = lampOn; tog.startOn = true;
        UdonSharpEditorUtility.CopyProxyToUdon(tog);
        EditorUtility.SetDirty(tog);
        log.AppendLine(string.Format("스위치 {0} (지면 {1:0.00})", pos, pos.y));
    }

    [MenuItem("Tools/Pyrite/Z11b. Lake Mirror Revert", false, 21)]
    public static void Revert()
    {
        log.Clear(); log.AppendLine("[Z11b] " + System.DateTime.Now.ToString("HH:mm:ss"));
        RemoveOurs();
        foreach (var r in Resources.FindObjectsOfTypeAll<Renderer>().Where(r => r.gameObject.scene.IsValid() && r.sharedMaterial != null && r.sharedMaterial.name == "M_WaterShimmer"))
        { r.gameObject.SetActive(true); log.AppendLine("반짝이 켬: " + PathOf(r.gameObject)); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        log.AppendLine("RESULT: REVERTED (물 레이어는 Water 유지)"); Flush();
    }

    static void RemoveOurs()
    {
        foreach (var g in SceneManager.GetActiveScene().GetRootGameObjects())
            if (g.name == ROOT || g.name == SWITCH) Undo.DestroyObjectImmediate(g);
    }

    // 공개 필드에 없는 직렬화 값(Custom Shader, 해상도 등)은 SerializedObject 로 찾는다
    static void ConfigureSerialized(Component comp, Shader sh)
    {
        var so = new SerializedObject(comp);
        var it = so.GetIterator();
        bool enter = true;
        while (it.NextVisible(enter))
        {
            enter = false;
            string n = it.name, ln = n.ToLowerInvariant(), before = Show(it);
            if (it.propertyType == SerializedPropertyType.ObjectReference && ln.Contains("shader"))
                it.objectReferenceValue = sh;
            else if (it.propertyType == SerializedPropertyType.Enum && ln.Contains("resolution"))
            {
                int k = System.Array.FindIndex(it.enumNames, x => x.Contains("1024"));
                if (k >= 0) it.enumValueIndex = k;
            }
            string after = Show(it);
            log.AppendLine(string.Format("  [so] {0} ({1}) = {2}{3}", n, it.propertyType, after, before == after ? "" : "   <- " + before));
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static string Show(SerializedProperty p)
    {
        switch (p.propertyType)
        {
            case SerializedPropertyType.ObjectReference: return p.objectReferenceValue ? p.objectReferenceValue.name : "null";
            case SerializedPropertyType.Enum: return p.enumValueIndex >= 0 && p.enumValueIndex < p.enumNames.Length ? p.enumNames[p.enumValueIndex] : p.enumValueIndex.ToString();
            case SerializedPropertyType.Boolean: return p.boolValue.ToString();
            case SerializedPropertyType.Integer: return p.intValue.ToString();
            case SerializedPropertyType.LayerMask: return "0x" + p.intValue.ToString("X");
            case SerializedPropertyType.Float: return p.floatValue.ToString();
            default: return p.propertyType.ToString();
        }
    }

    [MenuItem("Tools/Pyrite/Z11c. Lake Mirror Configure (serialized)", false, 22)]
    public static void Configure()
    {
        log.Clear(); log.AppendLine("[Z11c] " + System.DateTime.Now.ToString("HH:mm:ss"));
        var go = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ROOT);
        System.Type mt = null;
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies()) { mt = asm.GetType("VRC.SDK3.Components.VRCMirrorReflection"); if (mt != null) break; }
        var comp = go == null || mt == null ? null : go.GetComponent(mt);
        if (comp == null) { log.AppendLine("LakeMirror 없음"); Flush(); return; }
        ConfigureSerialized(comp, Shader.Find("Pyrite/WaterMirror"));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        log.AppendLine("RESULT: DONE"); Flush();
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localScale = scale;
        if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    static string PathOf(GameObject g) { var s = g.name; for (var t = g.transform.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
    static void Flush() { Directory.CreateDirectory("Logs"); File.WriteAllText(LOG, log.ToString()); }
}
#endif
