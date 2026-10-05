// PyriteBedroomMoveProbe.cs — 침실을 원점 가까이로 옮길 때의 후보 자리 실측 (Z55a, 읽기 전용 · 씬 저장 안 함)
//  2026-10-05 관리자: 머리맡 슬라이더가 침실(2000 m)에서만 튐, 캠프(55 m)는 멀쩡 → 원점 거리 의심
//  후보마다: ① 포인터 떨림(임시 카메라의 ScreenPointToRay 왕복, 머리 0.1 mm 씩 이동) ② 닿는 소리·조명 ③ 월드 시점에서 보이는 픽셀 ④ 침실 안 시야에 들어오는 월드 삼각형(오클루전 없음 = 상한)
//  결과 Logs/pyrite_bedroom_move_probe.txt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteBedroomMoveProbe
{
    static StringBuilder sb;
    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);
    static string V(Vector3 v) => "(" + v.x.ToString("F0") + ", " + v.y.ToString("F0") + ", " + v.z.ToString("F0") + ")";

    [MenuItem("Tools/Pyrite4/Z55a. Bedroom Move Probe (read-only)", false, 6101)]
    public static void Run()
    {
        sb = new StringBuilder("[Z55a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        bool fog0 = RenderSettings.fog;
        var temps = new List<Object>();
        try { Inner(temps); sb.AppendLine("RESULT: DONE (씬 변경 없음)"); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            RenderSettings.fog = fog0;
            foreach (var t in temps) if (t != null) Object.DestroyImmediate(t);
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_move_probe.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    static void Inner(List<Object> temps)
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var rt0 = room.transform;
        var panel = rt0.Find("BedroomPanel/PanelCanvas");
        if (panel == null) { sb.AppendLine("!! PanelCanvas 없음"); return; }
        Vector3 panelLocal = rt0.InverseTransformPoint(panel.position);
        Quaternion panelRotLocal = Quaternion.Inverse(rt0.rotation) * panel.rotation;
        sb.AppendLine("현재 침실 " + rt0.position.ToString("F2") + " 회전 " + rt0.eulerAngles.ToString("F1") + ", 패널(방 기준) " + panelLocal.ToString("F3"));

        // 방 크기 (렌더러 경계)
        var rr = room.GetComponentsInChildren<Renderer>(false);
        Bounds rb = new Bounds(rt0.position, Vector3.zero);
        bool first = true;
        foreach (var r in rr)
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.bounds.size.magnitude > 60f) continue;
            if (first) { rb = r.bounds; first = false; } else rb.Encapsulate(r.bounds);
        }
        Vector3 boxSize = rb.size;
        Vector3 boxOff = rb.center - rt0.position;
        sb.AppendLine("방 경계 크기 " + boxSize.ToString("F2") + " 중심 오프셋 " + boxOff.ToString("F2"));

        var mainCam = Camera.main;
        sb.AppendLine("기준 카메라 near " + (mainCam ? mainCam.nearClipPlane.ToString("F3") : "?") + " far " + (mainCam ? mainCam.farClipPlane.ToString("F0") : "?"));

        // 후보
        var names = new List<string>(); var pos = new List<Vector3>();
        void C(string n, float x, float y, float z) { names.Add(n); pos.Add(new Vector3(x, y, z)); }
        C("현재 2000", 2000, 0, 0);
        C("캠프 자리(대조)", -10, 1.8f, 53.5f);
        C("+X 600", 600, 0, 0);
        C("+X 300", 300, 0, 0);
        C("+X 200", 200, 0, 0);
        C("+X 200 지하30", 200, -30, 0);
        C("+X 150 지하30", 150, -30, 0);
        C("+Z 200", 0, 0, 200);
        C("+Z 200 지하30", 0, -30, 200);
        C("대각 140/140 지하30", 140, -30, 140);
        C("호수 밑 지하40", 0, -40, -14);
        C("상공 150", 0, 150, 0);
        C("상공 1200", 0, 1200, 0);
        C("상공 2000", 0, 2000, 0);
        C("캠프 위 상공 1200", -10, 1200, 54);
        C("+Z 1200", 0, 0, 1200);
        C("+Z 2000", 0, 0, 2000);

        // 임시 카메라들
        var camGo = new GameObject("__probeCam"); temps.Add(camGo); camGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 60f; cam.farClipPlane = 1000f;
        var rtBig = new RenderTexture(1920, 1080, 24); temps.Add(rtBig);
        var rtSmall = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); temps.Add(rtSmall);
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube); temps.Add(box); box.hideFlags = HideFlags.HideAndDontSave;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        var mat = new Material(Shader.Find("Unlit/Color")); temps.Add(mat); mat.color = new Color(1f, 0f, 1f, 1f);
        box.GetComponent<Renderer>().sharedMaterial = mat;
        box.transform.localScale = boxSize;
        var tex = new Texture2D(480, 270, TextureFormat.RGB24, false); temps.Add(tex);

        // 월드 시점
        var vn = new List<string>(); var vp = new List<Vector3>();
        var terr = Terrain.activeTerrain;
        float Ground(float x, float z) { return terr ? terr.SampleHeight(new Vector3(x, 0, z)) + terr.transform.position.y : 0f; }
        void Vw(string n, float x, float z, float minY) { vn.Add(n); vp.Add(new Vector3(x, Mathf.Max(Ground(x, z), minY) + 1.6f, z)); }
        var spawn = Root("VRCWorld");
        if (spawn) { vn.Add("스폰"); vp.Add(spawn.transform.position + Vector3.up * 1.6f); }
        Vw("캠프", -10f, 53.5f, 0f); Vw("부두 끝", -10f, 31.5f, 0.5f); Vw("호수 가운데", 0f, -14f, 0f);
        for (int a = 0; a < 8; a++) { float an = a * 45f * Mathf.Deg2Rad; Vw("가장자리 " + (a * 45) + "°", Mathf.Sin(an) * 70f, -14f + Mathf.Cos(an) * 70f, 0f); }

        // 소리·조명 목록
        var auds = Resources.FindObjectsOfTypeAll<AudioSource>().Where(a => a.gameObject.scene.IsValid() && !a.transform.IsChildOf(rt0) && a.spatialBlend > 0.01f).ToList();
        var audReach = new List<float>();
        foreach (var a in auds)
        {
            float reach = a.maxDistance;
            foreach (var c in a.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "VRCSpatialAudioSource") continue;
                var so = new SerializedObject(c); var far = so.FindProperty("Far");
                if (far != null && far.propertyType == SerializedPropertyType.Float && far.floatValue > 0f) reach = Mathf.Max(reach, far.floatValue);
            }
            audReach.Add(reach);
        }
        var lights = Resources.FindObjectsOfTypeAll<Light>().Where(l => l.gameObject.scene.IsValid() && !l.transform.IsChildOf(rt0) && (l.type == LightType.Point || l.type == LightType.Spot)).ToList();
        sb.AppendLine("월드 3D 소리 " + auds.Count + "개 (가장 먼 도달 " + (audReach.Count > 0 ? audReach.Max().ToString("F0") : "0") + " m), 점·스폿 광원 " + lights.Count + "개");

        // 월드 메시 (침실 밖, 켜진 것)
        var mrs = Object.FindObjectsOfType<MeshRenderer>().Where(m => m.enabled && !m.transform.IsChildOf(rt0)).ToList();
        var mrTris = new List<long>();
        foreach (var m in mrs)
        {
            var mf = m.GetComponent<MeshFilter>(); long t = 0;
            if (mf != null && mf.sharedMesh != null) for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) t += mf.sharedMesh.GetIndexCount(i) / 3;
            mrTris.Add(t);
        }
        sb.AppendLine("월드 메시 렌더러 " + mrs.Count + "개, 삼각형 합 " + mrTris.Sum().ToString("N0"));
        sb.AppendLine();
        sb.AppendLine("떨림 = 머리가 0.1 mm 씩 200 걸음 움직일 때, 패널 위 고정점을 화면 좌표로 바꿨다 되돌린 위치가 흔들리는 폭(peak-to-peak, mm). 머리맡 슬라이더 길이 820 mm");
        sb.AppendLine();

        RenderSettings.fog = false;
        for (int ci = 0; ci < pos.Count; ci++)
        {
            Vector3 P = pos[ci];
            sb.AppendLine("■ " + names[ci] + " " + V(P) + "  원점 거리 " + P.magnitude.ToString("F0") + " m");
            // ① 떨림
            // 머리맡 패널(가로축 = 월드 X) 과 TV 벽 패널(−X 벽, 가로축 = 월드 Z) 각각, 가로/세로 떨림
            Vector3 tvLocal = new Vector3(-2.66f, 0.55f, -0.90f);
            Quaternion tvRotLocal = Quaternion.LookRotation(Vector3.left, Vector3.up);
            foreach (float near in new float[] { 0.05f, 0.02f, 0.01f })
            {
                Vector2 h = Jitter(cam, rtBig, P, rt0.rotation, panelLocal, panelRotLocal, near);
                Vector2 tv = Jitter(cam, rtBig, P, rt0.rotation, tvLocal, tvRotLocal, near);
                sb.AppendLine("  ① near " + near.ToString("F2") + ": 머리맡 가로 " + h.x.ToString("F3") + " · 세로 " + h.y.ToString("F3") + " mm | TV 벽 가로 " + tv.x.ToString("F3") + " · 세로 " + tv.y.ToString("F3") + " mm");
            }
            // ② 소리·조명
            Vector3 center = P + boxOff;
            var hitA = new List<string>();
            for (int i = 0; i < auds.Count; i++)
            {
                float d = Vector3.Distance(auds[i].transform.position, center);
                if (audReach[i] >= d) hitA.Add(auds[i].name + "(" + d.ToString("F0") + "/" + audReach[i].ToString("F0") + ")");
            }
            var hitL = new List<string>();
            foreach (var l in lights) { float d = Vector3.Distance(l.transform.position, center); if (l.range >= d - boxSize.magnitude * 0.5f) hitL.Add(l.name + "(" + d.ToString("F0") + "/" + l.range.ToString("F0") + ")"); }
            sb.AppendLine("  ② 닿는 소리 " + hitA.Count + (hitA.Count > 0 ? ": " + string.Join(", ", hitA.Take(8)) + (hitA.Count > 8 ? " …" : "") : "") + " | 닿는 광원 " + hitL.Count + (hitL.Count > 0 ? ": " + string.Join(", ", hitL.Take(5)) : ""));
            // ③ 월드 시점에서 보이는 픽셀
            box.transform.position = center; box.transform.rotation = rt0.rotation;
            box.SetActive(true);
            cam.nearClipPlane = 0.05f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; cam.cullingMask = ~0; cam.targetTexture = rtSmall; cam.allowHDR = false;
            int maxPx = 0; string maxAt = "-"; int seen = 0;
            for (int vi = 0; vi < vp.Count; vi++)
            {
                if ((vp[vi] - center).magnitude < 3f) continue;
                cam.transform.SetPositionAndRotation(vp[vi], Quaternion.LookRotation(center - vp[vi]));
                cam.Render();
                var act = RenderTexture.active; RenderTexture.active = rtSmall;
                tex.ReadPixels(new Rect(0, 0, 480, 270), 0, 0); tex.Apply(); RenderTexture.active = act;
                var px = tex.GetPixels32(); int n = 0;
                foreach (var q in px) if (q.r > 110 && q.b > 110 && q.g < q.r / 2) n++;
                if (n > 0) seen++;
                if (n > maxPx) { maxPx = n; maxAt = vn[vi]; }
            }
            box.SetActive(false);
            sb.AppendLine("  ③ 월드 시점 " + vp.Count + "곳 중 보이는 곳 " + seen + ", 최대 " + maxPx + " px / 129600 (" + maxAt + ")");
            // ④ 침실 안 시야의 월드 삼각형 (오클루전 없음)
            cam.targetTexture = rtBig; cam.nearClipPlane = 0.02f;
            long maxT = 0; int maxR = 0; long within = 0;
            Vector3 head = P + new Vector3(0f, 1.2f, 0f);
            for (int i = 0; i < mrs.Count; i++) if (mrs[i].bounds.SqrDistance(head) < 1000f * 1000f && mrs[i].bounds.size.magnitude < 5000f) within += mrTris[i];
            for (int y = 0; y < 4; y++)
            {
                cam.transform.SetPositionAndRotation(head, Quaternion.Euler(0f, y * 90f, 0f));
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                long t = 0; int rc = 0;
                for (int i = 0; i < mrs.Count; i++)
                {
                    if (mrs[i].bounds.size.magnitude > 5000f) continue;
                    if (GeometryUtility.TestPlanesAABB(planes, mrs[i].bounds)) { t += mrTris[i]; rc++; }
                }
                if (t > maxT) { maxT = t; maxR = rc; }
            }
            sb.AppendLine("  ④ 침실 머리 위치 시야(4방향 중 최대) 월드 삼각형 " + maxT.ToString("N0") + " (렌더러 " + maxR + "), far 1000 안 전체 " + within.ToString("N0"));
            sb.AppendLine();
        }
        cam.targetTexture = null;
    }

    // 임시 카메라를 후보 자리의 침실 머리 위치에 두고(패널 앞 1.3 m, 패널 가로 방향으로 0.1 mm 씩 이동), 패널 위 고정점을 WorldToScreenPoint → ScreenPointToRay 로 왕복시켜 패널 기준 x·y 가 흔들리는 폭(mm)
    public static Vector2 Jitter(Camera cam, RenderTexture rt, Vector3 roomPos, Quaternion roomRot, Vector3 panelLocal, Quaternion panelRotLocal, float near)
    {
        cam.targetTexture = rt; cam.nearClipPlane = near; cam.fieldOfView = 60f;
        Vector3 pw = roomPos + roomRot * panelLocal;
        Quaternion prot = roomRot * panelRotLocal;
        Vector3 nrm = prot * Vector3.forward;
        Vector3 head0 = pw - nrm * 1.30f + prot * new Vector3(0.10f, 0.15f, 0f);
        Quaternion look = Quaternion.LookRotation(pw - head0);
        Vector3 target = pw + prot * new Vector3(0.12f, 0.05f, 0f);
        const int N = 200;
        double xlo = double.MaxValue, xhi = double.MinValue, ylo = double.MaxValue, yhi = double.MinValue;
        for (int k = 0; k < N; k++)
        {
            Vector3 hp = head0 + prot * new Vector3(k * 0.0001f, 0f, 0f);
            cam.transform.SetPositionAndRotation(hp, look);
            Vector3 sp = cam.WorldToScreenPoint(target);
            Ray r = cam.ScreenPointToRay(new Vector3(sp.x, sp.y, 0f));
            var plane = new Plane(nrm, pw);
            float t;
            if (!plane.Raycast(r, out t)) { plane = new Plane(-nrm, pw); plane.Raycast(r, out t); }
            Vector3 hit = r.GetPoint(t);
            Vector3 loc = Quaternion.Inverse(prot) * (hit - pw);
            if (loc.x < xlo) xlo = loc.x; if (loc.x > xhi) xhi = loc.x;
            if (loc.y < ylo) ylo = loc.y; if (loc.y > yhi) yhi = loc.y;
        }
        return new Vector2((float)((xhi - xlo) * 1000.0), (float)((yhi - ylo) * 1000.0));
    }
}
#endif
