// 반딧불 + 수면 발광 — 에디터 전용  [lightfx v1]
//  · 반딧불은 꽃 타일 메시를 ParticleSystem의 shape으로 써서 "꽃에서" 솟아오른다.
//  · 수면 발광은 스크롤 노이즈 2겹을 곱한 애디티브 판(Pyrite/WaterShimmer) + 수면 위 모트.
//  · 둘 다 애디티브 언릿이라 라이트맵 베이크에 영향을 주지 않는다. 베이크 전후 아무때나 돌려도 된다.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteLightFX
{
    const string MAT_DIR = "Assets/Materials/";
    const string TEX_DIR = "Assets/TerrainAssets/";

    // 반딧불 색 — 전부 금색. 진한 금 ↔ 옅은 금 사이에서 개체마다 랜덤.
    // (v1에는 4타일에 1번꼴로 청백이 섞여 있었는데 금색으로 통일했다)
    static readonly Color GOLD_A = new Color(1.00f, 0.74f, 0.24f);
    static readonly Color GOLD_B = new Color(1.00f, 0.93f, 0.58f);

    static Material EnsureMat(string name, string shaderName, string texName, System.Action<Material> tune)
    {
        var path = MAT_DIR + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var sh = Shader.Find(shaderName);
        if (sh == null) { Debug.LogError("[Pyrite] 셰이더 없음: " + shaderName); return null; }
        if (mat == null) { mat = new Material(sh); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = sh;
        if (texName != null)
        {
            var tp = TEX_DIR + texName + ".png";
            var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
            if (ti != null && (ti.wrapMode != TextureWrapMode.Repeat || !ti.mipmapEnabled))
            { ti.wrapMode = TextureWrapMode.Repeat; ti.mipmapEnabled = true; ti.SaveAndReimport(); }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(tp);
            if (tex == null) Debug.LogError("[Pyrite] 텍스처 없음: " + tp);
            mat.mainTexture = tex;
        }
        if (tune != null) tune(mat);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Gradient BlinkAlpha()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] {
                new GradientAlphaKey(0.00f, 0.00f),
                new GradientAlphaKey(1.00f, 0.10f),
                new GradientAlphaKey(0.12f, 0.28f),
                new GradientAlphaKey(1.00f, 0.46f),
                new GradientAlphaKey(0.18f, 0.64f),
                new GradientAlphaKey(0.90f, 0.80f),
                new GradientAlphaKey(0.00f, 1.00f),
            });
        return g;
    }

    static ParticleSystem MakePS(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "fx");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        GameObjectUtility.SetStaticEditorFlags(go, 0);   // 파티클은 절대 static 아님
        return go.AddComponent<ParticleSystem>();
    }

    // ── 반딧불 ────────────────────────────────────────────────────────
    [MenuItem("Tools/Pyrite/E. Build Fireflies and Water Glow &#9")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();

        GameObject flora = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "FlowerField") flora = r;
        if (flora == null) { Debug.LogError("[Pyrite] FlowerField 못 찾음 — 먼저 Alt+Shift+0"); return; }

        GameObject root = null;
        foreach (var r in scene.GetRootGameObjects()) if (r.name == "LightFX") root = r;
        if (root != null) Undo.DestroyObjectImmediate(root);
        root = new GameObject("LightFX");
        Undo.RegisterCreatedObjectUndo(root, "lightfx");
        GameObjectUtility.SetStaticEditorFlags(root, 0);

        var matFly = EnsureMat("M_Firefly", "Mobile/Particles/Additive", "T_Firefly", null);
        if (matFly == null) return;

        var flyRoot = new GameObject("Fireflies");
        Undo.RegisterCreatedObjectUndo(flyRoot, "fireflies");
        flyRoot.transform.SetParent(root.transform, false);

        var tiles = new List<MeshRenderer>();
        foreach (Transform t in flora.transform)
        {
            var mr = t.GetComponent<MeshRenderer>();
            if (mr != null) tiles.Add(mr);
        }
        if (tiles.Count == 0) { Debug.LogError("[Pyrite] 꽃 타일 없음"); return; }

        int meshShape = 0, boxShape = 0;
        for (int i = 0; i < tiles.Count; i++)
        {
            var mr = tiles[i];
            var mf = mr.GetComponent<MeshFilter>();
            bool readable = mf != null && mf.sharedMesh != null && mf.sharedMesh.isReadable;

            var ps = MakePS(flyRoot.transform, "FF_" + mr.name, mr.bounds.center);

            var main = ps.main;
            main.duration = 12f;
            main.loop = true;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.06f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.24f);
            main.startColor = new ParticleSystem.MinMaxGradient(GOLD_A, GOLD_B);
            main.maxParticles = 55;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 4.5f;

            var sh = ps.shape;
            sh.enabled = true;
            if (readable)
            {
                sh.shapeType = ParticleSystemShapeType.MeshRenderer;
                sh.meshRenderer = mr;
                sh.meshShapeType = ParticleSystemMeshShapeType.Triangle;
                sh.useMeshColors = false;
                sh.normalOffset = 0.04f;
                sh.randomDirectionAmount = 1f;
                meshShape++;
            }
            else
            {
                // 메시를 못 읽으면 타일 바운즈 바닥에 얇은 박스로 대체
                sh.shapeType = ParticleSystemShapeType.Box;
                var b = mr.bounds;
                ps.transform.position = new Vector3(b.center.x, b.min.y + 0.30f, b.center.z);
                sh.scale = new Vector3(b.size.x, 0.6f, b.size.z);
                boxShape++;
            }

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);
            vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.24f);   // 천천히 떠오른다
            vel.z = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);

            var nz = ps.noise;
            nz.enabled = true;
            nz.quality = ParticleSystemNoiseQuality.Medium;
            nz.strength = new ParticleSystem.MinMaxCurve(0.18f, 0.30f);
            nz.frequency = 0.17f;
            nz.scrollSpeed = 0.10f;
            nz.damping = true;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(BlinkAlpha());

            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = matFly;
            pr.alignment = ParticleSystemRenderSpace.View;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortingFudge = -30f;
            pr.maxParticleSize = 0.05f;    // 가까이서 화면을 덮지 않게
            pr.lightProbeUsage = LightProbeUsage.Off;
            pr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        // ── 수면 발광 판 ────────────────────────────────────────────────
        var matShim = EnsureMat("M_WaterShimmer", "Pyrite/WaterShimmer", "T_Shimmer", m =>
        {
            m.SetColor("_Tint", new Color(0.52f, 0.74f, 1.00f, 1f));
            m.SetFloat("_Gain", 1.0f);
            m.SetFloat("_Scale1", 7f);
            m.SetFloat("_Scale2", 11f);
            m.SetVector("_Speed1", new Vector4(0.012f, 0.007f, 0, 0));
            m.SetVector("_Speed2", new Vector4(-0.008f, 0.011f, 0, 0));
            m.SetFloat("_EdgeIn", 0.34f);
            m.SetFloat("_EdgeOut", 0.485f);
        });
        if (matShim == null) return;

        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Undo.RegisterCreatedObjectUndo(plane, "shimmer");
        plane.name = "WaterShimmer";
        var pc = plane.GetComponent<Collider>();
        if (pc != null) Object.DestroyImmediate(pc);
        plane.transform.SetParent(root.transform, false);
        plane.transform.position = new Vector3(0f, 0.03f, -14f);
        plane.transform.localScale = new Vector3(11.6f, 1f, 11.6f);
        var prr = plane.GetComponent<MeshRenderer>();
        prr.sharedMaterial = matShim;
        prr.shadowCastingMode = ShadowCastingMode.Off;
        prr.receiveShadows = false;
        prr.lightProbeUsage = LightProbeUsage.Off;
        prr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        GameObjectUtility.SetStaticEditorFlags(plane, 0);

        // ── 수면 위 반짝이 모트 ─────────────────────────────────────────
        var mote = MakePS(root.transform, "WaterMotes", new Vector3(0f, 0.10f, -14f));
        mote.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // Circle을 XZ 평면에 눕힌다
        {
            var main = mote.main;
            main.duration = 14f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 16f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0.02f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.80f, 1.00f), new Color(0.95f, 0.98f, 1.00f));
            main.maxParticles = 700;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = mote.emission; em.enabled = true; em.rateOverTime = 55f;

            var sh = mote.shape;
            sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Circle;
            sh.radius = 56f;
            sh.radiusThickness = 0.46f;   // 반경 30~56 고리 — 사람이 보는 물가에 몰아준다
            sh.arcMode = ParticleSystemShapeMultiModeValue.Random;

            var vel = mote.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            vel.y = new ParticleSystem.MinMaxCurve(0.00f, 0.05f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);

            var col = mote.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(BlinkAlpha());

            var pr = mote.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = matFly;
            pr.alignment = ParticleSystemRenderSpace.View;
            pr.shadowCastingMode = ShadowCastingMode.Off;
            pr.receiveShadows = false;
            pr.sortingFudge = -30f;
            pr.maxParticleSize = 0.05f;
            pr.lightProbeUsage = LightProbeUsage.Off;
            pr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(string.Format(
            "[Pyrite] 반딧불 {0}개 시스템 (메시형 {1} / 박스대체 {2}) · 전부 금색 · 최대 {3}마리"
            + " + 수면 시머판 + 물 모트 700",
            tiles.Count, meshShape, boxShape, tiles.Count * 55));
    }
}
#endif
