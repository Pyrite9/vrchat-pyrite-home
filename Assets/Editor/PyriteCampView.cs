using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// 관객 시점(모닥불 쪽)에서 캠프를 렌더해 PNG 로 저장한다 — 씬 뷰를 손으로 돌리는 것보다 정확하다
public static class PyriteCampView
{
    const string OUT = "Assets/_preview/";

    [MenuItem("Tools/Pyrite/N6. Capture Camp View", false, 275)]
    public static void Capture()
    {
        Directory.CreateDirectory(OUT);
        var terrain = Terrain.activeTerrain;

        // (위치, 바라보는 지점, 파일명)
        Shot(terrain, new Vector3(-10.5f, 1.55f, 50.0f), new Vector3(-12.75f, 2.4f, 57.6f), "camp_seat");      // 의자에서 화면 쪽
        Shot(terrain, new Vector3(-10.5f, 1.70f, 48.0f), new Vector3(-13.5f, 2.2f, 57.0f), "camp_wide");       // 좀 더 뒤에서 넓게
        Shot(terrain, new Vector3(-14.0f, 1.60f, 52.5f), new Vector3(-12.75f, 2.6f, 57.6f), "camp_side");      // 비스듬히
        Shot(terrain, new Vector3(-9.3f,  1.60f, 53.0f), new Vector3(-8.5f,  1.2f, 58.2f), "camp_tarp");       // 타프 안쪽(간이침대)
        Shot(terrain, new Vector3(-10.0f, 1.40f, 40.0f), new Vector3(-10.0f, 0.9f, 31.5f), "dock_end");        // 부두 끝
        Shot(terrain, new Vector3(-9.3f,  7.00f, 50.5f), new Vector3(-9.3f,  1.8f, 58.0f), "tarp_top");        // 타프 위에서 내려다보기
        AssetDatabase.Refresh();
        Debug.Log("[VIEW] 저장 완료 — " + OUT);
    }

    static void Shot(Terrain t, Vector3 eye, Vector3 look, string name)
    {
        if (t != null)
        {
            float g = t.SampleHeight(new Vector3(eye.x, 0f, eye.z)) + t.transform.position.y;
            eye.y += g;    // 지면 위 눈높이
        }

        var go = new GameObject("__shot");
        var cam = go.AddComponent<Camera>();
        cam.transform.position = eye;
        cam.transform.rotation = Quaternion.LookRotation((look - eye).normalized, Vector3.up);
        cam.fieldOfView = 62f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 900f;
        cam.clearFlags = CameraClearFlags.Skybox;
        cam.allowHDR = true;

        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        rt.antiAliasing = 4;
        cam.targetTexture = rt;
        cam.Render();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        File.WriteAllBytes(OUT + name + ".png", tex.EncodeToPNG());

        cam.targetTexture = null;
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);
    }
}
