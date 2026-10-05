// PyriteBedroomMove.cs — 텐트 침실 자리 옮기기. 재실행 안전
//  Z55d 호수 밑 (0, -40, -14) ← 현행(10-06) / Z55b 상공 (0, 1200, 0) / Z55c 처음 자리 (2000, 0, 0)
//  2026-10-05 관리자: 머리맡 슬라이더가 침실에서만 튐. Z55a 실측: 떨림은 좌표 크기를 따라간다
//   (2000,0,0) 머리맡 가로 13.8 mm → (0,1200,0) 가로 0.01 · 세로 2.6~10 mm
//  2026-10-06 관리자: 상공에서 침실 UI 전부 + VRChat 메뉴까지 이상 → 좌표가 작은 호수 밑으로 (Z55a: 가로 0.01 · 세로 0.33 mm, 캠프 수준)
//   대가: AMB_Water_Center 가 닿음 → PyriteBedroomPanel.muteInRoom 으로 침실 안에서 음소거(이 도구가 연결), 침실 시야에 월드 메시가 들어옴(절두체 상한을 로그에)
//  침실 오브젝트 전부 정적 플래그 없음 · 텔레포트(Spawn)·창밖 카메라·빈백·이불 전부 침실 Transform 기준 → 루트 위치만 바꾼다
//  PyriteBedroomBuild.ORIGIN 도 같은 값(Z49b 를 다시 돌려도 새 자리). 다른 자리로 되돌리면 ORIGIN 도 손으로 되돌릴 것
//  결과 Logs/pyrite_bedroom_move.txt, 렌더 Assets/_preview/bedroom/move_*.jpg
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

public static class PyriteBedroomMove
{
    static readonly Vector3 OLD = new Vector3(2000f, 0f, 0f);
    static readonly Vector3 SKY = new Vector3(0f, 1200f, 0f);
    static readonly Vector3 LAKE = new Vector3(0f, -40f, -14f);
    public const float BACKDROP_SCALE = 40f;   // 창밖 배경 구(Backdrop/BackdropSphere) 지름 = 반지름 20 m. 원래 800(반지름 400 m) — 호수 밑에선 그 안에 월드가 들어와 창으로 비친다(10-06). Pyrite/Backdrop 은 시선 방향만 쓰므로 크기는 모습과 무관(양눈도 방향이 같아 무한 원경)
    const string PREV = "Assets/_preview/bedroom/";
    static StringBuilder sb;
    static GameObject Root(string n) => SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == n);

    [MenuItem("Tools/Pyrite4/Z55b. Bedroom Move To Sky (0,1200,0)", false, 6102)]
    public static void Move() { Do("Z55b", SKY); }

    [MenuItem("Tools/Pyrite4/Z55c. Bedroom Move Revert (2000,0,0)", false, 6103)]
    public static void Revert() { Do("Z55c", OLD); }

    [MenuItem("Tools/Pyrite4/Z55d. Bedroom Move Under Lake (0,-40,-14)", false, 6104)]
    public static void UnderLake() { Do("Z55d", LAKE); }

    static void Do(string tag, Vector3 to)
    {
        sb = new StringBuilder("[" + tag + "] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var temps = new List<Object>();
        try { Inner(tag, to, temps); }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally { foreach (var t in temps) if (t != null) Object.DestroyImmediate(t); }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_bedroom_move.txt", sb.ToString());
        EditorGUIUtility.systemCopyBuffer = sb.ToString();
        Debug.Log(sb.ToString());
    }

    // 침실(room) 밖의 3D 소리 중 도달 거리(AudioSource.maxDistance 와 VRC Far 중 큰 값)가 침실 중심까지 닿는 것. Z50a 도 부른다
    public static AudioSource[] ReachingAudio(Transform room, StringBuilder log)
    {
        Vector3 center = room.position + new Vector3(0f, 1.6f, 0f);
        var hit = new List<AudioSource>();
        var auds = Resources.FindObjectsOfTypeAll<AudioSource>().Where(a => a.gameObject.scene.IsValid() && !a.transform.IsChildOf(room) && a.spatialBlend > 0.01f).OrderBy(a => a.name).ToList();
        foreach (var a in auds)
        {
            float reach = a.maxDistance;
            foreach (var c in a.GetComponents<Component>())
            {
                if (c == null || c.GetType().Name != "VRCSpatialAudioSource") continue;
                var so = new SerializedObject(c); var far = so.FindProperty("Far");
                if (far != null && far.propertyType == SerializedPropertyType.Float && far.floatValue > 0f) reach = Mathf.Max(reach, far.floatValue);
            }
            float d = Vector3.Distance(a.transform.position, center);
            if (reach >= d - 4.5f) { hit.Add(a); if (log != null) log.AppendLine("  닿는 소리 " + a.name + " 거리 " + d.ToString("F0") + " m / 도달 " + reach.ToString("F0") + " m"); }   // 4.5 m = 방 대각 반쪽
        }
        if (log != null) log.AppendLine("침실에 닿는 월드 3D 소리 " + hit.Count + "개 (전체 " + auds.Count + ") → 침실 안에서 음소거");
        return hit.ToArray();
    }

    static void Inner(string tag, Vector3 to, List<Object> temps)
    {
        var room = Root("TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); return; }
        var rt = room.transform;
        // 안전 검사: 정적 플래그가 있으면 중단 (오클루전·배칭이 옛 자리에 묶임)
        int statics = 0;
        foreach (var t in room.GetComponentsInChildren<Transform>(true)) if (GameObjectUtility.GetStaticEditorFlags(t.gameObject) != 0) statics++;
        sb.AppendLine("침실 오브젝트 " + room.GetComponentsInChildren<Transform>(true).Length + "개, 정적 플래그 " + statics + "개");
        if (statics > 0) { sb.AppendLine("!! 정적 오브젝트가 있어 중단"); return; }

        // 안전 검사: 리스폰 높이보다 충분히 위인지
        float respawnY = float.NaN;
        foreach (var c in Resources.FindObjectsOfTypeAll<Component>())
        {
            if (c == null || !c.gameObject.scene.IsValid() || c.GetType().Name != "VRCSceneDescriptor") continue;
            var so = new SerializedObject(c); var p = so.FindProperty("RespawnHeightY");
            if (p != null) respawnY = p.floatValue;
        }
        sb.AppendLine("리스폰 높이 " + respawnY.ToString("F1") + " → 새 바닥 " + to.y.ToString("F1") + " 까지 여유 " + (to.y - respawnY).ToString("F1") + " m");
        if (!float.IsNaN(respawnY) && to.y - respawnY < 5f) { sb.AppendLine("!! 리스폰 높이와 5 m 안 → 중단"); return; }

        var panel = rt.Find("BedroomPanel/PanelCanvas");
        var spawn = rt.Find("Spawn");
        Vector3 from = rt.position;
        sb.AppendLine("이동 전 " + from.ToString("F2") + (spawn ? ", Spawn " + spawn.position.ToString("F2") : ""));

        // 임시 카메라
        var camGo = new GameObject("__moveCam"); temps.Add(camGo); camGo.hideFlags = HideFlags.HideAndDontSave;
        var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 60f; cam.farClipPlane = 1000f;
        var rtBig = new RenderTexture(1920, 1080, 24); temps.Add(rtBig);
        var rtSmall = new RenderTexture(480, 270, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); temps.Add(rtSmall);
        var texA = new Texture2D(480, 270, TextureFormat.RGB24, false); temps.Add(texA);
        var texB = new Texture2D(480, 270, TextureFormat.RGB24, false); temps.Add(texB);
        var rtShot = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); temps.Add(rtShot);
        var texS = new Texture2D(960, 540, TextureFormat.RGB24, false); temps.Add(texS);
        Directory.CreateDirectory(PREV);

        if (panel != null)
        {
            Vector3 pl = rt.InverseTransformPoint(panel.position); Quaternion pr = Quaternion.Inverse(rt.rotation) * panel.rotation;
            foreach (float near in new float[] { 0.05f, 0.02f, 0.01f })
            {
                Vector2 j0 = PyriteBedroomMoveProbe.Jitter(cam, rtBig, from, rt.rotation, pl, pr, near);
                Vector2 jt = PyriteBedroomMoveProbe.Jitter(cam, rtBig, from, rt.rotation, new Vector3(-2.66f, 0.55f, -0.90f), Quaternion.LookRotation(Vector3.left, Vector3.up), near);
                sb.AppendLine("이동 전 떨림 near " + near.ToString("F2") + ": 머리맡 가로 " + j0.x.ToString("F3") + " · 세로 " + j0.y.ToString("F3") + " mm | TV 벽 가로 " + jt.x.ToString("F3") + " · 세로 " + jt.y.ToString("F3") + " mm");
            }
        }
        // 이동 전 침실 안 렌더 (같은 시점) — 옮긴 뒤와 비교
        cam.targetTexture = rtShot; cam.nearClipPlane = 0.02f; cam.cullingMask = ~0; cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false;
        Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(1.2f, 1.5f, 2.0f)), rt.TransformPoint(new Vector3(-0.3f, 0.6f, -2.2f)), "move_" + tag + "_before_room");
        Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(0.3f, 1.3f, -1.6f)), rt.TransformPoint(new Vector3(0f, 1.2f, 2.6f)), "move_" + tag + "_before_window");

        Undo.RecordObject(rt, tag + " bedroom move");
        rt.position = to;
        EditorUtility.SetDirty(rt);
        Physics.SyncTransforms();
        sb.AppendLine("이동 후 " + rt.position.ToString("F2") + (spawn ? ", Spawn " + spawn.position.ToString("F2") : ""));

        if (panel != null)
        {
            Vector3 pl = rt.InverseTransformPoint(panel.position); Quaternion pr = Quaternion.Inverse(rt.rotation) * panel.rotation;
            foreach (float near in new float[] { 0.05f, 0.02f, 0.01f })
            {
                Vector2 j1 = PyriteBedroomMoveProbe.Jitter(cam, rtBig, rt.position, rt.rotation, pl, pr, near);
                Vector2 jt = PyriteBedroomMoveProbe.Jitter(cam, rtBig, rt.position, rt.rotation, new Vector3(-2.66f, 0.55f, -0.90f), Quaternion.LookRotation(Vector3.left, Vector3.up), near);
                sb.AppendLine("이동 후 떨림 near " + near.ToString("F2") + ": 머리맡 가로 " + j1.x.ToString("F3") + " · 세로 " + j1.y.ToString("F3") + " mm | TV 벽 가로 " + jt.x.ToString("F3") + " · 세로 " + jt.y.ToString("F3") + " mm");
            }
        }

        // 창밖 배경 구를 월드보다 안쪽으로 (월드 메시가 구 안에 있으면 창으로 비친다)
        {
            var bs = rt.Find("Backdrop/BackdropSphere");
            if (bs == null) sb.AppendLine("!! Backdrop/BackdropSphere 없음");
            else
            {
                float s0 = bs.localScale.x;
                Undo.RecordObject(bs, tag + " backdrop scale");
                bs.localScale = Vector3.one * BACKDROP_SCALE;
                EditorUtility.SetDirty(bs);
                float rad = BACKDROP_SCALE * 0.5f * bs.lossyScale.x / Mathf.Max(1e-6f, bs.localScale.x);
                float nearest = float.MaxValue; string nearName = "-"; int insideN = 0;
                foreach (var r in Object.FindObjectsOfType<Renderer>())
                {
                    if (!r.enabled || r.transform.IsChildOf(rt) || r.bounds.size.magnitude > 5000f) continue;
                    float d = Mathf.Sqrt(r.bounds.SqrDistance(bs.position));
                    if (d < rad) insideN++;
                    if (d < nearest) { nearest = d; nearName = r.name; }
                }
                float farIn = 0f; string farName = "-";
                foreach (var r in room.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.transform == bs || r is ParticleSystemRenderer) continue;
                    var b = r.bounds; float d = Vector3.Distance(b.center, bs.position) + b.extents.magnitude;
                    if (d > rad * 0.5f) sb.AppendLine("  침실 물체 10 m 밖: " + AnimationUtility.CalculateTransformPath(r.transform, rt) + " 끝 " + d.ToString("F1") + " m, 중심 " + rt.InverseTransformPoint(b.center).ToString("F1") + " 크기 " + b.size.ToString("F1") + (r.gameObject.activeInHierarchy && r.enabled ? " 켜짐" : " 꺼짐"));
                    if (d > farIn) { farIn = d; farName = r.name; }
                }
                sb.AppendLine("배경 구 지름 " + s0.ToString("F0") + " → " + BACKDROP_SCALE.ToString("F0") + " (반지름 " + rad.ToString("F1") + " m, 중심 " + bs.position.ToString("F1") + "). 침실 물체 가장 먼 끝 " + farName + " " + farIn.ToString("F1") + " m, 가장 가까운 월드 렌더러 " + nearName + " " + nearest.ToString("F1") + " m, 구 안쪽 월드 렌더러 " + insideN + "개" + (insideN > 0 || farIn > rad ? "  !! 확인" : ""));
            }
        }

        // 침실에 닿는 월드 소리 → 패널 muteInRoom
        var mute = ReachingAudio(rt, sb);
        var pb = room.GetComponentInChildren<PyriteBedroomPanel>(true);
        if (pb == null) sb.AppendLine("!! PyriteBedroomPanel 없음 — 음소거 연결 못 함");
        else
        {
            Undo.RecordObject(pb, tag + " muteInRoom");
            pb.muteInRoom = mute;
            UdonSharpEditorUtility.CopyProxyToUdon(pb);
            EditorUtility.SetDirty(pb);
            var ub = UdonSharpEditorUtility.GetBackingUdonBehaviour(pb);
            if (ub != null) EditorUtility.SetDirty(ub);
            object back; bool ok = ub != null && ub.publicVariables.TryGetVariableValue("muteInRoom", out back) && back is System.Array && ((System.Array)back).Length == mute.Length;
            sb.AppendLine("패널 muteInRoom " + mute.Length + "개 연결, Udon 쪽 확인 " + (ok ? "OK" : "!! 불일치"));
        }

        // 침실 주변 조건: 닿는 광원 · 반사 프로브 · 지형 아래인지
        Vector3 center = rt.position + new Vector3(0f, 1.6f, 0f);
        int nl = 0;
        foreach (var l in Resources.FindObjectsOfTypeAll<Light>())
        {
            if (!l.gameObject.scene.IsValid() || l.transform.IsChildOf(rt) || !(l.type == LightType.Point || l.type == LightType.Spot)) continue;
            float d = Vector3.Distance(l.transform.position, center);
            if (l.range >= d - 4.5f) { nl++; sb.AppendLine("  닿는 광원 " + l.name + " 거리 " + d.ToString("F0") + " / 범위 " + l.range.ToString("F0")); }
        }
        sb.AppendLine("침실에 닿는 월드 점·스폿 광원 " + nl + "개");
        foreach (var rp in Object.FindObjectsOfType<ReflectionProbe>())
        {
            if (rp.transform.IsChildOf(rt)) continue;
            bool inside = rp.bounds.Contains(center);
            sb.AppendLine("  반사 프로브 " + rp.name + " 경계 " + rp.bounds.min.ToString("F0") + "~" + rp.bounds.max.ToString("F0") + (inside ? "  ← 침실 중심 포함" : ""));
        }
        var terr = Terrain.activeTerrain;
        if (terr != null)
        {
            float gy = terr.SampleHeight(new Vector3(to.x, 0f, to.z)) + terr.transform.position.y;
            sb.AppendLine("침실 위 지형 높이 " + gy.ToString("F2") + " (지형 바닥 " + terr.transform.position.y.ToString("F1") + "), 돔 꼭대기 " + (to.y + 3.4f).ToString("F1") + " → 지형까지 " + (gy - to.y - 3.4f).ToString("F1") + " m");
        }

        // 침실 머리 자리에서 절두체에 들어오는 월드 메시(오클루전 없음 = 상한) + 거울
        {
            var mrs = Object.FindObjectsOfType<Renderer>().Where(m => m.enabled && !m.transform.IsChildOf(rt) && !(m is ParticleSystemRenderer) && m.bounds.size.magnitude < 5000f).ToList();
            var tris = new List<long>();
            foreach (var m in mrs)
            {
                var mf = m.GetComponent<MeshFilter>(); long t = 0;
                if (mf != null && mf.sharedMesh != null) for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) t += mf.sharedMesh.GetIndexCount(i) / 3;
                tris.Add(t);
            }
            var mirrors = mrs.Where(m => m.GetComponents<Component>().Any(c => c != null && c.GetType().Name == "VRCMirrorReflection")).ToList();
            Vector3 head = rt.position + new Vector3(0f, 1.2f, 0f);
            cam.targetTexture = rtBig; cam.nearClipPlane = 0.02f; cam.fieldOfView = 60f;
            string[] dn = { "+Z 창", "+X", "-Z 머리맡", "-X TV", "위 60°", "아래 60°" };
            Quaternion[] dq = { Quaternion.Euler(0, 0, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 270, 0), Quaternion.Euler(-60, 0, 0), Quaternion.Euler(60, 0, 0) };
            for (int k = 0; k < dq.Length; k++)
            {
                cam.transform.SetPositionAndRotation(head, dq[k]);
                var planes = GeometryUtility.CalculateFrustumPlanes(cam);
                long t = 0; int rc = 0;
                for (int i = 0; i < mrs.Count; i++) if (GeometryUtility.TestPlanesAABB(planes, mrs[i].bounds)) { t += tris[i]; rc++; }
                var mv = mirrors.Where(m => m.gameObject.activeInHierarchy && GeometryUtility.TestPlanesAABB(planes, m.bounds)).Select(m => m.name).ToArray();
                sb.AppendLine("  침실 시야 " + dn[k] + ": 월드 삼각형 " + t.ToString("N0") + " (렌더러 " + rc + ")" + (mv.Length > 0 ? " · 절두체 안 켜진 거울 " + string.Join(", ", mv) : ""));
            }
            sb.AppendLine("월드 렌더러 " + mrs.Count + "개 삼각형 합 " + tris.Sum().ToString("N0") + ", 거울 " + mirrors.Count + "개 (" + string.Join(", ", mirrors.Select(m => m.name + (m.gameObject.activeInHierarchy ? "=켬" : "=끔"))) + ")");
        }

        // 월드 시점에서 침실이 보이는지: 침실 켬/끔 두 장의 다른 픽셀 수
        bool fog0 = RenderSettings.fog;
        bool act0 = room.activeSelf;
        try
        {
            cam.targetTexture = rtSmall; cam.nearClipPlane = 0.05f; cam.cullingMask = ~0; cam.clearFlags = CameraClearFlags.Skybox; cam.allowHDR = false;
            var vn = new List<string>(); var vp = new List<Vector3>();
            float Ground(float x, float z) { return terr ? terr.SampleHeight(new Vector3(x, 0f, z)) + terr.transform.position.y : 0f; }
            var sp = Root("VRCWorld"); if (sp) { vn.Add("스폰"); vp.Add(sp.transform.position + Vector3.up * 1.6f); }
            vn.Add("캠프"); vp.Add(new Vector3(-10f, Ground(-10f, 53.5f) + 1.6f, 53.5f));
            vn.Add("부두 끝"); vp.Add(new Vector3(-10f, 2.1f, 31.5f));
            vn.Add("호수 가운데 수면"); vp.Add(new Vector3(0f, 1.6f, -14f));
            vn.Add("호수 가운데 옆 5 m"); vp.Add(new Vector3(5f, 1.6f, -14f));
            vn.Add("호수 바닥 위(물속)"); vp.Add(new Vector3(3f, Ground(3f, -14f) + 0.5f, -11f));
            for (int a = 0; a < 4; a++) { float an = a * 90f * Mathf.Deg2Rad; float x = Mathf.Sin(an) * 70f, z = -14f + Mathf.Cos(an) * 70f; vn.Add("가장자리 " + (a * 90) + "°"); vp.Add(new Vector3(x, Mathf.Max(Ground(x, z), 0f) + 1.6f, z)); }
            int worst = 0; string worstAt = "-";
            for (int i = 0; i < vp.Count; i++)
            {
                Vector3 dir = center - vp[i];
                cam.transform.SetPositionAndRotation(vp[i], Quaternion.LookRotation(dir, Mathf.Abs(Vector3.Dot(dir.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up));
                room.SetActive(true); Grab(cam, rtSmall, texA);
                room.SetActive(false); Grab(cam, rtSmall, texB);
                var a2 = texA.GetPixels32(); var b = texB.GetPixels32(); int n = 0;
                for (int k = 0; k < a2.Length; k++) if (Mathf.Abs(a2[k].r - b[k].r) + Mathf.Abs(a2[k].g - b[k].g) + Mathf.Abs(a2[k].b - b[k].b) > 12) n++;
                sb.AppendLine("  월드 시점 " + vn[i] + " → 침실 방향: 침실 켬/끔 차이 " + n + " px / 129600, 거리 " + Vector3.Distance(vp[i], center).ToString("F0") + " m");
                if (n > worst) { worst = n; worstAt = vn[i]; }
                if (i == 3) { room.SetActive(true); cam.targetTexture = rtShot; Grab(cam, rtShot, texS); File.WriteAllBytes(PREV + "move_" + tag + "_from_lake.jpg", texS.EncodeToJPG(85)); cam.targetTexture = rtSmall; }
            }
            room.SetActive(act0);
            sb.AppendLine("월드에서 보이는 최대 " + worst + " px (" + worstAt + ")");
            // 침실 안 렌더 2장 (머리맡 패널 쪽 / 창 쪽) — 옮기기 전과 같은 모습인지
            cam.targetTexture = rtShot; cam.nearClipPlane = 0.02f;
            Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(1.2f, 1.5f, 2.0f)), rt.TransformPoint(new Vector3(-0.3f, 0.6f, -2.2f)), "move_" + tag + "_room");
            Shot(cam, rtShot, texS, rt.TransformPoint(new Vector3(0.3f, 1.3f, -1.6f)), rt.TransformPoint(new Vector3(0f, 1.2f, 2.6f)), "move_" + tag + "_window");
            cam.targetTexture = null;
        }
        finally { room.SetActive(act0); RenderSettings.fog = fog0; }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE (다른 자리 = Z55b 상공 / Z55c 2000 / Z55d 호수 밑)");
    }

    static void Grab(Camera cam, RenderTexture rt, Texture2D tex)
    {
        cam.Render();
        var act = RenderTexture.active; RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = act;
    }

    static void Shot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 eye, Vector3 at, string name)
    {
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        Grab(cam, rt, tex);
        File.WriteAllBytes(PREV + name + ".jpg", tex.EncodeToJPG(85));
        var px = tex.GetPixels32(); double sum = 0; foreach (var q in px) sum += q.r + q.g + q.b;
        sb.AppendLine("  shot " + name + " 평균 밝기 " + (sum / px.Length / 3).ToString("F1"));
    }
}
#endif
