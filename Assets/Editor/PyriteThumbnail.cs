// Tools ▸ Pyrite2 ▸ Z39a. Thumbnail Candidates — VRChat 월드 썸네일 후보(1200×900, 4:3) 몇 장을 Assets/_preview/thumbnail/ 에
//  관리자가 SDK 창 'Select Image' 로 고른다 (업로드는 관리자)
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class PyriteThumbnail
{
    [MenuItem("Tools/Pyrite2/Z39a. Thumbnail Candidates", false, 130)]
    public static void Run()
    {
        const string OUT = "Assets/_preview/thumbnail/"; Directory.CreateDirectory(OUT);
        var cam = Camera.main; var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var shots = new (string n, float h, Vector3 eye, Vector3 at, float fov)[]
        {
            ("A_camp_lake_1850", 18.5f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-8.0f, 2.6f, 30.0f), 55f),
            ("B_camp_lake_1900", 19.0f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-8.0f, 2.6f, 30.0f), 55f),
            ("C_dock_camp_1850", 18.5f, new Vector3(-7.8f, 1.9f, 30.2f), new Vector3(-10.5f, 3.2f, 52.0f), 55f),
            ("D_camp_night_2130", 21.5f, new Vector3(-13.5f, 3.9f, 57.5f), new Vector3(-9.5f, 2.6f, 45.0f), 55f),
        };
        try
        {
            foreach (var s in shots)
            {
                cyc.ResetCache(); cyc.EvaluateAt(s.h);
                cam.fieldOfView = s.fov; cam.transform.SetPositionAndRotation(s.eye, Quaternion.LookRotation(s.at - s.eye));
                const int W = 1200, H = 900;
                var rt = new RenderTexture(W, H, 24); rt.antiAliasing = 4; cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tx = new Texture2D(W, H, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, W, H), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes(OUT + s.n + ".png", tx.EncodeToPNG());
                cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        finally
        {
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
        AssetDatabase.Refresh();
        File.AppendAllText("Logs/pyrite_sfx.txt", "[Z39a] " + System.DateTime.Now.ToString("HH:mm:ss") + " thumbnails " + shots.Length + "\nRESULT: DONE\n");
    }
}
#endif
