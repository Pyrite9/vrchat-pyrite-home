// Tools ▸ Pyrite2 ▸ Z30e. Boat Water Mask Test
//  물결이 배 안까지 올라온 상황을 흉내: 보트를 잠시 25 cm 내려(안쪽 바닥이 수면 아래) 가림막 켬/끔을 렌더하고 되돌린다 (씬 변경 없음)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PyriteBoatMaskTest
{
    [MenuItem("Tools/Pyrite2/Z30e. Boat Water Mask Test", false, 44)]
    public static void Run()
    {
        var boat = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.scene.IsValid() && g.name == "Boat");
        if (boat == null) return;
        var mask = boat.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WaterMask");
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var pos0 = boat.transform.position;
        Directory.CreateDirectory("Assets/_preview/boat/");
        try
        {
            cyc.ResetCache(); cyc.EvaluateAt(12f);
            boat.transform.position = pos0 - Vector3.up * 0.25f;
            var c = boat.transform.position;
            var eye = c + new Vector3(2.6f, 1.9f, 4.2f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(c + new Vector3(0f, 0.1f, 0.3f) - eye));
            cam.fieldOfView = 50f;
            foreach (var on in new[] { false, true })
            {
                if (mask) mask.gameObject.SetActive(on);
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var tx = new Texture2D(1280, 720, TextureFormat.RGB24, false); tx.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tx.Apply(); RenderTexture.active = null;
                File.WriteAllBytes("Assets/_preview/boat/masktest_" + (on ? "on" : "off") + ".png", tx.EncodeToPNG());
                cam.targetTexture = null; Object.DestroyImmediate(tx); rt.Release(); Object.DestroyImmediate(rt);
            }
        }
        finally
        {
            if (mask) mask.gameObject.SetActive(true);
            boat.transform.position = pos0;
            cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = null;
            cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR);
        }
    }
}
#endif
