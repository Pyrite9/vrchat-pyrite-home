using System;
using System.Reflection;
using UdonSharpEditor;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// 거울 — 기본 꺼짐 + 상호작용 토글
//  구조:  Mirror(루트)
//           MirrorFrame   기둥 2 + 상단보 + 받침   (항상 보임)
//           MirrorSwitch  손잡이 박스 + 표시등     (상호작용)
//           MirrorSurface Quad + VRCMirrorReflection  (기본 비활성)
public static class PyriteMirrorTools
{
    const string MAT = "Assets/Materials/";

    // 캠프 서쪽 끝. 모닥불(-10.5, 51.5)을 바라보게 세운다.
    const float MX = -16.2f, MZ = 57.6f;
    const float MW = 1.40f, MH = 2.20f;     // 거울면 크기
    const float SILL = 0.12f;               // 바닥에서 거울 아랫변까지

    // VRChat 레이어 — 반사에 넣을 것만 켠다. UI/UiMenu/Stereo 는 빼야 한다.
    const int REFLECT =
        (1 << 0) | (1 << 1) | (1 << 4) | (1 << 8) | (1 << 9) | (1 << 10) |
        (1 << 11) | (1 << 13) | (1 << 14) | (1 << 17) | (1 << 18);

    [MenuItem("Tools/Pyrite/L. Setup Mirror", false, 261)]
    public static void Setup()
    {
        var old = FindRoot("Mirror");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);

        var terrain = Terrain.activeTerrain;
        float gy = terrain != null
            ? terrain.SampleHeight(new Vector3(MX, 0f, MZ)) + terrain.transform.position.y
            : 1.81f;

        var root = new GameObject("Mirror");
        root.transform.position = new Vector3(MX, gy, MZ);
        // 모닥불 방향을 바라보게
        Vector3 to = new Vector3(-10.5f - MX, 0f, 51.5f - MZ);
        root.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);

        var brass = Solid("M_Brass_Mirror", new Color(0.55f, 0.44f, 0.20f), 0.85f, 0.60f);
        var wood  = Solid("M_Wood_Mirror",  new Color(0.24f, 0.17f, 0.11f), 0.00f, 0.25f);
        var lampOff = Emissive("M_MirrorLamp_Off", new Color(0.18f, 0.16f, 0.13f), Color.black);
        var lampOn  = Emissive("M_MirrorLamp_On",  new Color(0.95f, 0.78f, 0.34f),
                                                    new Color(1.00f, 0.74f, 0.24f) * 1.6f);

        // ── 틀 ─────────────────────────────────────────────
        var frame = new GameObject("MirrorFrame");
        frame.transform.SetParent(root.transform, false);
        float hw = MW * 0.5f + 0.07f;
        float top = SILL + MH + 0.10f;
        Prim(PrimitiveType.Cylinder, frame.transform, "Post_L",
             new Vector3(-hw, top * 0.5f, 0f), new Vector3(0.09f, top * 0.5f, 0.09f), brass, true);
        Prim(PrimitiveType.Cylinder, frame.transform, "Post_R",
             new Vector3(hw, top * 0.5f, 0f), new Vector3(0.09f, top * 0.5f, 0.09f), brass, true);
        Prim(PrimitiveType.Cube, frame.transform, "Bar_Top",
             new Vector3(0f, top, 0f), new Vector3(hw * 2f + 0.09f, 0.09f, 0.09f), brass, true);
        Prim(PrimitiveType.Cube, frame.transform, "Base",
             new Vector3(0f, 0.045f, 0f), new Vector3(hw * 2f + 0.30f, 0.09f, 0.34f), wood, true);
        // 거울 뒷판 — 꺼져 있을 때 허공에 틀만 떠 보이지 않도록
        Prim(PrimitiveType.Cube, frame.transform, "Backing",
             new Vector3(0f, SILL + MH * 0.5f, 0.035f), new Vector3(MW + 0.06f, MH + 0.06f, 0.03f), wood, true);

        // ── 스위치 ─────────────────────────────────────────
        var sw = new GameObject("MirrorSwitch");
        sw.transform.SetParent(root.transform, false);
        sw.transform.localPosition = new Vector3(hw + 0.02f, 1.05f, -0.10f);
        Prim(PrimitiveType.Cube, sw.transform, "Knob", Vector3.zero,
             new Vector3(0.10f, 0.14f, 0.07f), brass, true);
        var lampGo = Prim(PrimitiveType.Sphere, sw.transform, "Lamp",
             new Vector3(0f, 0.10f, -0.045f), new Vector3(0.05f, 0.05f, 0.05f), lampOff, true);
        var box = sw.AddComponent<BoxCollider>();
        box.size = new Vector3(0.22f, 0.26f, 0.22f);
        box.center = new Vector3(0f, 0.03f, -0.03f);

        // ── 거울면 ─────────────────────────────────────────
        var surf = new GameObject("MirrorSurface");
        surf.transform.SetParent(root.transform, false);
        surf.transform.localPosition = new Vector3(0f, SILL + MH * 0.5f, 0f);
        surf.transform.localScale = new Vector3(MW, MH, 1f);
        var mf = surf.AddComponent<MeshFilter>();
        mf.sharedMesh = QuadMesh();
        var mr = surf.AddComponent<MeshRenderer>();
        var mirrorMat = FindMirrorMaterial();
        if (mirrorMat != null) mr.sharedMaterial = mirrorMat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(surf, 0);
        AddMirrorComponent(surf);
        surf.SetActive(false);                       // 🔴 기본 꺼짐

        // ── 배선 ───────────────────────────────────────────
        var tog = sw.GetComponent<PyriteMirrorToggle>();
        if (tog == null) tog = UdonSharpUndo.AddComponent<PyriteMirrorToggle>(sw);
        tog.mirror  = surf;
        tog.lamp    = lampGo.GetComponent<Renderer>();
        tog.lampOff = lampOff;
        tog.lampOn  = lampOn;
        UdonSharpEditorUtility.CopyProxyToUdon(tog);
        EditorUtility.SetDirty(tog);

        EditorSceneManager_MarkDirty();
        Debug.Log(string.Format("[MIR] 완료 — 위치 ({0:F1}, {1:F2}, {2:F1}) / 거울면 {3}×{4} / 기본 꺼짐 / 머티리얼 {5}",
            MX, gy, MZ, MW, MH, mirrorMat != null ? mirrorMat.name : "없음(VRChat이 런타임에 교체)"));
    }

    // ── 도우미 ─────────────────────────────────────────────
    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }

    static Mesh QuadMesh()
    {
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(tmp);
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, string name,
                           Vector3 pos, Vector3 scale, Material mat, bool stripCollider)
    {
        var go = GameObject.CreatePrimitive(t);
        go.name = name;
        if (stripCollider)
        { var c = go.GetComponent<Collider>(); if (c != null) UnityEngine.Object.DestroyImmediate(c); }
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        GameObjectUtility.SetStaticEditorFlags(go, 0);
        return go;
    }

    static Material Solid(string name, Color c, float metallic, float smooth)
    {
        var path = MAT + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", c);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Glossiness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material Emissive(string name, Color c, Color emission)
    {
        var m = Solid(name, c, 0.2f, 0.7f);
        if (emission.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;  // 굽지 않는다(런타임 토글)
            m.SetColor("_EmissionColor", emission);
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material FindMirrorMaterial()
    {
        foreach (var g in AssetDatabase.FindAssets("t:Material VRCMirror"))
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m != null) { Debug.Log("[MIR] 거울 머티리얼 ← " + p); return m; }
        }
        // SDK 머티리얼이 없으면 어두운 금속으로 채운다. VRChat 은 런타임에 자기 것으로 갈아끼우지만,
        // 비워두면 에디터에서 기본 흰색 Standard 로 보여서 틀 안이 하얗게 뜬다.
        Debug.LogWarning("[MIR] VRCMirror 머티리얼을 못 찾음 — 대체 머티리얼 사용 (VRChat이 런타임에 교체)");
        return Solid("M_MirrorFallback", new Color(0.05f, 0.06f, 0.07f), 0.95f, 0.96f);
    }

    static void AddMirrorComponent(GameObject go)
    {
        Type t = FindType("VRC.SDK3.Components.VRCMirrorReflection")
              ?? FindType("VRC.SDKBase.VRC_MirrorReflection");
        if (t == null) { Debug.LogError("[MIR] VRCMirrorReflection 타입 없음 — 수동으로 붙여야 함"); return; }
        var c = go.GetComponent(t) ?? go.AddComponent(t);
        SetMember(c, "m_ReflectLayers", (LayerMask)REFLECT);
        SetMember(c, "TurnOffMirrorOcclusion", true);
    }

    static Type FindType(string full)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        { var t = asm.GetType(full); if (t != null) return t; }
        return null;
    }

    static void SetMember(object o, string name, object v)
    {
        var t = o.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.FieldType.IsInstanceOfType(v)) { f.SetValue(o, v); return; }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType.IsInstanceOfType(v)) p.SetValue(o, v, null);
    }

    static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
