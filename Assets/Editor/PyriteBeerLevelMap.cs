// Tools ▸ Pyrite4 ▸ Z53g. Beer Level Map (부피 보존 수면) / Z53h. Beer Level Map Revert
//  2026-09-30 23:49 관리자 인게임: 맥주를 들고 뒤집으면 액체가 반대로 점점 차오름
//   원인: Pyrite/BeerLiquid 가 수면을 "병 축 위 (0, _Level, 0) 을 지나는 수평면"으로 두고 아래만 남김 → 뒤집으면 남는 쪽이 빈 목 쪽
//    (가득 0.192 병을 180° = 목 0.192~0.207 만 보임, 한 모금 0.022 병 180° = 거의 가득, 90° = 채움과 무관하게 절반)
//  Z53g: 액체 메시(PyriteBeerBuild 의 BeerLiquid 회전체와 같은 윤곽)를 점 7.7 만 개로 나눠, 기울기 64 × 똑바로 수면 64 칸마다
//        "똑바로일 때와 같은 부피가 되는 수평면 높이(원점 기준 m)" 를 구해 RHalf 64×64 텍스처 T_BeerLevelMap.asset 으로 저장,
//        M_BeerLiquid 에 _LevelMap + _MapOn 1. Udon·동기화는 그대로 (_Level = 똑바로 섰을 때 수면 높이, 의미 같음)
//        전후 렌더: 기울기 0/45/90/135/180° × 수면 0.192/0.095/0.022 → Assets/_preview/beer/level_before.jpg · level_after.jpg
//  Z53h: _MapOn 0 (예전 방식). 텍스처는 남겨 둠
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteBeerLevelMap
{
    const string MAT = "Assets/Props/Beer/M_BeerLiquid.mat";
    const string TEX = "Assets/Props/Beer/T_BeerLevelMap.asset";
    const string PREV = "Assets/_preview/beer/";
    const int N = 64;
    const float LMAX = 0.2072f;
    static readonly Vector2[] PROF = {   // (반지름, 높이) — PyriteBeerBuild 의 BeerLiquid 윤곽 (바닥 0.006, 꼭대기 0.2072)
        new Vector2(0.0262f, 0.006f), new Vector2(0.0283f, 0.012f), new Vector2(0.0283f, 0.139f), new Vector2(0.0264f, 0.155f),
        new Vector2(0.0187f, 0.175f), new Vector2(0.0131f, 0.191f), new Vector2(0.0121f, 0.207f), new Vector2(0.0121f, 0.2072f) };
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite4/Z53g. Beer Level Map (volume-true surface)", false, 5307)]
    public static void Apply()
    {
        sb = new StringBuilder("[Z53g] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
            if (mat == null) { sb.AppendLine("!! " + MAT + " 없음"); return; }
            if (!mat.HasProperty("_LevelMap")) { sb.AppendLine("!! 셰이더에 _LevelMap 없음 (셰이더 갱신 전)"); return; }
            var sw = System.Diagnostics.Stopwatch.StartNew();
            float[,] H = Build(out float vol);
            sb.AppendLine(string.Format("액체 부피 {0:F1} ml · 계산 {1} ms", vol * 1e6, sw.ElapsedMilliseconds));

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TEX);
            if (tex == null || tex.width != N || tex.format != TextureFormat.RHalf)
            {
                if (tex != null) AssetDatabase.DeleteAsset(TEX);
                tex = new Texture2D(N, N, TextureFormat.RHalf, false, true);
                AssetDatabase.CreateAsset(tex, TEX);
            }
            var px = new Color[N * N];
            for (int j = 0; j < N; j++) for (int i = 0; i < N; i++) px[j * N + i] = new Color(H[i, j], 0, 0, 1);
            tex.SetPixels(px); tex.Apply(false, false);
            tex.wrapMode = TextureWrapMode.Clamp; tex.filterMode = FilterMode.Bilinear; tex.name = "T_BeerLevelMap";
            EditorUtility.SetDirty(tex);

            // 확인용 값 (Python 검산: 0.192 → 0°0.1919 45°0.1368 90°0.0251 135°0.0005 180°−0.0093)
            foreach (float L in new[] { 0.192f, 0.095f, 0.022f })
            {
                var line = new StringBuilder("  수면 " + L.ToString("F3") + ":");
                foreach (float a in new[] { 0f, 45f, 90f, 135f, 180f }) line.Append(string.Format("  {0:F0}° {1:F4}", a, Sample(H, a / 180f, L / LMAX)));
                sb.AppendLine(line.ToString());
            }

            Shots(mat, "level_before", 0f);
            mat.SetTexture("_LevelMap", tex); mat.SetFloat("_LevelMax", LMAX); mat.SetFloat("_MapN", N); mat.SetFloat("_MapOn", 1f);
            EditorUtility.SetDirty(mat);
            Shots(mat, "level_after", 1f);
            AssetDatabase.SaveAssets();
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally { Flush(); }
    }

    [MenuItem("Tools/Pyrite4/Z53h. Beer Level Map Revert", false, 5308)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z53h] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (mat != null) { mat.SetFloat("_MapOn", 0f); EditorUtility.SetDirty(mat); AssetDatabase.SaveAssets(); sb.AppendLine("_MapOn 0\nRESULT: DONE"); }
        Flush();
    }

    static float R(float y)
    {
        if (y <= PROF[0].y) return PROF[0].x;
        for (int k = 1; k < PROF.Length; k++)
            if (y <= PROF[k].y) return Mathf.Lerp(PROF[k - 1].x, PROF[k].x, (y - PROF[k - 1].y) / (PROF[k].y - PROF[k - 1].y));
        return PROF[PROF.Length - 1].x;
    }

    // H[i, j]: 기울기 i/(N−1)·180°, 똑바로 수면 j/(N−1)·LMAX 일 때 수면 높이 (원점 기준, 병 축 방향 단위 m)
    static float[,] Build(out float vol)
    {
        const int NA = 200, NR = 12, NP = 32;
        float y0 = PROF[0].y, y1 = PROF[PROF.Length - 1].y, dy = (y1 - y0) / NA;
        int M = NA * NR * NP;
        var px = new float[M]; var py = new float[M]; var w = new float[M];
        int n = 0;
        for (int a = 0; a < NA; a++)
        {
            float y = y0 + (a + 0.5f) * dy, r0 = R(y), dr = r0 / NR;
            for (int b = 0; b < NR; b++)
            {
                float r = (b + 0.5f) * dr, ww = r * dr * dy * 2f * Mathf.PI / NP;
                for (int c = 0; c < NP; c++) { float p = (c + 0.5f) * 2f * Mathf.PI / NP; px[n] = r * Mathf.Cos(p); py[n] = y; w[n] = ww; n++; }
            }
        }
        vol = w.Sum();
        // 똑바로 수면 j 까지의 부피
        var target = new float[N];
        for (int j = 0; j < N; j++) { float L = j / (float)(N - 1) * LMAX; float s = 0f; for (int k = 0; k < M; k++) if (py[k] < L) s += w[k]; target[j] = s; }
        var H = new float[N, N];
        var h = new float[M]; var idx = new int[M];
        for (int i = 0; i < N; i++)
        {
            float th = i / (float)(N - 1) * Mathf.PI, cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            for (int k = 0; k < M; k++) { h[k] = py[k] * cs + px[k] * sn; idx[k] = k; }
            System.Array.Sort((float[])h.Clone(), idx);
            float cum = 0f; int q = 0;
            for (int j = 0; j < N; j++)
            {
                if (target[j] <= 0f) { H[i, j] = h[idx[0]] - 0.002f; continue; }   // 빈 병: 가장 낮은 점보다 아래
                while (q < M - 1 && cum + w[idx[q]] < target[j]) { cum += w[idx[q]]; q++; }
                H[i, j] = h[idx[q]];
            }
        }
        return H;
    }

    static float Sample(float[,] H, float u, float v)
    {
        float x = Mathf.Clamp01(u) * (N - 1), y = Mathf.Clamp01(v) * (N - 1);
        int x0 = Mathf.Min((int)x, N - 2), y0 = Mathf.Min((int)y, N - 2); float fx = x - x0, fy = y - y0;
        return Mathf.Lerp(Mathf.Lerp(H[x0, y0], H[x0 + 1, y0], fx), Mathf.Lerp(H[x0, y0 + 1], H[x0 + 1, y0 + 1], fx), fy);
    }

    // 병 하나를 공중(박스 위 3 m)에 복제해 5 기울기 × 3 수면을 한 장으로
    static void Shots(Material mat, string tag, float mapOn)
    {
        var src = GameObject.Find("BeerCooler/Bottles/Beer_0/Visual/Body");
        var cam = Camera.main;
        if (src == null || cam == null) { sb.AppendLine("!! Beer_0 Body 또는 카메라 없음"); return; }
        mat.SetFloat("_MapOn", mapOn);
        var tmp = Object.Instantiate(src); tmp.name = "_LevelShot";
        foreach (var ps in tmp.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
        var capT = tmp.transform.Find("Cap"); if (capT) capT.gameObject.SetActive(false);
        var full = tmp.transform.Find("GlassFull"); var empty = tmp.transform.Find("GlassEmpty");
        if (full) full.gameObject.SetActive(true); if (empty) empty.gameObject.SetActive(false);
        var liq = tmp.transform.Find("Liquid").GetComponent<Renderer>(); liq.gameObject.SetActive(true);
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView, n0 = cam.nearClipPlane;
        const int CW = 240, CH = 300; float[] tilts = { 0f, 45f, 90f, 135f, 180f }; float[] levels = { 0.192f, 0.095f, 0.022f };
        var sheet = new Texture2D(CW * 5, CH * 3, TextureFormat.RGB24, false);
        var rt = RenderTexture.GetTemporary(CW, CH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var mpb = new MaterialPropertyBlock();
        try
        {
            Vector3 C = GameObject.Find("BeerCooler").transform.position + new Vector3(0f, 3f, 0f);
            cam.fieldOfView = 40f; cam.nearClipPlane = 0.02f;
            cam.transform.SetPositionAndRotation(C + new Vector3(0f, 0f, -0.48f), Quaternion.LookRotation(Vector3.forward));
            var prevA = RenderTexture.active;
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 5; col++)
                {
                    var rot = Quaternion.Euler(0f, 0f, tilts[col]);
                    tmp.transform.SetPositionAndRotation(C - rot * new Vector3(0f, 0.1036f, 0f), rot);
                    mpb.Clear(); mpb.SetFloat("_Level", levels[row]); liq.SetPropertyBlock(mpb);
                    cam.targetTexture = rt; cam.Render(); cam.targetTexture = null;
                    RenderTexture.active = rt;
                    sheet.ReadPixels(new Rect(0, 0, CW, CH), col * CW, (2 - row) * CH);
                }
            RenderTexture.active = prevA;
            sheet.Apply();
            Directory.CreateDirectory(PREV);
            File.WriteAllBytes(PREV + tag + ".jpg", sheet.EncodeToJPG(90));
            sb.AppendLine("  shot " + PREV + tag + ".jpg (가로 0/45/90/135/180°, 세로 수면 0.192/0.095/0.022, _MapOn " + mapOn + ")");
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(sheet); Object.DestroyImmediate(tmp);
            cam.fieldOfView = f0; cam.nearClipPlane = n0; cam.transform.SetPositionAndRotation(p0, r0);
        }
    }

    static void Flush()
    {
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/pyrite_beerlevel.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
