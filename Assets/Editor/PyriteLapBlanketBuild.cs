// PyriteLapBlanketBuild.cs — 머스터드 빈백 양털 한 장 + 앉은 사람 다리 위로 불룩 (Z52d 실측 / Z52e 빌드 / Z52f 되돌림). 재실행 안전
//  2026-09-30 관리자: (fbb88db 고정 모양 무릎 담요는) "본만 보고 옷을 상정하지 않아 옷 안으로 파고듦" → "이불처럼, on/off 토글 없애고 기본 on"
//   빈 상태 = "좌석까지 한 장 (침대식)"
//  흐름: 양털 한 장이 등받이 → 좌석 → 앞면 → 앞 바닥까지 덮여 있음. 앉으면 그 사람 다리 뼈 선분으로 셰이더가 정점을 위로 들어 올림
//   (침대 이불 Pyrite/Blanket 과 같은 계산, 셰이더 Pyrite/SheepThrow = 양털 컷아웃 텍스처판). 누르기·동기화 없음
//  메시: 빈백 로컬 위→아래 높이장(빈백 표면 +1.2 cm, 바닥 +0.6 cm) → 원뿔 팽창(기울기 SLOPE, 모서리에서 천이 흘러내림) → 매끈
//  구조: Beanbag_1/SheepThrow (침실 레이어, 렌더러 + U# PyriteLapBlanket). 옛 Sheepskin(Z52b)은 끄고 EditorOnly — Z52f 로 복구
//  ⚠ Z52b(양털 재빌드) · Z51l(빈백 재빌드) 뒤에는 Z52e 다시. Z51o(앉기 재보정) 뒤에는 Z52d 로 들림 확인
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
using UnityEngine.SceneManagement;

public static class PyriteLapBlanketBuild
{
    const string CLIP_PATH = "Assets/Animations/A_BeanbagSit.anim";
    const string DIR = "Assets/Bedroom/Soft/";
    const string PREV = "Assets/_preview/bedroom/";
    const string LOG = "Logs/pyrite_lap_blanket.txt";
    const float REF_HS = 0.85f;
    static readonly float[] SIZES = { 0.65f, 0.85f, 1.06f };

    // 담요 한 장 (빈백 로컬, m)
    const float XW = 0.31f;          // 반폭
    const float ZB = -0.34f;         // 뒤끝 (등받이 비탈)
    const float ZF = 0.66f;          // 앞끝 (바닥, 무릎 z 0.43~0.60 보다 앞)
    const float STEP = 0.02f;
    const float SLOPE = 3.0f;        // 빈백 모서리에서 흘러내리는 기울기 (≈ 72°)
    const float CLR_BAG = 0.012f, CLR_FLOOR = 0.006f;
    // 들어올림 (U# 와 같은 값)
    const float THIGH_R = 0.30f, SHIN_R = 0.23f, R_MIN = 0.06f, R_MAX = 0.14f;
    const float THICK = 0.03f, SKIRT = 0.14f;

    static StringBuilder sb;
    static bool loggedMat, loggedTri;

    [MenuItem("Tools/Pyrite3/Z52d. Lap Blanket Report", false, 5130)]
    public static void Report() => Run("Z52d", () => Main(false));

    [MenuItem("Tools/Pyrite3/Z52e. Lap Blanket Build", false, 5131)]
    public static void Build() => Run("Z52e", () => Main(true));

    [MenuItem("Tools/Pyrite3/Z52f. Lap Blanket Revert", false, 5132)]
    public static void Revert() => Run("Z52f", () =>
    {
        var bag = Bag(); if (bag == null) return false;
        foreach (var n in new[] { "LapThrow", "SheepThrow" }) { var t = bag.Find(n); if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine(n + " 삭제"); } }
        var sk = bag.Find("Sheepskin");
        if (sk) { sk.gameObject.SetActive(true); sk.gameObject.tag = "Untagged"; var r = sk.GetComponent<Renderer>(); if (r) r.enabled = true; sb.AppendLine("Sheepskin(등받이 띠) 복구"); }
        Save(); return true;
    });

    // ═════════════════════════════════════════════════════════════
    static bool Main(bool build)
    {
        if (build && !EnsureProgram("PyriteLapBlanket")) return false;
        var bag = Bag(); if (bag == null) return false;
        var room = bag.parent.parent;
        var sp = bag.Find("Seat/SitPoint");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CLIP_PATH);
        var model = Humanoid();
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DIR + "T_Sheepskin.png");
        var shader = Shader.Find("Pyrite/SheepThrow");
        if (sp == null || clip == null || model == null || tex == null || shader == null) { sb.AppendLine("!! SitPoint " + (sp != null) + " · 클립 " + (clip != null) + " · 휴머노이드 " + (model != null) + " · T_Sheepskin " + (tex != null) + " · 셰이더 " + (shader != null)); return false; }

        Transform thr = bag.Find("SheepThrow");
        Material mat;
        if (build)
        {
            foreach (var n in new[] { "LapThrow", "SheepThrow" }) { var t = bag.Find(n); if (t) Object.DestroyImmediate(t.gameObject); }
            var sk = bag.Find("Sheepskin"); if (sk) { sk.gameObject.SetActive(false); sk.gameObject.tag = "EditorOnly"; sb.AppendLine("옛 Sheepskin(등받이 띠) 끔 + EditorOnly"); }
            var mesh = RestMesh(bag);
            mesh = SaveMesh(mesh, "SheepThrow");
            mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "M_SheepThrow.mat");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, DIR + "M_SheepThrow.mat"); }
            mat.shader = shader; mat.mainTexture = tex; mat.color = new Color(0.93f, 0.89f, 0.81f);
            mat.SetFloat("_Cutoff", 0.5f); mat.SetFloat("_Glossiness", 0.04f); mat.SetFloat("_Thick", THICK); mat.SetFloat("_Skirt", SKIRT); mat.SetFloat("_SegCount", 0f);
            mat.renderQueue = (int)RenderQueue.AlphaTest; EditorUtility.SetDirty(mat);
            var go = new GameObject("SheepThrow"); go.transform.SetParent(bag, false); go.layer = PyriteBedroomV3.LAYER;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.On;
            var lb = UdonSharpUndo.AddComponent<PyriteLapBlanket>(go);
            lb.mat = mat; lb.sitPoint = sp; lb.thighR = THIGH_R; lb.shinR = SHIN_R; lb.rMin = R_MIN; lb.rMax = R_MAX;
            UdonSharpEditorUtility.CopyProxyToUdon(lb);
            thr = go.transform;
            Save();
        }
        else
        {
            if (thr == null) { sb.AppendLine("!! SheepThrow 없음 (Z52e 먼저)"); return false; }
            mat = thr.GetComponent<Renderer>().sharedMaterial;
        }
        var rest = thr.GetComponent<MeshFilter>().sharedMesh;
        sb.AppendLine("양털 한 장: " + (2 * XW).ToString("F2") + " × " + (ZF - ZB).ToString("F2") + " m (빈백 로컬 z " + ZB + " ~ " + ZF + "), 정점 " + rest.vertexCount + " · 삼각형 " + rest.triangles.Length / 3 + " (Cull Off 한 면)");

        // ── 크기별: 뼈 선분 → CPU 로 셰이더 들어올림 재현 → 관통·띄움 측정 + 렌더 ──
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        GameObject probe = null; MeshCollider bodyMc = null;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 50f;
            var c = bag.position + bag.up * 0.5f;
            var eye34 = c + bag.right * 1.5f + bag.forward * 0.9f + bag.up * 0.35f;
            SetSegs(mat, new List<Vector4>());
            if (build)
            {
                Shot(cam, eye34, c, "thr_empty_34");
                Shot(cam, c + bag.right * 1.6f + bag.up * 0.05f, c, "thr_empty_side");
                Shot(cam, c + bag.forward * 1.7f + bag.up * 0.45f, c, "thr_empty_front");
            }
            AnimationMode.StartAnimationMode();
            foreach (var hs in SIZES)
            {
                probe = Spawn(model, sp, hs, out var an);
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(probe, clip, 0f); AnimationMode.EndSampling();
                var segs = Segs(an);
                var hips = an.GetBoneTransform(HumanBodyBones.Hips).position;
                loggedMat = false; loggedTri = false;
                bodyMc = BakeBody(probe, an, hips);
                Measure(rest, thr, segs, bodyMc, hs);
                SetSegs(mat, segs);
                if (build)
                {
                    string tag = "thr_" + Mathf.RoundToInt(hs * 100);
                    Shot(cam, eye34, c, tag + "_34");
                    Shot(cam, c + bag.right * 1.6f + bag.up * 0.05f, c, tag + "_side");
                    if (hs == REF_HS)
                    {
                        Shot(cam, c + bag.forward * 1.7f + bag.up * 0.45f, c, tag + "_front");
                        var head = an.GetBoneTransform(HumanBodyBones.Head).position;
                        var kn = an.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position;
                        Shot(cam, head + bag.forward * 0.10f + bag.up * 0.05f, kn - bag.right * 0.08f, tag + "_eye");
                    }
                }
                Object.DestroyImmediate(bodyMc.gameObject); bodyMc = null;
                Object.DestroyImmediate(probe); probe = null;
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (probe) Object.DestroyImmediate(probe);
            if (bodyMc) Object.DestroyImmediate(bodyMc.gameObject);
            SetSegs(mat, new List<Vector4>());
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        return true;
    }

    // U# 와 같은 선분: 허벅지 2 · 사타구니 1 · 정강이 2. 반지름 = 허벅지 길이 비례
    static List<Vector4> Segs(Animator an)
    {
        Vector3 P(HumanBodyBones b) => an.GetBoneTransform(b).position;
        var lu = P(HumanBodyBones.LeftUpperLeg); var ru = P(HumanBodyBones.RightUpperLeg);
        var lk = P(HumanBodyBones.LeftLowerLeg); var rk = P(HumanBodyBones.RightLowerLeg);
        var lf = P(HumanBodyBones.LeftFoot); var rf = P(HumanBodyBones.RightFoot);
        float len = (lk - lu).magnitude;
        float tr = Mathf.Clamp(len * THIGH_R, R_MIN, R_MAX), sr = Mathf.Clamp(len * SHIN_R, R_MIN * 0.8f, R_MAX);
        var l = new List<Vector4>();
        void S(Vector3 a, Vector3 b, float r) { l.Add(new Vector4(a.x, a.y, a.z, r)); l.Add(new Vector4(b.x, b.y, b.z, 0f)); }
        S(lu, lk, tr); S(ru, rk, tr); S(lu, ru, tr * 1.1f); S(lk, lf, sr); S(rk, rf, sr);
        sb.AppendLine("크기 " + (an.humanScale * an.transform.localScale.x).ToString("F2") + ": 허벅지 " + len.ToString("F3") + " m → 반지름 허벅지 " + tr.ToString("F3") + " · 정강이 " + sr.ToString("F3") + " (+ 두께 " + THICK + ")");
        return l;
    }

    static void SetSegs(Material m, List<Vector4> segs)
    {
        var arr = new Vector4[32]; for (int i = 0; i < segs.Count && i < 32; i++) arr[i] = segs[i];
        m.SetVectorArray("_Seg", arr); m.SetFloat("_SegCount", segs.Count / 2);
    }

    // 셰이더 BodyH 그대로
    static float BodyH(Vector3 p, float y0, List<Vector4> s)
    {
        float h = y0;
        for (int i = 0; i + 1 < s.Count; i += 2)
        {
            Vector4 a = s[i], b = s[i + 1]; float r = a.w;
            var ab = new Vector2(b.x - a.x, b.z - a.z); var ap = new Vector2(p.x - a.x, p.z - a.z);
            float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / Mathf.Max(Vector2.Dot(ab, ab), 1e-5f));
            float d = (ap - ab * t).magnitude; float cy = Mathf.Lerp(a.y, b.y, t);
            float top = cy + r + THICK; float sm = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r * 0.55f, r + SKIRT, d));
            h = Mathf.Max(h, Mathf.Lerp(y0, Mathf.Max(top, y0), sm));
        }
        return h;
    }

    // 몸(골반·다리)이 담요를 뚫는지 + 몸 위 띄움 (= 옷 두께 여유)
    static void Measure(Mesh rest, Transform thr, List<Vector4> segs, MeshCollider body, float hs)
    {
        var vs = rest.vertices; int poke = 0, over = 0, lifted = 0; float worst = 0f, sum = 0f, minGap = 9f;
        foreach (var v in vs)
        {
            var w = thr.TransformPoint(v); float h = BodyH(w, w.y, segs); if (h - w.y > 0.005f) lifted++; w.y = h;
            if (body.Raycast(new Ray(w + Vector3.up * 0.4f, Vector3.down), out var hit, 0.8f))
            {
                float gap = w.y - hit.point.y;
                if (gap < -0.002f) { poke++; worst = Mathf.Max(worst, -gap); }
                else { over++; sum += gap; minGap = Mathf.Min(minGap, gap); }
            }
        }
        sb.AppendLine("  들린 정점 " + lifted + " / " + vs.Length + " · 몸 위 정점 " + (poke + over) + " 중 관통 " + poke + " (최대 " + (worst * 100f).ToString("F1") + " cm) · 몸 위 띄움 평균 " + (over > 0 ? (sum / over * 100f).ToString("F1") : "-") + " / 최소 " + (over > 0 ? (minGap * 100f).ToString("F1") : "-") + " cm");
    }

    // 빈 상태 모양: 빈백 로컬 높이장 → 원뿔 팽창 → 매끈
    static Mesh RestMesh(Transform bag)
    {
        var body = bag.Find("Body");
        var mc = body.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = body.GetComponent<MeshFilter>().sharedMesh;
        try
        {
            int nx = Mathf.RoundToInt(2 * XW / STEP) + 1, nz = Mathf.RoundToInt((ZF - ZB) / STEP) + 1, n = nx * nz;
            var X = new float[n]; var Z = new float[n]; var H = new float[n]; int hitB = 0;
            for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
            {
                int k = j * nx + i; float x = -XW + 2f * XW * i / (nx - 1), z = ZB + (ZF - ZB) * j / (nz - 1); X[k] = x; Z[k] = z;
                float y = CLR_FLOOR;
                if (mc.Raycast(new Ray(bag.TransformPoint(new Vector3(x, 2f, z)), -bag.up), out var h, 3f)) { y = Mathf.Max(y, bag.InverseTransformPoint(h.point).y + CLR_BAG); hitB++; }
                H[k] = y;
            }
            var Y = new float[n];
            for (int a = 0; a < n; a++)
            {
                float m = H[a];
                for (int b = 0; b < n; b++) { float dx = X[a] - X[b], dz = Z[a] - Z[b]; float v = H[b] - SLOPE * Mathf.Sqrt(dx * dx + dz * dz); if (v > m) m = v; }
                Y[a] = m;
            }
            for (int pass = 0; pass < 4; pass++)
            {
                var Y2 = (float[])Y.Clone();
                for (int j = 1; j < nz - 1; j++) for (int i = 1; i < nx - 1; i++)
                { int k = j * nx + i; Y2[k] = Mathf.Max(0.5f * Y[k] + 0.125f * (Y[k - 1] + Y[k + 1] + Y[k - nx] + Y[k + nx]), H[k]); }
                Y = Y2;
            }
            var vs = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var ts = new List<int>();
            for (int k = 0; k < n; k++) { vs.Add(new Vector3(X[k], Y[k], Z[k])); uv.Add(new Vector2((X[k] + XW) / (2f * XW), (Z[k] - ZB) / (ZF - ZB))); col.Add(new Color(1f, 0f, 0f, 1f)); }
            for (int j = 0; j < nz - 1; j++) for (int i = 0; i < nx - 1; i++)
            { int p = j * nx + i, q = p + 1, s = p + nx, t = s + 1; ts.Add(p); ts.Add(s); ts.Add(q); ts.Add(q); ts.Add(s); ts.Add(t); }
            var mesh = new Mesh { name = "SheepThrow" }; mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(ts, 0);
            mesh.RecalculateNormals();
            if (mesh.normals.Sum(q => q.y) < 0f) { var t2 = mesh.triangles; for (int k = 0; k < t2.Length; k += 3) { int tmp = t2[k + 1]; t2[k + 1] = t2[k + 2]; t2[k + 2] = tmp; } mesh.triangles = t2; mesh.RecalculateNormals(); sb.AppendLine("  (감김 뒤집음)"); }
            mesh.RecalculateBounds();
            var bb = mesh.bounds; bb.Encapsulate(bb.max + Vector3.up * 0.35f); mesh.bounds = bb;   // 셰이더 들어올림 몫 (컬링)
            sb.AppendLine("  높이장 " + nx + "×" + nz + " · 빈백 적중 " + hitB + " · 높이 " + Y.Min().ToString("F2") + " ~ " + Y.Max().ToString("F2") + " m");
            return mesh;
        }
        finally { Object.DestroyImmediate(mc); }
    }

    // 받침 = 골반·다리에 붙은 삼각형만 (첫 판: 허벅지 위 손·팔꿈치까지 받쳐서 담요가 뾰족한 텐트처럼 솟음)
    static readonly HumanBodyBones[] SUPPORT = { HumanBodyBones.Hips, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
    static MeshCollider BakeBody(GameObject g, Animator an, Vector3 hips)
    {
        var verts = new List<Vector3>(); var tris = new List<int>();
        var hmap = new Dictionary<Transform, HumanBodyBones>();
        for (int hb = 0; hb < (int)HumanBodyBones.LastBone; hb++) { var t = an.GetBoneTransform((HumanBodyBones)hb); if (t && !hmap.ContainsKey(t)) hmap[t] = (HumanBodyBones)hb; }
        int kept = 0, dropped = 0;
        foreach (var smr in g.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!smr.enabled || !smr.gameObject.activeInHierarchy || smr.sharedMesh == null) continue;
            // 뼈마다 소속 휴머노이드 뼈 (트위스트 등 비휴머노이드 뼈는 위로 올라가 찾음)
            var bones = smr.bones; var ok = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var t = bones[i]; while (t && !hmap.ContainsKey(t)) t = t.parent;
                ok[i] = t && SUPPORT.Contains(hmap[t]);
            }
            var bw = smr.sharedMesh.boneWeights;
            bool VOk(int v) => bw.Length == 0 || ok[bw[v].boneIndex0];
            var m = new Mesh(); smr.BakeMesh(m, true);
            // BakeMesh 좌표계 확인: 스케일 포함 행렬 vs 스케일 뺀 행렬 — 결과 중심이 골반에 가까운 쪽
            var a = smr.transform.localToWorldMatrix; var b = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            var mv = m.vertices; var ca = Center(mv.Select(v => a.MultiplyPoint3x4(v))); var cb = Center(mv.Select(v => b.MultiplyPoint3x4(v)));
            var mat = (ca - hips).sqrMagnitude <= (cb - hips).sqrMagnitude ? a : b;
            if (!loggedMat) { sb.AppendLine("  BakeMesh 행렬: " + (mat == a ? "스케일 포함" : "스케일 뺌") + " (중심−골반 " + (ca - hips).magnitude.ToString("F2") + " / " + (cb - hips).magnitude.ToString("F2") + " m)"); loggedMat = true; }
            int b0 = verts.Count; verts.AddRange(mv.Select(v => mat.MultiplyPoint3x4(v)));
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var tt = m.GetTriangles(s);
                for (int k = 0; k < tt.Length; k += 3)
                {
                    if (VOk(tt[k]) && VOk(tt[k + 1]) && VOk(tt[k + 2])) { tris.Add(tt[k] + b0); tris.Add(tt[k + 1] + b0); tris.Add(tt[k + 2] + b0); kept++; }
                    else dropped++;
                }
            }
            Object.DestroyImmediate(m);
        }
        if (!loggedTri) { sb.AppendLine("  받침 삼각형 (골반·다리) " + kept + " / 뺀 것(팔·몸통·머리) " + dropped); loggedTri = true; }
        var cm = new Mesh { indexFormat = IndexFormat.UInt32 }; cm.SetVertices(verts); cm.SetTriangles(tris, 0); cm.RecalculateBounds();
        var go = new GameObject("_LapBodyCol") { hideFlags = HideFlags.DontSave };
        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = cm;
        return mc;
    }

    static Vector3 Center(IEnumerable<Vector3> ps) { var l = ps.ToList(); var mn = l.Aggregate(Vector3.Min); var mx = l.Aggregate(Vector3.Max); return (mn + mx) * 0.5f; }

    static GameObject Spawn(GameObject model, Transform at, float hs, out Animator an)
    {
        var g = Object.Instantiate(model); g.name = "_LapProbe"; g.hideFlags = HideFlags.DontSave;
        g.transform.SetPositionAndRotation(at.position, at.rotation);
        an = g.GetComponent<Animator>(); an.applyRootMotion = false;
        g.transform.localScale = Vector3.one * (hs / an.humanScale);
        foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = PyriteBedroomV3.LAYER;
        foreach (var r in g.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
        return g;
    }

    static GameObject Humanoid()
    {
        var found = new List<(string path, Avatar av)>();
        foreach (var gid in AssetDatabase.FindAssets("t:Avatar"))
        {
            var p = AssetDatabase.GUIDToAssetPath(gid);
            foreach (var av in AssetDatabase.LoadAllAssetsAtPath(p).OfType<Avatar>()) if (av.isHuman && av.isValid) found.Add((p, av));
        }
        foreach (var f in found.OrderBy(f => f.path.StartsWith("Assets/") ? 0 : 1))
        {
            var go = AssetDatabase.LoadMainAssetAtPath(f.path) as GameObject; if (go == null) continue;
            var a = go.GetComponent<Animator>(); if (a == null || a.avatar != f.av) continue;
            sb.AppendLine("기준 모델: " + f.path);
            return go;
        }
        return null;
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes(PREV + tag + ".jpg", tex.EncodeToJPG(85));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    // ── 공통 ──
    static void Run(string tag, System.Func<bool> body)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (body()) sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText(LOG, sb.ToString(), new UTF8Encoding(false));
    }

    static Transform Bag()
    {
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        var b = room ? room.transform.Find("Beanbags/Beanbag_1") : null;
        if (b == null) sb.AppendLine("!! TentBedroom/Beanbags/Beanbag_1 없음");
        return b;
    }

    static Bounds LocalBounds(MeshFilter mf, Transform space)
    {
        var mb = mf.sharedMesh.bounds; var bb = new Bounds(space.InverseTransformPoint(mf.transform.TransformPoint(mb.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            var c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            bb.Encapsulate(space.InverseTransformPoint(mf.transform.TransformPoint(c)));
        }
        return bb;
    }

    static bool EnsureProgram(string name)
    {
        string asset = "Assets/Udon/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(asset) != null) return true;
        var paType = System.AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return new System.Type[0]; } })
            .FirstOrDefault(t => t.Name == "UdonSharpProgramAsset");
        var ms = AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Udon/" + name + ".cs");
        if (paType == null || ms == null) { sb.AppendLine("!! 프로그램 에셋 생성 실패 " + name + " (cs " + (ms != null) + ")"); return false; }
        var pa = ScriptableObject.CreateInstance(paType);
        AssetDatabase.CreateAsset(pa, asset);
        var so = new SerializedObject(pa); so.FindProperty("sourceCsScript").objectReferenceValue = ms; so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(pa); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        sb.AppendLine("프로그램 에셋 생성 " + asset + " → Assets ▸ Refresh 후 Z52e 다시");
        return false;
    }

    static void Save() { EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes(); }

    static Mesh SaveMesh(Mesh m, string name)
    {
        Directory.CreateDirectory(DIR);
        string path = DIR + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        m.name = name; AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static string Path(Transform t, Transform stop) { var s = t.name; while (t.parent && t != stop && t.parent != stop) { t = t.parent; s = t.name + "/" + s; } return s; }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + ", " + v.y.ToString("F2") + ", " + v.z.ToString("F2") + ")";
}
#endif
