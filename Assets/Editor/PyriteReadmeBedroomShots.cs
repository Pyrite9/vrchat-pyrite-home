// PyriteReadmeBedroomShots.cs — README 용 텐트 침실 사진 (Z52n). 저장소 루트 docs/images/ 에 JPG 1600×900 (Assets 밖)
//  bedroom_night    : 21시, 방 전경 (기본 빈백 2개)
//  bedroom_lounge   : 빈백 6개 + TV 켬, 침대 쪽에서 러그를 봄
//  bedroom_sleep    : 수면 모드 100% 흉내 (침실 광원 × 0.05, 창 _Dim 0.4, 별 조명 최대, 줄전구 × 0.3), 누운 자리에서 천장
//  씬은 끝나면 원래대로 (광원 세기 · 빈백 켜짐 · TV · 머티리얼 값)
//  ⚠ Z41b(README 이미지 정리)는 README 에 없는 jpg 를 지운다 → 이 세 장은 README 에 넣어야 남는다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteReadmeBedroomShots
{
    const string OUT = "docs/images/";

    [MenuItem("Tools/Pyrite3/Z52n. README Bedroom Shots", false, 5140)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z52n] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var room = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "TentBedroom");
        if (room == null) { sb.AppendLine("!! TentBedroom 없음"); Flush(sb); return; }
        var o = room.transform;
        var cam = Camera.main; var p0 = cam.transform.position; var r0 = cam.transform.rotation; float f0 = cam.fieldOfView;
        var cyc = Object.FindObjectOfType<PyriteDayCycle>();
        var lights = o.Find("Lights") ? o.Find("Lights").GetComponentsInChildren<Light>(true) : new Light[0]; var baseI = lights.Select(l => l.intensity).ToArray();
        var star = o.Find("Mood/StarLamp/StarLight") ? o.Find("Mood/StarLamp/StarLight").GetComponent<Light>() : null; float star0 = star ? star.intensity : 0f; bool starOn0 = star && star.enabled;
        var bd = AssetDatabase.LoadAssetAtPath<Material>("Assets/Bedroom/M_Backdrop.mat"); float dim0 = bd && bd.HasProperty("_Dim") ? bd.GetFloat("_Dim") : 1f;
        var sm = AssetDatabase.LoadAssetAtPath<Material>(PyriteBedroomMood.DIR + "M_StringBulb.mat"); Color se0 = sm ? sm.GetColor("_EmissionColor") : Color.black;
        var bags = o.Find("Beanbags") ? o.Find("Beanbags").Cast<Transform>().Select(t => t.gameObject).ToArray() : new GameObject[0]; var bag0 = bags.Select(b => b.activeSelf).ToArray();
        var tv = o.Find("BedroomPanel/BedroomTV"); bool tv0 = tv && tv.gameObject.activeSelf;
        Directory.CreateDirectory(OUT);
        try
        {
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(21f); }
            Vector3 W(float x, float y, float z) => o.TransformPoint(new Vector3(x, y, z));
            // 1. 전경
            Shot(cam, W(2.35f, 1.75f, 2.30f), W(-0.6f, 0.55f, -0.9f), 60f, "bedroom_night", sb);
            // 2. 빈백 6 + TV
            foreach (var b in bags) b.SetActive(true);
            if (tv) tv.gameObject.SetActive(true);
            Shot(cam, W(1.30f, 1.45f, -0.55f), W(-0.9f, 0.45f, 1.30f), 62f, "bedroom_lounge", sb);
            for (int i = 0; i < bags.Length; i++) bags[i].SetActive(bag0[i]);
            if (tv) tv.gameObject.SetActive(tv0);
            // 3. 수면 100%
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i] * 0.05f;
            if (star) { star.enabled = true; star.intensity = PyriteBedroomMood.STAR_MAX; }
            if (bd && bd.HasProperty("_Dim")) bd.SetFloat("_Dim", 0.4f);
            if (sm) sm.SetColor("_EmissionColor", PyriteBedroomMood.STRING_EMIT * 0.3f);
            Shot(cam, W(-0.28f, 0.50f, -1.95f), W(0.10f, 2.90f, 0.30f), 75f, "bedroom_sleep", sb);
            sb.AppendLine("RESULT: DONE");
        }
        catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        finally
        {
            for (int i = 0; i < lights.Length; i++) lights[i].intensity = baseI[i];
            if (star) { star.intensity = star0; star.enabled = starOn0; }
            if (bd && bd.HasProperty("_Dim")) bd.SetFloat("_Dim", dim0);
            if (sm) sm.SetColor("_EmissionColor", se0);
            for (int i = 0; i < bags.Length; i++) bags[i].SetActive(bag0[i]);
            if (tv) tv.gameObject.SetActive(tv0);
            cam.fieldOfView = f0; cam.transform.SetPositionAndRotation(p0, r0);
            if (cyc) { cyc.ResetCache(); cyc.EvaluateAt(PyriteDayCycleSetup.EDITOR_HOUR); }
            Flush(sb);
        }
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 at, float fov, string name, StringBuilder sb)
    {
        cam.fieldOfView = fov;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye));
        int w = 1600, h = 900;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 8);
        var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
        var a = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
        RenderTexture.active = a; RenderTexture.ReleaseTemporary(rt);
        var px = tex.GetPixels32(); double lum = px.Average(c => 0.2126 * c.r + 0.7152 * c.g + 0.0722 * c.b);
        File.WriteAllBytes(OUT + name + ".jpg", tex.EncodeToJPG(90));
        sb.AppendLine("  " + name + ".jpg 평균 밝기 " + lum.ToString("F1"));
        Object.DestroyImmediate(tex);
    }

    static void Flush(StringBuilder sb)
    {
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_readme_bedroom.txt", sb.ToString(), new UTF8Encoding(false));
    }
}
#endif
