// PyriteLapBlanketBuild.cs — 머스터드 빈백 양털을 앉은 사람 무릎에 덮기 (Z52d 실측 / Z52e 빌드 / Z52f 되돌림). 재실행 안전
//  2026-09-30 관리자: "누르면 담요가 걸쳐지면 좋겠어" → "플레이어 무릎에 담요를 덮는 걸 생각" · 동기화
//  흐름: 평소 = 등받이에 걸친 양털(Z52b). 빈백에 앉은 사람이 자기 무릎(판정)을 누르면 무릎에 덮임 → 다시 누르거나 일어나면 등받이로
//  메시: A_BeanbagSit 을 기준 아바타(humanScale 0.85)에 샘플링 → 몸을 굽힌 메시(BakeMesh)를 콜라이더로 삼아
//        허벅지 좌표계(원점 = 허벅지 뿌리 중점, +Z = 무릎 쪽, +Y = 위) 격자에서 위→아래 레이캐스트 → 높이장
//        → 원뿔 팽창(기울기 SLOPE 로 흘러내림) → 매끈하게. 빈백 표면·바닥도 받침으로 포함. 앞뒷면 (가장자리 안쪽이 보여서)
//  런타임(PyriteLapBlanket): owner 의 허벅지 뿌리·무릎 bone 으로 위치·방향, 크기 = 허벅지 길이 / 기준 길이
//  구조: Beanbag_1/LapThrow (Default 레이어, BoxCollider + U#) / Mesh (렌더러, 침실 레이어)
//  ⚠ Z52b(양털 재빌드) · Z51o(앉기 재보정) 뒤에는 Z52e 다시
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

    // 격자 (허벅지 좌표계, m, 기준 크기)
    const float W = 0.26f;          // 반폭 (첫 판 0.31: 옆이 바닥까지 흘러내림)
    const float Z0 = -0.06f;        // 뒤끝 (배 쪽)
    const float PAST = 0.14f;       // 무릎 너머 (첫 판 0.24: 정강이 따라 바닥까지)
    const int NX = 32, NZ = 36;
    const float TOP = 0.6f;         // 레이 시작 높이
    const float SLOPE = 1.7f;       // 받침에서 멀어질 때 내려가는 기울기 (≈ 60°)
    const float CLR_BODY = 0.022f, CLR_BAG = 0.012f, CLR_FLOOR = 0.006f;
    const float YMIN = -0.50f;

    static StringBuilder sb;
    static bool loggedMat, loggedTri;

    [MenuItem("Tools/Pyrite3/Z52d. Lap Blanket Report", false, 5130)]
    public static void Report() => Run("Z52d", () => Measure(false));

    [MenuItem("Tools/Pyrite3/Z52e. Lap Blanket Build", false, 5131)]
    public static void Build() => Run("Z52e", () => Measure(true));

    [MenuItem("Tools/Pyrite3/Z52f. Lap Blanket Revert", false, 5132)]
    public static void Revert() => Run("Z52f", () =>
    {
        var bag = Bag(); if (bag == null) return false;
        var t = bag.Find("LapThrow"); if (t) { Object.DestroyImmediate(t.gameObject); sb.AppendLine("LapThrow 삭제"); }
        var sk = bag.Find("Sheepskin"); if (sk) { var r = sk.GetComponent<Renderer>(); if (r) r.enabled = true; }
        Save(); return true;
    });

    // ═════════════════════════════════════════════════════════════
    static bool Measure(bool build)
    {
        if (build && !EnsureProgram("PyriteLapBlanket")) return false;
        var bag = Bag(); if (bag == null) return false;
        var room = bag.parent.parent;
        var sp = bag.Find("Seat/SitPoint"); var seat = bag.Find("Seat");
        var sheep = bag.Find("Sheepskin");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(CLIP_PATH);
        var model = Humanoid();
        if (sp == null || clip == null || model == null) { sb.AppendLine("!! SitPoint " + (sp != null) + " · 클립 " + (clip != null) + " · 휴머노이드 " + (model != null)); return false; }
        if (build && sheep == null) { sb.AppendLine("!! Beanbag_1/Sheepskin 없음 (Z52b 먼저)"); return false; }

        // ── 배치 · 판정 ──
        sb.AppendLine("빈백 (방 로컬):");
        foreach (Transform b in bag.parent) sb.AppendLine("  " + b.name + " " + V(room.InverseTransformPoint(b.position)) + " yaw " + (Quaternion.Inverse(room.rotation) * b.rotation).eulerAngles.y.ToString("F0") + " scale " + b.lossyScale.ToString("F2"));
        sb.AppendLine(bag.name + " SitPoint (빈백 로컬) " + V(bag.InverseTransformPoint(sp.position)) + " · Seat 컴포넌트 [" + string.Join(", ", seat.GetComponents<Component>().Select(c => c.GetType().Name)) + "]");
        foreach (var bc in bag.GetComponentsInChildren<BoxCollider>(true))
            sb.AppendLine("  BoxCollider " + Path(bc.transform, bag) + " 빈백 로컬 중심 " + V(bag.InverseTransformPoint(bc.transform.TransformPoint(bc.center))) + " 크기 " + V(Vector3.Scale(bc.size, bc.transform.lossyScale)) + " trigger " + bc.isTrigger + " layer " + LayerMask.LayerToName(bc.gameObject.layer));
        foreach (var c in bag.GetComponentsInChildren<Collider>(true).Where(c => !(c is BoxCollider)))
            sb.AppendLine("  " + c.GetType().Name + " " + Path(c.transform, bag) + " bounds " + V(c.bounds.center - bag.position) + " / " + V(c.bounds.size));
        Bounds sheepB = default;
        if (sheep) { sheepB = LocalBounds(sheep.GetComponent<MeshFilter>(), bag); sb.AppendLine("양털(걸침) 빈백 로컬 bounds 중심 " + V(sheepB.center) + " 크기 " + V(sheepB.size)); }

        var bagBody = bag.Find("Body");
        var bagMc = bagBody.gameObject.AddComponent<MeshCollider>(); bagMc.sharedMesh = bagBody.GetComponent<MeshFilter>().sharedMesh;
        var floor = new Plane(bag.up, bag.position);
        GameObject probe = null; MeshCollider bodyMc = null;
        Mesh lapMesh = null; float refThigh = 0f; Vector3 refPos = Vector3.zero; Quaternion refRot = Quaternion.identity;
        var poses = new Dictionary<float, (Vector3 u, Quaternion q, float s, Vector3 head)>();
        try
        {
            AnimationMode.StartAnimationMode();
            // 기준 크기 먼저 (메시), 그다음 크기별 측정
            foreach (float hs in new[] { REF_HS }.Concat(SIZES.Where(s => s != REF_HS)))
            {
                probe = Spawn(model, sp, hs, out var an);
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(probe, clip, 0f); AnimationMode.EndSampling();
                Transform B(HumanBodyBones hb) => an.GetBoneTransform(hb);
                var U = (B(HumanBodyBones.LeftUpperLeg).position + B(HumanBodyBones.RightUpperLeg).position) * 0.5f;
                var K = (B(HumanBodyBones.LeftLowerLeg).position + B(HumanBodyBones.RightLowerLeg).position) * 0.5f;
                var hips = B(HumanBodyBones.Hips).position; var head = B(HumanBodyBones.Head).position;
                var f = (K - U).normalized; var r = Vector3.ProjectOnPlane(sp.right, f).normalized; var up = Vector3.Cross(f, r);
                float thigh = (K - U).magnitude;
                var dh = hips - sp.position; float dUp = Vector3.Dot(dh, sp.up), dHor = Vector3.ProjectOnPlane(dh, sp.up).magnitude;
                var kl = bag.InverseTransformPoint(K);
                sb.AppendLine("크기 " + hs + ": 허벅지 " + thigh.ToString("F3") + " m · 방향(위 성분) " + Vector3.Dot(f, sp.up).ToString("F2") + " (" + (Mathf.Asin(Vector3.Dot(f, sp.up)) * Mathf.Rad2Deg).ToString("F0") + "°)"
                              + " · 무릎 사이 " + (B(HumanBodyBones.LeftLowerLeg).position - B(HumanBodyBones.RightLowerLeg).position).magnitude.ToString("F3")
                              + " · 골반−SitPoint 위 " + dUp.ToString("F3") + " / 수평 " + dHor.ToString("F3") + " · 무릎 빈백로컬 " + V(kl)
                              + " · 머리 빈백로컬 " + V(bag.InverseTransformPoint(head)));
                if (sheep) { var hl = bag.InverseTransformPoint(head); sb.AppendLine("  머리가 걸침 양털 박스 안? " + sheepB.Contains(hl) + " (박스 윗면 " + sheepB.max.y.ToString("F2") + ")"); }

                loggedMat = false; loggedTri = false;
                bodyMc = BakeBody(probe, an, hips);
                if (hs == REF_HS)
                {
                    refThigh = thigh;
                    refPos = sp.InverseTransformPoint(U); refRot = Quaternion.Inverse(sp.rotation) * Quaternion.LookRotation(f, up);
                    lapMesh = MakeMesh(U, f, r, up, thigh, bodyMc, bagMc, floor, out int hitB, out int hitG, out int hitF);
                    sb.AppendLine("  무릎 담요 격자 " + (NX + 1) + "×" + (NZ + 1) + " · 받침 적중 몸 " + hitB + " / 빈백 " + hitG + " / 바닥 " + hitF + " · 삼각형 " + lapMesh.triangles.Length / 3 + " (앞뒷면) · bounds " + V(lapMesh.bounds.size));
                }
                if (lapMesh != null)
                {
                    float s = thigh / refThigh; var q = Quaternion.LookRotation(f, up);
                    poses[hs] = (U, q, s, head);
                    Poke(lapMesh, U, q, s, bodyMc, hs);
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
            Object.DestroyImmediate(bagMc);
        }
        if (!build) { if (lapMesh) Object.DestroyImmediate(lapMesh); return true; }

        // ── 오브젝트 ──
        var old = bag.Find("LapThrow"); if (old) Object.DestroyImmediate(old.gameObject);
        lapMesh = SaveMesh(lapMesh, "LapBlanket");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "M_Sheepskin.mat");
        var go = new GameObject("LapThrow"); go.transform.SetParent(bag, false); go.layer = 0;
        var mesh = new GameObject("Mesh"); mesh.transform.SetParent(go.transform, false); mesh.layer = PyriteBedroomV3.LAYER;
        mesh.AddComponent<MeshFilter>().sharedMesh = lapMesh;
        var mr = mesh.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.lightProbeUsage = LightProbeUsage.Off; mr.shadowCastingMode = ShadowCastingMode.On; mr.enabled = false;
        // 판정: 담요 bounds + 위로 18 cm (Seat 판정 박스 윗면 0.62 보다 위에서 먼저 맞게 — 무릎 쪽 z > 0.36 은 Seat 박스 밖)
        var lb = lapMesh.bounds; var box = go.AddComponent<BoxCollider>();
        box.center = lb.center + new Vector3(0f, 0.09f, 0f); box.size = lb.size + new Vector3(0f, 0.18f, 0f);
        // 쉬는 자세: 판정 박스가 걸친 양털을 감싸게 (비대칭 스케일, 이때 렌더러는 꺼져 있음)
        var ds = new Vector3(sheepB.size.x / box.size.x, sheepB.size.y / box.size.y, sheepB.size.z / box.size.z);
        var dp = sheepB.center - Vector3.Scale(ds, box.center);
        go.transform.localPosition = dp; go.transform.localScale = ds;
        var lbh = UdonSharpUndo.AddComponent<PyriteLapBlanket>(go);
        lbh.sitPoint = sp; lbh.drape = sheep.GetComponent<Renderer>(); lbh.lap = mr; lbh.refThigh = refThigh;
        lbh.refPos = refPos; lbh.refRot = refRot; lbh.drapePos = dp; lbh.drapeRot = Quaternion.identity; lbh.drapeScale = ds;
        UdonSharpEditorUtility.CopyProxyToUdon(lbh);
        var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(lbh);
        if (ub != null) { ub.interactText = "Cover Lap"; ub.proximity = 1.5f; EditorUtility.SetDirty(ub); }
        sb.AppendLine("LapThrow: 기준 허벅지 " + refThigh.ToString("F3") + " m · 판정 " + V(box.size) + " · 쉬는 자세 " + V(dp) + " × " + V(ds) + " · 기준 자세(SitPoint 로컬) " + V(refPos) + " / " + (refRot.eulerAngles).ToString("F0"));
        Save();
        Renders(room, bag, model, sp, clip, go.transform, mr, sheep.GetComponent<Renderer>(), poses);
        return true;
    }

    // 허벅지 좌표계 높이장 → 원뿔 팽창 → 매끈 → 앞뒷면 메시
    static Mesh MakeMesh(Vector3 U, Vector3 f, Vector3 r, Vector3 up, float thigh, MeshCollider body, MeshCollider bagMc, Plane floor, out int hitB, out int hitG, out int hitF)
    {
        hitB = hitG = hitF = 0;
        float Z1 = thigh + PAST;
        int nx = NX + 1, nz = NZ + 1, n = nx * nz;
        var X = new float[n]; var Z = new float[n]; var H = new float[n];
        for (int j = 0; j < nz; j++) for (int i = 0; i < nx; i++)
        {
            int k = j * nx + i; float x = -W + 2f * W * i / NX, z = Z0 + (Z1 - Z0) * j / NZ; X[k] = x; Z[k] = z;
            var ray = new Ray(U + r * x + f * z + up * TOP, -up);
            float best = float.MaxValue; int src = -1;
            if (body.Raycast(ray, out var h1, 2f) && h1.distance - CLR_BODY < best) { best = h1.distance - CLR_BODY; src = 0; }
            if (bagMc.Raycast(ray, out var h2, 2f) && h2.distance - CLR_BAG < best) { best = h2.distance - CLR_BAG; src = 1; }
            if (floor.Raycast(ray, out float d3) && d3 - CLR_FLOOR < best) { best = d3 - CLR_FLOOR; src = 2; }
            H[k] = src < 0 ? float.NegativeInfinity : TOP - best;
            if (src == 0) hitB++; else if (src == 1) hitG++; else if (src == 2) hitF++;
        }
        var Y = new float[n];
        for (int a = 0; a < n; a++)
        {
            float m = YMIN;
            for (int b = 0; b < n; b++) { if (float.IsNegativeInfinity(H[b])) continue; float dx = X[a] - X[b], dz = Z[a] - Z[b]; float v = H[b] - SLOPE * Mathf.Sqrt(dx * dx + dz * dz); if (v > m) m = v; }
            Y[a] = m;
        }
        for (int pass = 0; pass < 6; pass++)
        {
            var Y2 = (float[])Y.Clone();
            for (int j = 1; j < nz - 1; j++) for (int i = 1; i < nx - 1; i++)
            {
                int k = j * nx + i; float avg = 0.25f * (Y[k - 1] + Y[k + 1] + Y[k - nx] + Y[k + nx]);
                Y2[k] = Mathf.Max(0.5f * Y[k] + 0.5f * avg, float.IsNegativeInfinity(H[k]) ? YMIN : H[k]);
            }
            Y = Y2;
        }
        var vs = new List<Vector3>(); var uv = new List<Vector2>(); var ts = new List<int>();
        for (int side = 0; side < 2; side++)
        {
            int b0 = vs.Count;
            for (int k = 0; k < n; k++) { vs.Add(new Vector3(X[k], Y[k], Z[k])); uv.Add(new Vector2((X[k] + W) / (2f * W), (Z[k] - Z0) / (Z1 - Z0))); }
            for (int j = 0; j < NZ; j++) for (int i = 0; i < NX; i++)
            {
                int p = b0 + j * nx + i, q = p + 1, s = p + nx, t = s + 1;
                if (side == 0) { ts.Add(p); ts.Add(s); ts.Add(q); ts.Add(q); ts.Add(s); ts.Add(t); }
                else { ts.Add(p); ts.Add(q); ts.Add(s); ts.Add(q); ts.Add(t); ts.Add(s); }
            }
        }
        var mesh = new Mesh { name = "LapBlanket" }; mesh.SetVertices(vs); mesh.SetUVs(0, uv); mesh.SetTriangles(ts, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        // 윗면 법선이 위(+Y)를 보는지 확인, 아니면 두 면 감김을 서로 바꿈
        var nr = mesh.normals; float upSum = 0f; for (int k = 0; k < n; k++) upSum += nr[k].y;
        if (upSum < 0f) { var t2 = mesh.triangles; for (int k = 0; k < t2.Length; k += 3) { int tmp = t2[k + 1]; t2[k + 1] = t2[k + 2]; t2[k + 2] = tmp; } mesh.triangles = t2; mesh.RecalculateNormals(); sb.AppendLine("  (감김 뒤집음)"); }
        return mesh;
    }

    // 몸이 담요를 뚫는지: 윗면 정점마다 위에서 아래로 몸에 레이 — 몸 표면이 정점보다 위면 관통
    static void Poke(Mesh m, Vector3 U, Quaternion q, float s, MeshCollider body, float hs)
    {
        var vs = m.vertices; int n = vs.Length / 2, poke = 0, near = 0; float worst = 0f, gapSum = 0f;
        var up = q * Vector3.up;
        for (int k = 0; k < n; k++)
        {
            var w = U + q * (vs[k] * s);
            if (body.Raycast(new Ray(w + up * 0.3f, -up), out var h, 0.6f))
            {
                float gap = 0.3f - h.distance;    // > 0 = 몸이 정점 위로 나옴
                if (gap > 0.002f) { poke++; worst = Mathf.Max(worst, gap); }
                else if (-gap < 0.08f) { near++; gapSum += -gap; }
            }
        }
        sb.AppendLine("  크기 " + hs + " 배율 " + s.ToString("F2") + ": 관통 정점 " + poke + " / " + n + " (" + (100f * poke / n).ToString("F1") + "%) 최대 " + (worst * 100f).ToString("F1") + " cm · 몸 위 평균 띄움 " + (near > 0 ? (gapSum / near * 100f).ToString("F1") : "-") + " cm (" + near + " 정점)");
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

    // ── 렌더: 걸침(전) + 크기별 무릎 덮음(후) ──
    static void Renders(Transform room, Transform bag, GameObject model, Transform sp, AnimationClip clip, Transform throwT, Renderer lapR, Renderer drapeR, Dictionary<float, (Vector3 u, Quaternion q, float s, Vector3 head)> poses)
    {
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var tp = throwT.localPosition; var tr = throwT.localRotation; var ts = throwT.localScale;
        GameObject probe = null;
        Directory.CreateDirectory(PREV);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            AnimationMode.StartAnimationMode();
            cam.fieldOfView = 50f;
            var c = bag.position + bag.up * 0.5f;
            foreach (var hs in SIZES)
            {
                if (!poses.ContainsKey(hs)) continue;
                probe = Spawn(model, sp, hs, out var an);
                AnimationMode.BeginSampling(); AnimationMode.SampleAnimationClip(probe, clip, 0f); AnimationMode.EndSampling();
                string tag = "lap_" + Mathf.RoundToInt(hs * 100);
                if (hs == REF_HS)
                {   // 전: 걸친 상태로 앉음
                    lapR.enabled = false; drapeR.enabled = true;
                    Shot(cam, c + bag.right * 1.5f + bag.forward * 0.9f + bag.up * 0.35f, c, "lap_before");
                }
                var ps = poses[hs];
                throwT.SetPositionAndRotation(ps.u, ps.q); throwT.localScale = Vector3.one * ps.s;
                lapR.enabled = true; drapeR.enabled = false;
                Shot(cam, c + bag.right * 1.5f + bag.forward * 0.9f + bag.up * 0.35f, c, tag + "_34");
                Shot(cam, c + bag.right * 1.6f + bag.up * 0.05f, c, tag + "_side");
                if (hs == REF_HS)
                {
                    Shot(cam, c + bag.forward * 1.7f + bag.up * 0.45f, c, tag + "_front");
                    var eye = an.GetBoneTransform(HumanBodyBones.Head).position + bag.forward * 0.10f + bag.up * 0.05f;
                    Shot(cam, eye, ps.u + (ps.q * Vector3.forward) * 0.35f * ps.s, tag + "_eye");
                }
                Object.DestroyImmediate(probe); probe = null;
            }
        }
        finally
        {
            if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
            if (probe) Object.DestroyImmediate(probe);
            throwT.localPosition = tp; throwT.localRotation = tr; throwT.localScale = ts;
            lapR.enabled = false; drapeR.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
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
