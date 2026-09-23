// Tools ▸ Pyrite ▸ Y9. Capture Check Views
//  고정 시점 × 시간대 3종을 Main Camera(포스트 포함)로 렌더 → Assets/_preview/views/<시점>_<i>.png
//  작업 전후 비교·수치 측정용. 끝나면 시간대를 노을(0)로 되돌린다.
#if UNITY_EDITOR
using System.IO;
using UdonSharpEditor;
using UnityEditor;
using UnityEngine;

public static class PyriteViews
{
    public const string OUT = "Assets/_preview/views/";

    struct V { public string n; public Vector3 eye, look; public float fov; public bool eyeOnGround; }

    static readonly V[] Views =
    {
        // 관리자 스크린샷 1 — 캠프에서 호수 정면
        new V{ n="camp_lake",  eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3(-10.0f, 3.0f, -40.0f), fov=70f, eyeOnGround=true },
        // 관리자 스크린샷 2 — 물가에서 중앙 기둥 방향
        new V{ n="shore",      eye=new Vector3( 12.0f, 1.7f, 36.0f), look=new Vector3(-13.0f, 2.0f, -60.0f), fov=70f, eyeOnGround=true },
        // 캠프에서 왼쪽 기둥 방향
        new V{ n="camp_left",  eye=new Vector3(-10.0f, 1.7f, 47.0f), look=new Vector3( 56.0f, 4.0f,  18.0f), fov=60f, eyeOnGround=true },
        // 중앙 기둥 근접 (캠프 쪽에서)
        new V{ n="lm1",        eye=new Vector3(-10.0f, 3.0f, -52.0f), look=new Vector3(-13.0f, 5.0f, -73.0f), fov=55f, eyeOnGround=true },
        // 왼쪽 기둥 근접 (캠프 쪽에서)
        new V{ n="lm2",        eye=new Vector3( 38.0f, 3.0f,  28.0f), look=new Vector3( 56.0f, 4.0f,  18.0f), fov=55f, eyeOnGround=true },
        // 물가 선 내려다보기
        new V{ n="shoreline",  eye=new Vector3(  2.0f, 3.5f, 44.0f), look=new Vector3( -6.0f, 0.0f,  30.0f), fov=60f, eyeOnGround=true },
        // 호수 건너에서 캠프 쪽(+Z) 벽 — 안쪽 계단(Y7) 확인
        new V{ n="north_wall", eye=new Vector3(  5.0f, 1.7f,-40.0f), look=new Vector3( -5.0f, 6.0f,  78.0f), fov=70f, eyeOnGround=true },
        // 캠프 동쪽에서 서쪽(-X) 벽
        new V{ n="west_wall",  eye=new Vector3( 25.0f, 1.7f, 45.0f), look=new Vector3(-78.0f, 5.0f,  20.0f), fov=65f, eyeOnGround=true },
    };

    [MenuItem("Tools/Pyrite/Y9. Capture Check Views", false, 299)]
    public static void Capture() { CaptureSet(null); }

    public static void CaptureSet(string only) { CaptureSet(only, new[] { 0, 1, 2 }); }

    public static void CaptureSet(string only, int[] presets)
    {
        Directory.CreateDirectory(OUT);
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        if (cam == null) { Debug.LogError("[Y9] Main Camera 없음"); return; }
        var t = Terrain.activeTerrain;

        // ToD 는 첫 Apply 때 반딧불·캠프 조명 기준값을 캐시한다. 배열을 바꾼 뒤엔 캐시를 비워야 한다.
        // (캡처는 항상 노을(0)로 끝나므로, 여기 들어올 때 씬은 기준 상태다)
        if (tod != null)
        {
            var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(tod, false);
        }
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var tt = cam.targetTexture; float n0 = cam.nearClipPlane, fa0 = cam.farClipPlane;
        try
        {
            foreach (int i in presets)
            {
                if (tod != null) { tod.index = i; tod.Apply(); }
                // 에디터에선 파티클이 재생되지 않는다 — 몇 초 진행시켜 반딧불이 찍히게
                foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
                    if (ps.emission.enabled && ps.emission.rateOverTime.constantMax > 0.001f) ps.Simulate(8f, true, true, true);
                    else ps.Clear(true);
                foreach (var v in Views)
                {
                    if (only != null && !only.Contains(v.n)) continue;
                    var eye = v.eye;
                    if (v.eyeOnGround && t != null) eye.y += Mathf.Max(0f, t.SampleHeight(new Vector3(eye.x, 0f, eye.z)) + t.transform.position.y);   // 물 위(수면 y=0)면 수면 기준
                    Render(cam, eye, v.look, v.fov, OUT + v.n + "_" + i + ".png");
                }
            }
        }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0);
            cam.fieldOfView = f0; cam.targetTexture = tt; cam.nearClipPlane = n0; cam.farClipPlane = fa0;
            if (tod != null) { tod.index = 0; tod.Apply(); }
        }
        AssetDatabase.Refresh();
        Debug.Log("[Y9] 저장 완료 — " + OUT);
    }

    static void Render(Camera cam, Vector3 eye, Vector3 look, float fov, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 1000f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
