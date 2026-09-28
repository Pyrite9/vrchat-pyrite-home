// PyriteBedroomBeds.cs — 텐트 침실 3단계: 에어 매트 4 + 베개 4 + 담요 4 + 눕기 Station 4
// Tools ▸ Pyrite3 ▸ Z49f. Bedroom Beds Build  /  Z49g. Bedroom Beds Revert
//  TentBedroom/Beds 아래에 만든다. ⚠ Z49b(셸 재빌드)는 TentBedroom 을 통째로 새로 만들므로 그 뒤 Z49f 다시
//  매트: 초타원체(위아래 평평, 모서리 둥근) 2.2×0.9×0.25 m, 윗면에 세로 공기 챔버 5줄. 머리 = −Z(뒤 벽), 발 = +Z(창)
//  눕기: 야전침대 Cot 의 VRCStation 복사(AC_LieDown — Z44b 트래킹 설정 포함) + PyriteCarrySeat(pickup 없음). 판정 상자는 Pickup 레이어(몸에 안 걸림)
//  VR 은 그냥 실제로 누우면 되고, Station 은 데스크톱·앉은 자세 사용자용
//  정적 플래그 없음 (4단계 조명에서 결정). 로그 Logs/pyrite_bedroom.txt, 렌더 Assets/_preview/bedroom/beds_*.jpg
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

public static class PyriteBedroomBeds
{
    const string DIR = "Assets/Bedroom";
    const int PICKUP_LAYER = 13;
    const float MAT_W = 0.9f, MAT_H = 0.25f;
    static StringBuilder sb;

    static readonly Color C_MAT = new Color(0.19f, 0.23f, 0.26f);
    static readonly Color C_PILLOW = new Color(0.82f, 0.78f, 0.70f);
    static readonly Color[] C_BLANKET =
    {
        new Color(0.72f, 0.55f, 0.22f),   // 머스터드
        new Color(0.58f, 0.27f, 0.18f),   // 러스트
        new Color(0.44f, 0.51f, 0.39f),   // 세이지
        new Color(0.26f, 0.26f, 0.29f),   // 차콜
    };

    [MenuItem("Tools/Pyrite3/Z49f. Bedroom Beds Build", false, 4905)]
    public static void Build()
    {
        sb = new StringBuilder("[Z49f] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { if (Inner()) { Renders(); sb.AppendLine("RESULT: DONE"); } }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    [MenuItem("Tools/Pyrite3/Z49g. Bedroom Beds Revert", false, 4906)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z49g] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = Root("TentBedroom");
        var beds = room ? room.transform.Find("Beds") : null;
        if (beds) { Object.DestroyImmediate(beds.gameObject); sb.AppendLine("Beds 삭제"); }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE");
        Flush();
    }

    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    static bool Inner()
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음 → Z49b 먼저"); return false; }
        var cot = Root("Cot");
        var cotSt = cot ? cot.GetComponent<VRC.SDK3.Components.VRCStation>() : null;
        if (cotSt == null) { sb.AppendLine("!! Cot VRCStation 없음"); return false; }

        var old = room.transform.Find("Beds"); if (old) { Object.DestroyImmediate(old.gameObject); sb.AppendLine("이전 Beds 삭제"); }
        Directory.CreateDirectory(DIR + "/Meshes");

        var mMat = Mat("M_AirMattress", C_MAT, 0.30f);
        var mPil = Mat("M_Pillow", C_PILLOW, 0.08f);
        var mBl = C_BLANKET.Select((c, i) => Mat("M_Blanket_" + i, c, 0.05f)).ToArray();

        var mattress = SaveMesh(SuperEllipsoid("AirMattress", MAT_W / 2f, MAT_H / 2f, PyriteBedroomBuild.MAT_LEN / 2f, 0.22f, 0.16f, 64, 24, 5, 0.018f), "AirMattress");
        var pillow = SaveMesh(SuperEllipsoid("Pillow", 0.30f, 0.065f, 0.19f, 0.55f, 0.35f, 32, 16, 0, 0f), "Pillow");
        var blanket = SaveMesh(SuperEllipsoid("BlanketFold", 0.43f, 0.04f, 0.30f, 0.30f, 0.18f, 32, 12, 3, 0.008f), "BlanketFold");

        var beds = new GameObject("Beds"); beds.transform.SetParent(room.transform, false);
        Undo.RegisterCreatedObjectUndo(beds, "beds");

        float head = PyriteBedroomBuild.MAT_HEAD, len = PyriteBedroomBuild.MAT_LEN;
        float zc = head + len / 2f, foot = head + len;
        var rnd = new System.Random(49);
        int nSt = 0;
        for (int i = 0; i < PyriteBedroomBuild.MAT_X.Length; i++)
        {
            float x = PyriteBedroomBuild.MAT_X[i];
            var bed = new GameObject("Bed_" + (i + 1)); bed.transform.SetParent(beds.transform, false);
            bed.transform.localPosition = new Vector3(x, 0, zc);

            Part(bed.transform, "Mattress", mattress, mMat, new Vector3(0, 0, 0), Quaternion.identity);
            float yaw = (float)(rnd.NextDouble() * 6 - 3);
            Part(bed.transform, "Pillow", pillow, mPil, new Vector3((float)(rnd.NextDouble() * 0.06 - 0.03), MAT_H - 0.015f, -len / 2f + 0.26f), Quaternion.Euler(-6f, yaw, 0));
            yaw = (float)(rnd.NextDouble() * 8 - 4);
            Part(bed.transform, "Blanket", blanket, mBl[i % mBl.Length], new Vector3((float)(rnd.NextDouble() * 0.04 - 0.02), MAT_H - 0.01f, len / 2f - 0.40f), Quaternion.Euler(0, yaw, 0));

            // 눕기 Station
            var lie = new GameObject("Lie"); lie.layer = PICKUP_LAYER; lie.transform.SetParent(bed.transform, false);
            var bc = lie.AddComponent<BoxCollider>(); bc.center = new Vector3(0, MAT_H * 0.5f + 0.02f, 0); bc.size = new Vector3(MAT_W - 0.05f, MAT_H + 0.04f, len - 0.05f);
            var st = lie.AddComponent<VRC.SDK3.Components.VRCStation>();
            EditorUtility.CopySerialized(cotSt, st);
            st.PlayerMobility = VRC.SDKBase.VRCStation.Mobility.Immobilize; st.seated = true; st.disableStationExit = false; st.canUseStationFromStation = true;
            var lp = new GameObject("LiePoint").transform; lp.SetParent(lie.transform, false);
            lp.localPosition = new Vector3(0, MAT_H + 0.005f, -0.05f); lp.localRotation = Quaternion.identity;   // 앞(+Z) = 발 쪽 (돗자리: 머리 −X, rot 90 → 앞 +X 와 같은 규칙)
            var ep = new GameObject("ExitPoint").transform; ep.SetParent(lie.transform, false);
            ep.localPosition = new Vector3(0, 0.02f, len / 2f + 0.45f); ep.localRotation = Quaternion.identity;
            st.stationEnterPlayerLocation = lp; st.stationExitPlayerLocation = ep;
            EditorUtility.SetDirty(st);
            var cs = UdonSharpUndo.AddComponent<PyriteCarrySeat>(lie);
            cs.station = st; cs.pickup = null;
            UdonSharpEditorUtility.CopyProxyToUdon(cs);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(cs);
            if (ub != null) { ub.interactText = "Lie down"; ub.proximity = 2f; EditorUtility.SetDirty(ub); }
            nSt++;

            // 벽 여유: 매트 뒤쪽 윗모서리가 돔 안인지
            float wallZ = PyriteBedroomBuild.SurfZ(Mathf.Abs(x) + MAT_W / 2f, MAT_H);
            float pz = PyriteBedroomBuild.SurfZ(Mathf.Abs(x), MAT_H + 0.13f);
            sb.AppendLine(string.Format("  Bed_{0} x {1:F2} | 매트 머리 z {2:F2}, 그 높이 벽 z −{3:F2} (여유 {4:F2}) | 베개 윗면 벽 z −{5:F2} | exit z {6:F2}",
                i + 1, x, head, wallZ, wallZ + head, pz, foot + 0.45f));
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        int tris = beds.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0);
        sb.AppendLine("Beds " + PyriteBedroomBuild.MAT_X.Length + " | stations " + nSt + " (Cot 복사, ctrl " + (cotSt.animatorController ? cotSt.animatorController.name : "null") + ") | tris " + tris);
        return true;
    }

    // 초타원체: 반지름 a(x) c(y) b(z), e1 = 위아래 둥금(작을수록 평평), e2 = 가로 모서리 둥금. 바닥이 y 0.
    //  chambers > 0 이면 윗면에 x 방향 골(세로 공기 챔버)
    static Mesh SuperEllipsoid(string name, float a, float c, float b, float e1, float e2, int seg, int rings, int chambers, float groove)
    {
        var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        for (int j = 0; j <= rings; j++)
        {
            float vv = -Mathf.PI / 2f + Mathf.PI * j / rings;
            float cv = SP(Mathf.Cos(vv), e1), sv = SP(Mathf.Sin(vv), e1);
            for (int i = 0; i <= seg; i++)
            {
                float uu = 2f * Mathf.PI * i / seg;
                var p = new Vector3(a * cv * SP(Mathf.Cos(uu), e2), c * sv, b * cv * SP(Mathf.Sin(uu), e2));
                if (chambers > 0 && p.y > 0)
                {
                    float fx = (p.x / a) * 0.5f + 0.5f;                  // 0..1 가로
                    float top = Mathf.Pow(Mathf.Clamp01(p.y / c), 3f);   // 윗면에서만
                    float wave = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * fx * chambers);   // 챔버 사이 이음선에서 0
                    p.y -= groove * top * (1f - wave);
                }
                p.y += c;
                v.Add(p);
                uv.Add(new Vector2((float)i / seg * 4f, (float)j / rings));
            }
        }
        int w = seg + 1;
        for (int j = 0; j < rings; j++)
            for (int i = 0; i < seg; i++)
            {
                int q = j * w + i, r = q + 1, s = q + w, d = s + 1;
                t.AddRange(new[] { q, s, r, r, s, d });
            }
        // 바깥을 보게 감김 맞추기
        var ctr = new Vector3(0, c, 0); double agree = 0;
        for (int k = 0; k < t.Count; k += 3)
        {
            var fn = Vector3.Cross(v[t[k + 1]] - v[t[k]], v[t[k + 2]] - v[t[k]]);
            agree += Vector3.Dot(fn, (v[t[k]] + v[t[k + 1]] + v[t[k + 2]]) / 3f - ctr);
        }
        if (agree < 0) for (int k = 0; k < t.Count; k += 3) { int x = t[k + 1]; t[k + 1] = t[k + 2]; t[k + 2] = x; }
        var m = new Mesh { name = name };
        m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateNormals();
        var nn = m.normals;   // u 이음매 법선 평균
        for (int j = 0; j <= rings; j++) { int k0 = j * w, k1 = j * w + seg; var avg = (nn[k0] + nn[k1]).normalized; nn[k0] = avg; nn[k1] = avg; }
        m.normals = nn;
        m.RecalculateBounds(); m.RecalculateTangents();
        Unwrapping.GenerateSecondaryUVSet(m);
        sb.AppendLine("  mesh " + name + " verts " + v.Count + " tris " + t.Count / 3 + (agree < 0 ? " (감김 뒤집음)" : "") + " size " + m.bounds.size.ToString("F3"));
        return m;
    }
    static float SP(float x, float e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);

    static Mesh SaveMesh(Mesh m, string name)
    {
        string path = DIR + "/Meshes/" + name + ".asset";
        if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);   // CopySerialized 는 정점이 안 바뀜 (v2 함정)
        AssetDatabase.CreateAsset(m, path); return m;
    }

    static Material Mat(string name, Color col, float gloss)
    {
        string path = DIR + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_Color", col); m.SetFloat("_Glossiness", gloss); m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 lp, Quaternion lr)
    {
        var g = new GameObject(name); g.transform.SetParent(parent, false);
        g.transform.localPosition = lp; g.transform.localRotation = lr;
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    // ── 확인 렌더 ─────────────────────────────
    static void Renders()
    {
        var room = Root("TentBedroom"); if (room == null) return;
        Directory.CreateDirectory("Assets/_preview/bedroom/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        var o = PyriteBedroomBuild.ORIGIN; float A = PyriteBedroomBuild.A, B = PyriteBedroomBuild.B;
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            cam.fieldOfView = 75f;
            Shot(cam, o + new Vector3(0.4f, 1.6f, 2.3f), o + new Vector3(0, 0.2f, -1.4f), "beds_aisle");          // 통로(창 쪽)에서 매트들
            Shot(cam, o + new Vector3(-A + 1.0f, 1.6f, PyriteBedroomBuild.DOOR_Z), o + new Vector3(1.2f, 0.3f, -1.4f), "beds_spawn");   // 입구에서
            Shot(cam, o + new Vector3(2.9f, 0.8f, 0.6f), o + new Vector3(0.5f, 0.2f, -1.6f), "beds_side");        // 옆 구석에서 낮게
            var dome = room.transform.Find("Dome").gameObject; dome.SetActive(false);
            bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
            cam.orthographic = true; cam.orthographicSize = 3.6f;
            Shot(cam, o + new Vector3(0, 30f, 0.001f), o, "beds_top");
            cam.orthographic = o0; cam.orthographicSize = s0;
            dome.SetActive(true);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, string tag)
    {
        var fwd = at - eye;
        cam.transform.SetPositionAndRotation(eye, Mathf.Abs(Vector3.Dot(fwd.normalized, Vector3.up)) > 0.99f ? Quaternion.LookRotation(fwd, Vector3.forward) : Quaternion.LookRotation(fwd));
        int w = 960, h = 540;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        File.WriteAllBytes("Assets/_preview/bedroom/" + tag + ".jpg", tex.EncodeToJPG(80));
        sb.AppendLine("  shot " + tag);
        Object.DestroyImmediate(tex);
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_bedroom.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
    }
}
#endif
