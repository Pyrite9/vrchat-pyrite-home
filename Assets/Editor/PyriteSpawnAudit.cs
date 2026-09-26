// Tools ▸ Pyrite2 ▸ Z42a. Spawn Table Audit  (씬 변경 없음)
//  타프 뒤 새 테이블 자리 실측: 지형 높이 · 위에서 쏜 레이(계단·절벽 콜라이더) 격자, 확인 렌더 3장, U# 컴파일 에러 목록
//  결과 Logs/pyrite_spawn.txt, 렌더 Assets/_preview/spawn/audit_*.png
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteSpawnAudit
{
    [MenuItem("Tools/Pyrite2/Z42a. Spawn Table Audit", false, 150)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z42a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(sb); sb.AppendLine("RESULT: DONE"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_spawn.txt", sb.ToString());
    }

    public static void CompileErrors(StringBuilder sb)
    {
        int nErr = 0, nAsset = 0;
        foreach (var g in AssetDatabase.FindAssets("t:UdonSharpProgramAsset"))
        {
            var pa = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(g));
            if (pa == null) continue; nAsset++;
            var f = pa.GetType().GetField("compileErrors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            var list = f != null ? f.GetValue(pa) as System.Collections.IList : null;
            if (list == null || list.Count == 0) continue;
            foreach (var e in list) { sb.AppendLine("  U# ERROR " + pa.name + ": " + e); nErr++; }
        }
        sb.AppendLine("U# program assets " + nAsset + ", errors " + nErr);
    }

    static void Inner(StringBuilder sb)
    {
        CompileErrors(sb);
        var terr = Terrain.activeTerrain;
        float TH(Vector3 p) => terr ? terr.SampleHeight(p) + terr.transform.position.y : float.NaN;

        // 1) 격자: x -16..-2, z 59..67 (0.5 m). 칸 = 지형 높이 - 1.81 (cm), 레이가 지형 아닌 콜라이더에 먼저 맞으면 '*'
        int mask = ~((1 << 4) | (1 << 5) | (1 << 13) | (1 << 18) | (1 << 19));   // Water, UI, Pickup, 로컬 플레이어류 제외
        sb.AppendLine("grid: 행 z, 열 x -16..-2 (0.5 m), 값 = 지형 - 1.81 (cm), *=다른 콜라이더가 위(이름은 아래)");
        var other = new System.Collections.Generic.Dictionary<string, string>();
        for (float z = 67f; z >= 59f - 0.01f; z -= 0.5f)
        {
            var row = new StringBuilder(string.Format("z {0,5:0.0} |", z));
            for (float x = -16f; x <= -2f + 0.01f; x += 0.5f)
            {
                var p = new Vector3(x, 0, z); float th = TH(p);
                string mark = " ";
                if (Physics.Raycast(new Vector3(x, 60f, z), Vector3.down, out var hit, 80f, mask, QueryTriggerInteraction.Ignore))
                {
                    if (!(hit.collider is TerrainCollider) && hit.point.y > th + 0.02f)
                    {
                        mark = "*";
                        string k = hit.collider.name;
                        if (!other.ContainsKey(k)) other[k] = string.Format("first ({0:0.0},{1:0.0}) top {2:0.00}", x, z, hit.point.y);
                    }
                }
                row.Append(string.Format("{0,4:0}{1}", (th - 1.81f) * 100f, mark));
            }
            sb.AppendLine(row.ToString());
        }
        foreach (var kv in other) sb.AppendLine("  collider " + kv.Key + " " + kv.Value);
        sb.AppendLine(string.Format("R from lake centre (0,-14): (-9.3,61.2) {0:0.0} | (-9.3,62.5) {1:0.0} | (-9.3,63.5) {2:0.0}",
            new Vector2(-9.3f, 75.2f).magnitude, new Vector2(-9.3f, 76.5f).magnitude, new Vector2(-9.3f, 77.5f).magnitude));

        // 2) 참고 물건
        foreach (var n in new[] { "Camp/camp02_hexa_tarp_GRN", "Camp/camp03_table", "CarryChair", "CarryChair_1", "CarryChair_2", "CampProps/PicnicMat", "Cot", "PyriteTerraces" })
        {
            var go = GameObject.Find(n); if (go == null) { sb.AppendLine(n + " 없음"); continue; }
            var rs = go.GetComponentsInChildren<Renderer>(false).Where(r => !(r is ParticleSystemRenderer)).ToArray();
            string bs = "";
            if (rs.Length > 0) { var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); bs = b.min.ToString("F2") + " .. " + b.max.ToString("F2"); }
            sb.AppendLine(string.Format("{0} pos {1} rot {2} scale {3} | {4} | cols {5} | static {6}", n, go.transform.position.ToString("F2"), go.transform.eulerAngles.ToString("F0"),
                go.transform.localScale.ToString("F2"), bs, go.GetComponentsInChildren<Collider>(true).Length, GameObjectUtility.GetStaticEditorFlags(go)));
        }
        var tbl = GameObject.Find("Camp/camp03_table");
        if (tbl) foreach (var c in tbl.GetComponentsInChildren<Component>(true)) sb.AppendLine("   table comp " + c.GetType().Name + " on " + c.name);

        // 3) 렌더 (21:00 + 12:00, 파티클 끔)
        Directory.CreateDirectory("Assets/_preview/spawn/");
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView; bool o0 = cam.orthographic; float s0 = cam.orthographicSize;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var psr = Object.FindObjectsOfType<ParticleSystemRenderer>().Where(r => r.enabled).ToArray();
        foreach (var r in psr) r.enabled = false;
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(12f); }
            cam.orthographic = true; cam.orthographicSize = 7f;
            cam.transform.SetPositionAndRotation(new Vector3(-9.3f, 40f, 60.5f), Quaternion.Euler(90f, 0f, 0f));
            Shot(cam, "Assets/_preview/spawn/audit_top.png", 1200, 1200);
            cam.orthographic = false; cam.fieldOfView = 60f;
            var e1 = new Vector3(-9.3f, 3.5f, 51.0f); cam.transform.SetPositionAndRotation(e1, Quaternion.LookRotation(new Vector3(-9.3f, 2.2f, 62.5f) - e1));
            Shot(cam, "Assets/_preview/spawn/audit_fromcamp.png", 1280, 720);
            var e2 = new Vector3(-6.5f, 3.4f, 66.0f); cam.transform.SetPositionAndRotation(e2, Quaternion.LookRotation(new Vector3(-9.5f, 2.0f, 60.5f) - e2));
            Shot(cam, "Assets/_preview/spawn/audit_behind.png", 1280, 720);
        }
        finally
        {
            foreach (var r in psr) r.enabled = true;
            cam.orthographic = o0; cam.orthographicSize = s0; cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
        }
        sb.AppendLine("shots Assets/_preview/spawn/audit_{top,fromcamp,behind}.png");
    }

    public static void Shot(Camera cam, string path, int w, int h)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt; var tx = new Texture2D(w, h, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, w, h), 0, 0); tx.Apply(); RenderTexture.active = null;
        File.WriteAllBytes(path, tx.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
