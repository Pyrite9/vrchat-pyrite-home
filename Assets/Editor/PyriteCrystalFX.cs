// Tools ▸ Pyrite ▸ Y4. Crystal Fireflies + Lights
//  [B] 랜드마크 결정 무더기 주변에 반딧불 — 결정이 은은히 빛나는 "이유"를 화면 안에 둔다.
//      호수 건너(~100m)에서도 보이도록 최소 화면 크기 + 블룸을 받는 HDR 밝기.
//  [C] 무더기마다 캠프 쪽 옆에 작은 따뜻한 점광원 — 금속 면에 하이라이트, 발밑에 빛 웅덩이.
//      절벽 광맥 쪽 CrystalLight 4개는 끈다(빛마다 주변을 한 번 더 그려서 비용이 쌓인다).
//  재실행 안전. E(LightFX 재생성)나 J(CrystalLights 재생성)를 다시 돌렸다면 이것도 다시 돌린다.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteCrystalFX
{
    const string MAT = "Assets/Materials/M_FireflyHDR.mat";
    static readonly Vector3 CAMP = new Vector3(-10.5f, 0f, 54.5f);
    public static readonly float[] LIGHT_I = { 0.0f, 1.3f, 0.0f };   // 노을 / 밤 / 새벽

    [MenuItem("Tools/Pyrite/Y4. Crystal Fireflies + Lights", false, 293)]
    public static void Run()
    {
        var log = new StringBuilder("[Y4] ");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var crystals = roots.FirstOrDefault(g => g.name == "Crystals");
        var fx = roots.FirstOrDefault(g => g.name == "LightFX");
        var lightRoot = roots.FirstOrDefault(g => g.name == "CrystalLights");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (crystals == null || fx == null || lightRoot == null || tod == null) { Debug.LogError("[Y4] Crystals/LightFX/CrystalLights/ToD 중 없음"); return; }
        var t = Terrain.activeTerrain;
        float G(Vector3 p) => t != null ? t.SampleHeight(p) + t.transform.position.y : 0f;

        // ── 머티리얼: 레거시 가산 파티클 (_TintColor × 2 → 블룸 임계 0.9 를 넘는다)
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        var src = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_Firefly.mat");
        if (mat == null)
        {
            mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive"));
            AssetDatabase.CreateAsset(mat, MAT);
        }
        if (src != null && src.mainTexture != null) mat.mainTexture = src.mainTexture;
        mat.SetColor("_TintColor", new Color(1.00f, 0.90f, 0.60f, 1f));
        EditorUtility.SetDirty(mat);

        // ── 무더기 목록 (활성 결정만)
        var clusters = new List<Bounds>();
        foreach (Transform c in crystals.transform)
        {
            if (!c.name.StartsWith("Landmark_")) continue;
            var rs = c.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            clusters.Add(b);
        }

        // ── [B] 반딧불
        foreach (var old in fx.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("FF_Crystal_")).ToList())
            Undo.DestroyObjectImmediate(old.gameObject);
        var newPs = new List<ParticleSystem>();
        for (int i = 0; i < clusters.Count; i++)
        {
            var b = clusters[i];
            float ext = Mathf.Max(b.extents.x, b.extents.z);
            float rad = Mathf.Clamp(ext * 0.75f, 3.0f, 6.0f);
            var c = new Vector3(b.center.x, G(b.center) + 1.6f, b.center.z);

            var go = new GameObject("FF_Crystal_" + (i + 1));
            Undo.RegisterCreatedObjectUndo(go, "crystal ff");
            go.transform.SetParent(fx.transform, false);
            go.transform.position = c;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 10f; main.loop = true; main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.01f, 0.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.14f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.74f, 0.24f), new Color(1f, 0.93f, 0.58f));
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = true;

            var em = ps.emission; em.enabled = true; em.rateOverTime = 3.0f;
            var sh = ps.shape; sh.enabled = true;
            sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = rad; sh.radiusThickness = 1f;
            sh.scale = new Vector3(1f, 0.55f, 1f);                     // 납작한 구 — 지면 가까이

            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            vel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.08f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            var nz = ps.noise; nz.enabled = true; nz.quality = ParticleSystemNoiseQuality.Medium;
            nz.strength = new ParticleSystem.MinMaxCurve(0.18f, 0.30f); nz.frequency = 0.2f; nz.scrollSpeed = 0.1f; nz.damping = true;

            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0.6f, 0.45f),
                              new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.renderMode = ParticleSystemRenderMode.Billboard;
            pr.sharedMaterial = mat;
            pr.alignment = ParticleSystemRenderSpace.View;
            pr.shadowCastingMode = ShadowCastingMode.Off; pr.receiveShadows = false;
            pr.minParticleSize = 0.0045f;       // 1440p 에서 약 6px — 호수 건너에서도 점으로 남는다
            pr.maxParticleSize = 0.05f;
            pr.lightProbeUsage = LightProbeUsage.Off; pr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            newPs.Add(ps);
            log.Append(go.name).Append(" r=").Append(rad.ToString("F1")).Append(" @").Append(c.ToString("F0")).Append(" | ");
        }

        // ToD 반딧불 목록에 편입 (프리셋별 배율·색을 같이 받는다)
        var ff = (tod.fireflies ?? new ParticleSystem[0]).Where(p => p != null && !p.name.StartsWith("FF_Crystal_")).ToList();
        ff.AddRange(newPs);
        tod.fireflies = ff.ToArray();

        // ── [C] 작은 빛 — 기존 CrystalLight 재사용
        var lights = lightRoot.GetComponentsInChildren<Light>(true).OrderBy(l => l.name).ToList();
        var used = new List<Light>();
        for (int i = 0; i < lights.Count; i++)
        {
            var L = lights[i];
            Undo.RecordObject(L.transform, "crystal light"); Undo.RecordObject(L, "crystal light"); Undo.RecordObject(L.gameObject, "crystal light");
            if (i < clusters.Count)
            {
                var b = clusters[i];
                var dir = CAMP - b.center; dir.y = 0f; dir.Normalize();
                float ext = new Vector2(b.extents.x, b.extents.z).magnitude * 0.7f;
                var p = b.center + dir * (ext + 1.5f);
                p.y = G(p) + 1.5f;
                L.transform.position = p;
                L.color = new Color(1.00f, 0.78f, 0.40f);
                L.range = 6f;
                L.shadows = LightShadows.None;
                L.lightmapBakeType = LightmapBakeType.Realtime;
                L.renderMode = LightRenderMode.ForcePixel;           // 금속 하이라이트는 픽셀 라이트여야 맺힌다
                L.gameObject.SetActive(true);
                used.Add(L);
                log.Append(L.name).Append(" @").Append(p.ToString("F1")).Append(" | ");
            }
            else L.gameObject.SetActive(false);                        // 절벽 광맥 쪽은 끈다
        }
        tod.crystalLights = used.ToArray();
        tod.crystalLightIntensity = (float[])LIGHT_I.Clone();

        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText("Assets/_preview/crystalfx.txt", log.ToString());

        PyriteViews.CaptureSet("lm1 lm2 camp_lake camp_left shore", new[] { 1 });
    }
}
#endif
