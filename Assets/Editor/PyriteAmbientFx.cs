// Tools ▸ Pyrite2 ▸ Z34b. Build Ambient FX / Z34c. Ambient FX Revert / Z34d. Ambient FX Renders
//  새 루트 AmbientFX 아래:
//   - 별똥별  Meteors   : 서버 시각 기준 약 1분마다 하나, 밤에만 (PyriteMeteors, 동기화 없이 모두 같은 순간)
//   - 물안개  LakeMist  : 새벽 04:18~08:00, 가장 짙은 때 05:18~06:48 (PyriteMist), 수면 위 낮은 안개 판 5장(Pyrite/LowFog)
//   - 불티    CampEmbers: 모닥불에서 튀어 오르는 불꽃 (Udon 없음)
//   - 물수제비 SkipStones: 부두 끝 쟁반 + 돌 5개 (PyriteSkipStone) + 물보라·물결 고리 + 호수 파문(PyriteLakeRipple.AddDrop)
//  재실행 안전: AmbientFX 를 지우고 다시 만든다. Z34c 는 AmbientFX 만 지운다
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using VRC.SDK3.Components;
using VRC.SDKBase;

public static class PyriteAmbientFx
{
    const string DIR = "Assets/Props";
    const string ROOT = "AmbientFX";
    const int PICKUP_LAYER = 13;
    static readonly Vector3 FIRE = new Vector3(-10.5f, 2.05f, 51.5f);
    static readonly Vector2 MASK_MIN = new Vector2(-62f, -76f);          // 안개 판·마스크 범위 (WaterWalk x −60..60, z −74..46 + 여유)
    const float MASK_SIZE = 124f;
    const float WATER_Y = 0.05f;
    static readonly Vector3 TRAY = new Vector3(-9.40f, 0f, 31.42f);     // 부두 끝(z 30.9) 오른쪽, 끝 랜턴(-10.7, 31.3) 반대편
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite2/Z34b. Build Ambient FX %&#8", false, 81)]   // Ctrl+Alt+Shift+8
    public static void Build()
    {
        sb = new StringBuilder("[Z34b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { BuildInner(); Renders(); sb.AppendLine("RESULT: DONE"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite2/Z34c. Ambient FX Revert", false, 82)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z34c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var old = FindRoot();
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("AmbientFX 삭제"); } else sb.AppendLine("AmbientFX 없음");
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush();
    }

    [MenuItem("Tools/Pyrite2/Z34d. Ambient FX Renders", false, 83)]
    public static void RendersMenu()
    {
        sb = new StringBuilder("[Z34d] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Renders(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static GameObject FindRoot() { return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == ROOT); }

    static void BuildInner()
    {
        foreach (var d in new[] { DIR, DIR + "/Meshes", DIR + "/Materials", DIR + "/Textures" })
            if (!AssetDatabase.IsValidFolder(d)) AssetDatabase.CreateFolder(Path.GetDirectoryName(d).Replace('\\', '/'), Path.GetFileName(d));
        var old = FindRoot();
        if (old != null) { Object.DestroyImmediate(old); sb.AppendLine("이전 AmbientFX 지움"); }
        var root = new GameObject(ROOT);
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var ripple = Object.FindObjectOfType<PyriteLakeRipple>();
        var water = GameObject.Find("Water/WaterWalk")?.GetComponent<Collider>();
        sb.AppendLine("cycle " + (cyc != null) + " ripple " + (ripple != null) + " waterWalk " + (water != null));

        var dot = Tex("T_SoftDot", 64, (u, v) => { float r = Mathf.Clamp01(1f - Mathf.Sqrt(u * u + v * v) * 2f); r = r * r * (3f - 2f * r); return new Color(1, 1, 1, r); }, true);
        var ringTex = Tex("T_RippleRing", 128, (u, v) => { float r = Mathf.Sqrt(u * u + v * v) * 2f; float a = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.07f, 2f)) + 0.35f * Mathf.Exp(-Mathf.Pow((r - 0.62f) / 0.05f, 2f)); return new Color(1, 1, 1, Mathf.Clamp01(a) * (r < 1f ? 1f : 0f)); }, true);
        var mistTex = Tex("T_Mist", 256, (u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v) * 2f;
            float fall = Mathf.Clamp01(1f - r); fall = fall * fall * (3f - 2f * fall);
            float n = 0f, amp = 0.5f, f = 3f;
            for (int o = 0; o < 4; o++) { n += amp * Mathf.PerlinNoise((u + 0.5f) * f + 17.3f, (v + 0.5f) * f + 5.1f); amp *= 0.5f; f *= 2f; }
            return new Color(1, 1, 1, Mathf.Clamp01(fall * (0.35f + 0.9f * n)));
        }, true);

        var trailTex = Tex("T_TrailSoft", 64, (u, v) => { float a = Mathf.Clamp01(1f - Mathf.Abs(v) * 2f); a = a * a * (3f - 2f * a); return new Color(1, 1, 1, a); }, true);   // 폭 방향으로 부드럽게
        var mGlowTrail = Mat("M_FxMeteor", "Pyrite/AddGlow", trailTex, new Color(2.4f, 2.6f, 3.2f, 1f));
        var mGlowEmber = Mat("M_FxEmber", "Pyrite/AddGlow", dot, new Color(3.2f, 2.0f, 1.1f, 1f));
        var mMist = Mat("M_FxMist", "Pyrite/SoftAlpha", mistTex, new Color(0.6f, 0.66f, 0.76f, 1f)); mMist.SetFloat("_Alpha", 0f); mMist.SetFloat("_NearFade", 8f);
        var mDrop = Mat("M_FxDroplet", "Pyrite/SoftAlpha", dot, new Color(0.86f, 0.9f, 0.96f, 1f)); mDrop.SetFloat("_Alpha", 1f); mDrop.SetFloat("_NearFade", 0f);
        var mRing = Mat("M_FxRing", "Pyrite/SoftAlpha", ringTex, new Color(0.9f, 0.94f, 1f, 1f)); mRing.SetFloat("_Alpha", 1f); mRing.SetFloat("_NearFade", 0f);

        // 1) 별똥별
        {
            var go = new GameObject("Meteors"); go.transform.SetParent(root.transform, false);
            var head = new GameObject("Head"); head.transform.SetParent(go.transform, false);
            var tr = head.AddComponent<TrailRenderer>();
            tr.time = 0.8f; tr.minVertexDistance = 1f; tr.widthMultiplier = 6f;   // 0.32 s × 3.2 m 는 Play 실측에서 점 몇 px 로만 보였다
            tr.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1f, 1f, 1f), 0f), new GradientColorKey(new Color(0.75f, 0.85f, 1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) });
            tr.colorGradient = g;
            tr.sharedMaterial = mGlowTrail; tr.shadowCastingMode = ShadowCastingMode.Off; tr.receiveShadows = false;
            tr.alignment = LineAlignment.View; tr.textureMode = LineTextureMode.Stretch; tr.emitting = false;
            head.SetActive(false);
            var m = UdonSharpUndo.AddComponent<PyriteMeteors>(go);
            m.cycle = cyc; m.head = head.transform; m.trail = tr;
            {   // 진단 글자판 (캠프 모닥불 옆, 머리 쪽으로 돈다). 확인 끝나면 debugText 를 비우고 지운다
                var dtGo = new GameObject("MeteorDebugText"); dtGo.transform.SetParent(go.transform, false);
                dtGo.transform.position = new Vector3(-9.2f, 3.2f, 51.2f);
                var tm = dtGo.AddComponent<TMPro.TextMeshPro>();   // TextMesh.text 는 Udon 미노출 → TMP
                tm.fontSize = 1.1f; tm.alignment = TMPro.TextAlignmentOptions.Left; tm.color = new Color(1f, 0.95f, 0.6f);
                tm.rectTransform.sizeDelta = new Vector2(3.2f, 1.4f); tm.enableWordWrapping = false;
                tm.text = "meteor debug";
                m.debugText = tm; m.debugTf = dtGo.transform;
            }
            UdonSharpEditorUtility.CopyProxyToUdon(m); EditorUtility.SetDirty(m);
            sb.AppendLine("meteors: interval " + m.interval + " s ± " + m.jitter + ", radius " + m.radius + " m, trail " + tr.time + " s × " + tr.widthMultiplier + " m");
        }

        // 2) 물안개 — 낮은 안개(수면 위 0.06~0.66 m 겹친 판 5장, Pyrite/LowFog). 1차 파티클 판은 "막처럼 보임"(관리자) → 교체
        {
            var go = new GameObject("LakeMist"); go.transform.SetParent(root.transform, false);
            var noise = TileNoise("T_FogNoise", 256);
            var mask = LakeMask("T_FogLakeMask", 128, out int wet);
            var mFog = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Materials/M_FxLowFog.mat");
            var fogSh = Shader.Find("Pyrite/LowFog");
            if (mFog == null) { mFog = new Material(fogSh); AssetDatabase.CreateAsset(mFog, DIR + "/Materials/M_FxLowFog.mat"); }
            mFog.shader = fogSh;
            mFog.SetTexture("_Noise", noise); mFog.SetTexture("_Mask", mask);
            mFog.SetVector("_MaskRect", new Vector4(MASK_MIN.x, MASK_MIN.y, MASK_SIZE, MASK_SIZE));
            mFog.SetColor("_Color", new Color(0.6f, 0.66f, 0.76f, 1f)); mFog.SetFloat("_Alpha", 0f);
            mFog.SetFloat("_Density", 0.2f); mFog.SetFloat("_Scale", 38f); mFog.SetFloat("_Threshold", 0.42f); mFog.SetFloat("_Contrast", 2.4f);
            mFog.SetFloat("_NearFade", 2.5f); EditorUtility.SetDirty(mFog);
            float[] hs = { 0.06f, 0.18f, 0.32f, 0.48f, 0.66f };
            float[] ws = { 1f, 0.85f, 0.62f, 0.4f, 0.2f };
            var v = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int l = 0; l < hs.Length; l++)
            {
                int b = v.Count; float y = WATER_Y + hs[l];
                v.Add(new Vector3(MASK_MIN.x, y, MASK_MIN.y)); v.Add(new Vector3(MASK_MIN.x + MASK_SIZE, y, MASK_MIN.y));
                v.Add(new Vector3(MASK_MIN.x, y, MASK_MIN.y + MASK_SIZE)); v.Add(new Vector3(MASK_MIN.x + MASK_SIZE, y, MASK_MIN.y + MASK_SIZE));
                var col = new Color(l / (float)hs.Length, 0, 0, ws[l]); for (int q = 0; q < 4; q++) c.Add(col);
                t.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
            }
            var fm = new Mesh(); fm.SetVertices(v); fm.SetColors(c); fm.SetTriangles(t, 0); fm.RecalculateNormals(); fm.RecalculateBounds();
            fm = SaveMesh(fm, "LakeLowFog");
            go.AddComponent<MeshFilter>().sharedMesh = fm;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mFog;
            r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false; r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.enabled = false;
            var mist = UdonSharpUndo.AddComponent<PyriteMist>(go);
            mist.cycle = cyc; mist.mistRenderer = r;
            UdonSharpEditorUtility.CopyProxyToUdon(mist); EditorUtility.SetDirty(mist);
            sb.AppendLine(string.Format("mist: low fog 5 layers y +{0}..+{1} m over {2} m², lake mask wet {3}/{4} texels, density {5}, {6}→{7} … {8}→{9}h, max α {10}",
                hs[0], hs[hs.Length - 1], MASK_SIZE * MASK_SIZE, wet, 128 * 128, 0.2f, mist.inStart, mist.inEnd, mist.outStart, mist.outEnd, mist.maxAlpha));
        }

        // 3) 불티
        {
            var go = new GameObject("CampEmbers"); go.transform.SetParent(root.transform, false);
            go.transform.position = FIRE; go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.prewarm = true; main.playOnAwake = true; main.duration = 5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
            main.gravityModifier = -0.08f; main.maxParticles = 60; main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 6f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0.5f, 3, 7, 1, 2.7f) });
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 18f; sh.radius = 0.12f;
            var no = ps.noise; no.enabled = true; no.strength = 0.35f; no.frequency = 1.4f; no.scrollSpeed = 0.6f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var cg = new Gradient();
            cg.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.5f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.4f), new GradientColorKey(new Color(0.8f, 0.15f, 0.02f), 1f) },
                       new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
            col.color = cg;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.06f; r.lengthScale = 1.2f;
            r.sharedMaterial = mGlowEmber; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            sb.AppendLine("embers at " + FIRE + ": 6/s + 3~7 every 2.7 s, life 1~2.4 s");
        }

        // 4) 물수제비
        {
            var go = new GameObject("SkipStones"); go.transform.SetParent(root.transform, false);
            float deck = Ground(TRAY);
            go.transform.position = new Vector3(TRAY.x, deck, TRAY.z);
            var mWood = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Materials/M_PropWoodDark.mat");
            var cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            const float TW = 0.30f, TD = 0.20f, TH = 0.045f;
            var trayParts = new List<(Mesh, Matrix4x4, Material)> {
                (cube, Matrix4x4.TRS(new Vector3(0, 0.008f, 0), Quaternion.identity, new Vector3(TW, 0.016f, TD)), mWood),
                (cube, Matrix4x4.TRS(new Vector3(0, TH * 0.5f, TD * 0.5f - 0.006f), Quaternion.identity, new Vector3(TW, TH, 0.012f)), mWood),
                (cube, Matrix4x4.TRS(new Vector3(0, TH * 0.5f, -TD * 0.5f + 0.006f), Quaternion.identity, new Vector3(TW, TH, 0.012f)), mWood),
                (cube, Matrix4x4.TRS(new Vector3(TW * 0.5f - 0.006f, TH * 0.5f, 0), Quaternion.identity, new Vector3(0.012f, TH, TD)), mWood),
                (cube, Matrix4x4.TRS(new Vector3(-TW * 0.5f + 0.006f, TH * 0.5f, 0), Quaternion.identity, new Vector3(0.012f, TH, TD)), mWood),
            };
            var trayGo = new GameObject("Tray"); trayGo.transform.SetParent(go.transform, false); trayGo.transform.localRotation = Quaternion.Euler(0, 12f, 0);
            var ci = trayParts.Select(p => new CombineInstance { mesh = p.Item1, transform = p.Item2 }).ToArray();
            var trayMesh = new Mesh(); trayMesh.CombineMeshes(ci, true, true); trayMesh.RecalculateBounds();
            trayGo.AddComponent<MeshFilter>().sharedMesh = SaveMesh(trayMesh, "SkipTray");
            trayGo.AddComponent<MeshRenderer>().sharedMaterial = mWood;
            var tb = trayGo.AddComponent<BoxCollider>(); tb.center = new Vector3(0, 0.008f, 0); tb.size = new Vector3(TW, 0.016f, TD);
            foreach (var sgn in new[] { -1f, 1f })
            {
                var w = trayGo.AddComponent<BoxCollider>(); w.center = new Vector3(0, TH * 0.5f, sgn * (TD * 0.5f - 0.006f)); w.size = new Vector3(TW, TH, 0.012f);
                var w2 = trayGo.AddComponent<BoxCollider>(); w2.center = new Vector3(sgn * (TW * 0.5f - 0.006f), TH * 0.5f, 0); w2.size = new Vector3(0.012f, TH, TD);
            }

            // 물보라·물결 고리 (돌 다섯이 같이 쓴다)
            var splash = FxPs(go.transform, "SkipSplash", mDrop, ParticleSystemRenderMode.Billboard, 60);
            { var main = splash.main; main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f); main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f); main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.06f); main.gravityModifier = 1f;
              var sh = splash.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 35f; sh.radius = 0.04f; splash.transform.rotation = Quaternion.Euler(-90f, 0, 0);
              var col = splash.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = g; }
            var ring = FxPs(go.transform, "SkipRing", mRing, ParticleSystemRenderMode.HorizontalBillboard, 20);
            { var main = ring.main; main.startLifetime = 1.4f; main.startSpeed = 0f; main.startSize = 0.3f;
              var sh = ring.shape; sh.enabled = false;
              var sz = ring.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 6.5f));
              var col = ring.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }); col.color = g; }

            // 돌 다섯
            var stoneMats = new[] {
                StoneMat("M_FxStoneA", new Color(0.44f, 0.44f, 0.42f)), StoneMat("M_FxStoneB", new Color(0.33f, 0.34f, 0.37f)), StoneMat("M_FxStoneC", new Color(0.52f, 0.48f, 0.43f)) };
            var stoneMesh = SaveMesh(Lathe(new[] { V(0, -0.009f), V(0.022f, -0.008f), V(0.034f, -0.003f), V(0.036f, 0f), V(0.034f, 0.004f), V(0.022f, 0.009f), V(0, 0.010f) }, 18), "SkipStone");
            var pkTemplate = GameObject.Find("Camp/Lantern_Carry")?.GetComponent<VRCPickup>();
            var rnd = new System.Random(11);
            for (int i = 0; i < 5; i++)
            {
                float sx = 0.95f + (float)rnd.NextDouble() * 0.25f, sz2 = 0.8f + (float)rnd.NextDouble() * 0.15f;
                var home = new GameObject("Home_" + i).transform; home.SetParent(trayGo.transform, false);
                home.localPosition = new Vector3(-0.09f + (i % 3) * 0.09f, 0.03f + (i / 3) * 0.018f, i < 3 ? -0.04f : 0.045f);
                home.localRotation = Quaternion.Euler(0, rnd.Next(0, 360), 0);
                var s = new GameObject("Stone_" + i); s.layer = PICKUP_LAYER;
                s.transform.SetParent(go.transform, false);
                s.transform.SetPositionAndRotation(home.position, home.rotation);
                var vis = new GameObject("Mesh"); vis.transform.SetParent(s.transform, false); vis.transform.localScale = new Vector3(sx, 1f, sz2);
                vis.AddComponent<MeshFilter>().sharedMesh = stoneMesh;
                vis.AddComponent<MeshRenderer>().sharedMaterial = stoneMats[i % 3];
                var rb = s.AddComponent<Rigidbody>(); rb.mass = 0.15f; rb.drag = 0.05f; rb.angularDrag = 0.2f; rb.useGravity = true; rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate; rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var bc = s.AddComponent<BoxCollider>(); bc.size = new Vector3(0.072f * sx, 0.02f, 0.072f * sz2);
                var pk = s.AddComponent<VRCPickup>();
                if (pkTemplate != null) EditorUtility.CopySerialized(pkTemplate, pk);
                pk.ExactGrip = null; pk.ExactGun = null; pk.orientation = VRC_Pickup.PickupOrientation.Any;
                pk.AutoHold = VRC_Pickup.AutoHoldMode.AutoDetect;    // 데스크톱은 자동으로 들고(우클릭 길게 = 던지기), VR 은 잡는 동안만 → 놓으면 던져진다
                pk.InteractionText = "Skipping stone"; pk.UseText = "";
                pk.ThrowVelocityBoostScale = 1.5f; pk.ThrowVelocityBoostMinSpeed = 1f;
                pk.pickupable = true; EditorUtility.SetDirty(pk);
                s.AddComponent<VRCObjectSync>().AllowCollisionOwnershipTransfer = false;
                var ss = UdonSharpUndo.AddComponent<PyriteSkipStone>(s);
                ss.home = home; ss.waterCollider = water; ss.splash = splash; ss.ring = ring; ss.ripple = ripple;
                UdonSharpEditorUtility.CopyProxyToUdon(ss); EditorUtility.SetDirty(ss);
            }
            sb.AppendLine(string.Format("skip stones: tray at {0} (deck y {1:F3}), 5 stones, throw boost 1.5, splash/ring shared, stone tris {2}", go.transform.position.ToString("F2"), deck, stoneMesh.triangles.Length / 3));
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    // ───────── 렌더 ─────────
    static void Renders()
    {
        var root = FindRoot(); if (root == null) { sb.AppendLine("AmbientFX 없음"); return; }
        Directory.CreateDirectory("Assets/_preview/fx/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var mistGo = root.transform.Find("LakeMist");
        var mistR = mistGo.GetComponent<Renderer>();
        var mistMat = mistR.sharedMaterial;
        var ember = root.transform.Find("CampEmbers").GetComponent<ParticleSystem>();
        var head = root.transform.Find("Meteors/Head");
        var tr = head.GetComponent<TrailRenderer>();
        var splash = root.transform.Find("SkipStones/SkipSplash").GetComponent<ParticleSystem>();
        var ring = root.transform.Find("SkipStones/SkipRing").GetComponent<ParticleSystem>();
        var mistComp = mistGo.GetComponent<PyriteMist>();
        try
        {
            // 물안개: 05:50(가장 짙음·푸른 회색), 06:40(해 뜬 뒤 따뜻한 색), 07:40(옅어짐)
            mistR.enabled = true;
            foreach (var (h, a, w) in new[] { (5.8f, 1f, 0f), (6.6f, 1f, 1f), (7.6f, 0.2f, 1f) })
            {
                cyc.ResetCache(); cyc.EvaluateAt(h);
                mistMat.SetColor("_Color", Color.Lerp(mistComp.cool, mistComp.warm, w)); mistMat.SetFloat("_Alpha", a * mistComp.maxAlpha);
                Shot(cam, new Vector3(-10.6f, 3.4f, 55f), new Vector3(-6f, 0.5f, 10f), 60f, string.Format("mist_{0:0.0}.png", h));
                Shot(cam, new Vector3(-10.2f, 1.2f, 38f), new Vector3(-4f, 0.4f, 0f), 60f, string.Format("mist_dock_{0:0.0}.png", h));
                Shot(cam, new Vector3(-4f, 1.65f, 12f), new Vector3(2f, 0.6f, -20f), 60f, string.Format("mist_onwater_{0:0.0}.png", h));
            }
            mistMat.SetFloat("_Alpha", 0f); mistR.enabled = false;
            // 별똥별: 22시, 하늘 방위 200° 고도 50° 쪽으로 궤적을 찍어 둔다
            cyc.ResetCache(); cyc.EvaluateAt(22f);
            head.gameObject.SetActive(true);
            var met = root.GetComponentInChildren<PyriteMeteors>();
            System.Func<float, float, Vector3> D = (el, az) => { float e = el * Mathf.Deg2Rad, a2 = az * Mathf.Deg2Rad; return new Vector3(Mathf.Sin(a2) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a2) * Mathf.Cos(e)); };
            var pts = Enumerable.Range(0, 24).Select(i => met.center + Vector3.Slerp(D(40f, 172f), D(31f, 190f), i / 23f) * met.radius).ToArray();
            tr.emitting = true; tr.Clear(); tr.AddPositions(pts); head.position = pts[pts.Length - 1];   // emitting=false 면 AddPositions 가 렌더되지 않음(Z34e 실측)
            var eye = new Vector3(-10.6f, 3.65f, 53.8f);     // 캠프에 선 눈높이, 호수 쪽을 조금 올려다본 데스크톱 화면(FOV 60)
            Shot(cam, eye, eye + D(14f, 180f), 60f, "meteor_22.png");
            var eyeD = new Vector3(-9.6f, 2.15f, 31.5f);     // 부두 끝
            Shot(cam, eyeD, eyeD + D(14f, 180f), 60f, "meteor_22_dock.png");
            tr.emitting = false; tr.Clear(); head.gameObject.SetActive(false);
            // 불티: 21시
            cyc.ResetCache(); cyc.EvaluateAt(21f);
            ember.Simulate(4f, true, true);
            Shot(cam, new Vector3(-10.5f, 2.9f, 53.6f), FIRE + Vector3.up * 0.6f, 50f, "embers_21.png");
            ember.Clear(); ember.Play();
            // 물수제비: 쟁반 + 부두 앞 물 위 물보라
            cyc.ResetCache(); cyc.EvaluateAt(13f);
            var tray = root.transform.Find("SkipStones");
            Shot(cam, tray.position + new Vector3(0.35f, 0.45f, 0.45f), tray.position + Vector3.up * 0.02f, 45f, "stones_tray.png");
            var sp = new Vector3(-8f, 0.05f, 27.5f);
            splash.transform.position = sp; splash.Emit(12); ring.transform.position = sp + Vector3.up * 0.01f; ring.Emit(1);
            splash.Simulate(0.18f, true, false); ring.Simulate(0.35f, true, false);
            Shot(cam, new Vector3(-9.2f, 1.3f, 31.2f), sp, 45f, "stones_splash.png");
            splash.Clear(); ring.Clear();
            sb.AppendLine("  shots fx/mist_{5.8,6.6,7.6}, mist_dock_*, mist_onwater_*, meteor_22(_dock), embers_21, stones_tray, stones_splash");
        }
        finally
        {
            mistMat.SetFloat("_Alpha", 0f); mistR.enabled = false;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string name)
    {
        cam.fieldOfView = fov; cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        const int W = 1280, H = 720;
        var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes("Assets/_preview/fx/" + name, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }

    // ───────── 도우미 ─────────
    static ParticleSystem FxPs(Transform parent, string name, Material mat, ParticleSystemRenderMode mode, int max)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.loop = false; main.playOnAwake = false; main.maxParticles = max; main.simulationSpace = ParticleSystemSimulationSpace.World; main.duration = 1f;
        var em = ps.emission; em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>(); r.renderMode = mode; r.sharedMaterial = mat; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
        return ps;
    }

    static Material Mat(string name, string shader, Texture tex, Color c)
    {
        string p = DIR + "/Materials/" + name + ".mat";
        var sh = Shader.Find(shader);
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, p); }
        m.shader = sh; m.SetTexture("_MainTex", tex); m.SetColor("_Color", c);
        EditorUtility.SetDirty(m); return m;
    }

    static Material StoneMat(string name, Color c)
    {
        string p = DIR + "/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(p);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, p); }
        m.shader = Shader.Find("Standard"); m.color = c; m.SetFloat("_Metallic", 0f); m.SetFloat("_Glossiness", 0.28f);
        EditorUtility.SetDirty(m); return m;
    }

    static Texture2D Tex(string name, int n, System.Func<float, float, Color> f, bool alpha)
    {
        string p = DIR + "/Textures/" + name + ".png";
        var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) t.SetPixel(x, y, f((x + 0.5f) / n - 0.5f, (y + 0.5f) / n - 0.5f));
        t.Apply(); File.WriteAllBytes(p, t.EncodeToPNG()); Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        ti.alphaIsTransparency = alpha; ti.wrapMode = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.alphaSource = TextureImporterAlphaSource.FromInput;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    // 이음매 없는 값 노이즈(격자 주기 4·8·16·32, 가장자리 감김) → 0~1
    static Texture2D TileNoise(string name, int n)
    {
        var rnd = new System.Random(4242);
        int[] periods = { 4, 8, 16, 32 }; float[] amps = { 0.5f, 0.27f, 0.15f, 0.08f };
        var grids = periods.Select(pp => { var g = new float[pp, pp]; for (int a = 0; a < pp; a++) for (int b2 = 0; b2 < pp; b2++) g[a, b2] = (float)rnd.NextDouble(); return g; }).ToArray();
        var vals = new float[n, n]; float lo = 9, hi = -9;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float s2 = 0f;
            for (int o = 0; o < periods.Length; o++)
            {
                int pp = periods[o]; float fx = x * pp / (float)n, fy = y * pp / (float)n;
                int x0 = (int)fx, y0 = (int)fy; float tx = fx - x0, ty = fy - y0; tx = tx * tx * (3 - 2 * tx); ty = ty * ty * (3 - 2 * ty);
                var g = grids[o]; int x1 = (x0 + 1) % pp, y1 = (y0 + 1) % pp;
                s2 += amps[o] * Mathf.Lerp(Mathf.Lerp(g[x0, y0], g[x1, y0], tx), Mathf.Lerp(g[x0, y1], g[x1, y1], tx), ty);
            }
            vals[x, y] = s2; lo = Mathf.Min(lo, s2); hi = Mathf.Max(hi, s2);
        }
        return WriteTex(name, n, (x, y) => { float q = (vals[x, y] - lo) / (hi - lo); return new Color(q, q, q, 1f); }, TextureWrapMode.Repeat);
    }

    // 호수 마스크: 바닥(WaterWalk 제외)이 수면보다 낮으면 1, 물가 0.35 m 에 걸쳐 0 으로, 흐리게 3 번
    static Texture2D LakeMask(string name, int n, out int wet)
    {
        Physics.SyncTransforms();
        var water = GameObject.Find("Water/WaterWalk")?.GetComponent<Collider>();
        var m = new float[n, n]; wet = 0;
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
        {
            float wx = MASK_MIN.x + (x + 0.5f) / n * MASK_SIZE, wz = MASK_MIN.y + (y + 0.5f) / n * MASK_SIZE;
            float g = -99f;
            foreach (var h in Physics.RaycastAll(new Vector3(wx, 30f, wz), Vector3.down, 60f, 1 | (1 << 11), QueryTriggerInteraction.Ignore))
                if (h.collider != water && h.point.y > g) g = h.point.y;
            if (g < -98f) g = 5f;   // 바닥 없음(범위 밖) = 뭍 취급
            float q = Mathf.Clamp01((WATER_Y - g) / 0.35f);
            m[x, y] = q; if (q > 0.5f) wet++;
        }
        for (int it = 0; it < 3; it++)
        {
            var b = new float[n, n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float s2 = 0; int c2 = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) { int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue; s2 += m[xx, yy]; c2++; }
                b[x, y] = s2 / c2;
            }
            m = b;
        }
        var mm = m;
        return WriteTex(name, n, (x, y) => new Color(mm[x, y], mm[x, y], mm[x, y], 1f), TextureWrapMode.Clamp);
    }

    static Texture2D WriteTex(string name, int n, System.Func<int, int, Color> f, TextureWrapMode wrap)
    {
        string p = DIR + "/Textures/" + name + ".png";
        var tx = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) tx.SetPixel(x, y, f(x, y));
        tx.Apply(); File.WriteAllBytes(p, tx.EncodeToPNG()); Object.DestroyImmediate(tx);
        AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        var ti = (TextureImporter)AssetImporter.GetAtPath(p);
        ti.sRGBTexture = false; ti.wrapMode = wrap; ti.mipmapEnabled = true; ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
    }

    static float Ground(Vector3 p)
    {
        Physics.SyncTransforms();
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(p.x, 20f, p.z), Vector3.down, out hit, 40f, 1 | (1 << 11), QueryTriggerInteraction.Ignore)) return hit.point.y;
        return 0.5f;
    }

    static Vector2 V(float r, float y) { return new Vector2(r, y); }

    static Mesh Lathe(Vector2[] p, int seg)
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
        int np = p.Length;
        var sn = new Vector2[np - 1];
        for (int i = 0; i < np - 1; i++) { var d = p[i + 1] - p[i]; sn[i] = new Vector2(d.y, -d.x).normalized; }
        for (int s = 0; s < np - 1; s++)
        {
            Vector2 n0 = sn[s], n1 = sn[s];
            if (s > 0 && Vector2.Angle(sn[s - 1], sn[s]) < 60f) n0 = (sn[s - 1] + sn[s]).normalized;
            if (s < np - 2 && Vector2.Angle(sn[s + 1], sn[s]) < 60f) n1 = (sn[s + 1] + sn[s]).normalized;
            int b = v.Count;
            for (int k = 0; k <= seg; k++)
            {
                float a = k * Mathf.PI * 2f / seg; float c = Mathf.Cos(a), si = Mathf.Sin(a);
                v.Add(new Vector3(p[s].x * c, p[s].y, p[s].x * si)); n.Add(new Vector3(n0.x * c, n0.y, n0.x * si));
                v.Add(new Vector3(p[s + 1].x * c, p[s + 1].y, p[s + 1].x * si)); n.Add(new Vector3(n1.x * c, n1.y, n1.x * si));
            }
            for (int k = 0; k < seg; k++)
            {
                int i0 = b + k * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                Tri(t, v, n, i0, i1, i2); Tri(t, v, n, i2, i1, i3);
            }
        }
        var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
        return m;
    }

    static void Tri(List<int> t, List<Vector3> v, List<Vector3> n, int a, int b, int c)
    {
        var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (fn.sqrMagnitude < 1e-16f) return;
        if (Vector3.Dot(fn, n[a] + n[b] + n[c]) < 0f) { t.Add(a); t.Add(c); t.Add(b); } else { t.Add(a); t.Add(b); t.Add(c); }
    }

    static Mesh SaveMesh(Mesh m, string name)
    {
        string p = DIR + "/Meshes/" + name + ".asset";
        m.name = name;
        if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p);
        AssetDatabase.CreateAsset(m, p);
        return m;
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/pyrite_fx.txt", sb.ToString());
        Debug.Log("[AmbientFX] " + sb.ToString().Split('\n').FirstOrDefault(l => l.StartsWith("RESULT") || l.StartsWith("EXCEPTION")));
    }
}
#endif
