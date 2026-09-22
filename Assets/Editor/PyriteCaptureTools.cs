// 프리뷰 캡처 — 에디터 전용
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteCaptureTools
{
    struct Shot { public string name; public Vector3 pos; public Vector3 rot; public float fov; }

    static readonly Shot[] SHOTS = new Shot[]
    {
        new Shot{ name="01_spawn",     pos=new Vector3( -2f, 4.23f, 62f),   rot=new Vector3( 5f, 218f, 0f), fov=60f },
        new Shot{ name="02_camp",      pos=new Vector3(-17f, 3.2f, 45f),    rot=new Vector3( 4f,  20f, 0f), fov=62f },
        new Shot{ name="03_meadow_w",  pos=new Vector3(-45f, 4.2f, 55f),    rot=new Vector3( 6f, 130f, 0f), fov=60f },
        new Shot{ name="04_meadow_e",  pos=new Vector3( 42f, 4.2f, 52f),    rot=new Vector3( 6f, 215f, 0f), fov=60f },
        new Shot{ name="05_shore",     pos=new Vector3(  0f, 2.4f, 46f),    rot=new Vector3( 0f, 180f, 0f), fov=70f },
        new Shot{ name="06_aerial",    pos=new Vector3(  0f, 68f, 96f),     rot=new Vector3(38f, 180f, 0f), fov=60f },
        new Shot{ name="07_camp_front",pos=new Vector3(-10.5f,2.6f,44.5f), rot=new Vector3( 6f,   0f, 0f), fov=62f },
        new Shot{ name="08_camp_seat", pos=new Vector3( -9.7f,2.90f,53.7f), rot=new Vector3( 2f, 200f, 0f), fov=68f },
        new Shot{ name="09_camp_top",  pos=new Vector3(-10.5f, 14f, 60f),   rot=new Vector3(46f, 180f, 0f), fov=60f },
        new Shot{ name="10_dock",      pos=new Vector3(-10f, 2.4f, 47.5f),  rot=new Vector3(10f, 180f, 0f), fov=64f },
        new Shot{ name="11_dock_end",  pos=new Vector3(-10f, 1.9f, 29.5f),  rot=new Vector3( 3f,   0f, 0f), fov=64f },
        new Shot{ name="12_flower_close",pos=new Vector3(-30f,4.2f,56f), rot=new Vector3(28f, 150f, 0f), fov=55f },
    };

    [MenuItem("Tools/Pyrite/0. Capture Preview Shots &9")]
    public static void Capture()
    {
        var dir = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "preview");
        Directory.CreateDirectory(dir);

        // 임시 카메라를 새로 만들면 PostProcessLayer가 안 붙어서 포스트가 빠진다.
        // 실제 Main Camera를 잠시 옮겨서 찍고 원위치시킨다.
        Camera cam = Camera.main;
        if (cam == null)
            foreach (var r in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Main Camera") cam = r.GetComponent<Camera>();
        if (cam == null) { Debug.LogError("[Pyrite] Main Camera 못 찾음"); return; }

        var t = cam.transform;
        Vector3 p0 = t.position; Quaternion r0 = t.rotation;
        float fov0 = cam.fieldOfView, near0 = cam.nearClipPlane, far0 = cam.farClipPlane;
        var rt0 = cam.targetTexture;

        // 에디터에서는 파티클이 정지해 있다. 찍기 전에 강제로 시뮬레이션한다.
        var systems = Object.FindObjectsOfType<ParticleSystem>(true);
        foreach (var s0 in systems)
        {
            if (!s0.gameObject.activeInHierarchy) continue;
            s0.Simulate(14f, true, true, true);
        }
        int alive = 0;
        foreach (var s0 in systems) if (s0.gameObject.activeInHierarchy) alive += s0.particleCount;
        if (systems.Length > 0)
        {
            Debug.Log("[Pyrite] 파티클 시스템 " + systems.Length + "개 · 시뮬레이션 후 살아있는 입자 " + alive);
            foreach (var s0 in systems)
            {
                if (!s0.gameObject.activeInHierarchy) continue;
                if (!s0.name.StartsWith("FF_") && s0.name != "WaterMotes") continue;
                var r0b = s0.GetComponent<ParticleSystemRenderer>();
                Debug.Log(string.Format("[Pyrite]   {0}  입자 {1}  셰이더 {2}  바운즈c({3:0.0},{4:0.0},{5:0.0}) s({6:0.0},{7:0.0},{8:0.0})",
                    s0.name, s0.particleCount,
                    r0b != null && r0b.sharedMaterial != null ? r0b.sharedMaterial.shader.name : "없음",
                    r0b.bounds.center.x, r0b.bounds.center.y, r0b.bounds.center.z,
                    r0b.bounds.size.x, r0b.bounds.size.y, r0b.bounds.size.z));
                if (s0.name.StartsWith("FF_B1_1") || alive == 0) break;
            }
        }

        const int W = 1280, H = 720;
        try
        {
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1200f;
            foreach (var s in SHOTS)
            {
                t.position = s.pos;
                t.rotation = Quaternion.Euler(s.rot);
                cam.fieldOfView = s.fov;

                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
                rt.antiAliasing = 1;
                cam.targetTexture = rt;
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                cam.targetTexture = null;

                File.WriteAllBytes(Path.Combine(dir, s.name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
        finally
        {
            cam.targetTexture = rt0;
            t.position = p0; t.rotation = r0;
            cam.fieldOfView = fov0; cam.nearClipPlane = near0; cam.farClipPlane = far0;
            foreach (var s1 in systems) if (s1 != null) s1.Clear(true);
        }
        Debug.Log("[Pyrite] 프리뷰 " + SHOTS.Length + "장 저장(포스트 포함): " + dir);
    }
}
#endif
