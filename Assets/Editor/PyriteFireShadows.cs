// Tools ▸ Pyrite ▸ Z8. Campfire + Lantern Shadows
//  밤엔 태양(달빛)이 0.04 라 그림자가 없다. 캠프를 비추는 건 모닥불·랜턴인데 둘 다 그림자 None 이었다.
//   · 모닥불 Fire_Light: Soft 그림자, 범위 3 → 5.5 m (의자·테이블·타프까지 그림자가 닿게), 픽셀 라이트 고정
//   · 들고 다니는 랜턴 Light: Soft 그림자 — 들고 걸으면 소품 그림자가 따라 움직인다
//   · 광원을 감싸는 자기 메시(화로 받침·랜턴 유리/틀)는 그림자를 안 드리우게 — 안 그러면 빛을 통째로 가린다
//  실시간 광원만 바꾸므로 재베이크 불필요. 되돌리기: Z8b
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteFireShadows
{
    const float FIRE_RANGE = 5.5f;

    [MenuItem("Tools/Pyrite/Z8. Campfire + Lantern Shadows", false, 13)]
    public static void Run() { Apply(true); }

    [MenuItem("Tools/Pyrite/Z8b. Campfire + Lantern Shadows Revert", false, 14)]
    public static void Revert() { Apply(false); }

    static void Apply(bool on)
    {
        var sb = new StringBuilder(on ? "[Z8] " : "[Z8b] ");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var camp = roots.FirstOrDefault(g => g.name == "Camp");
        if (camp == null) { Debug.LogError("[Z8] Camp 없음"); return; }
        var fireRoot = camp.transform.Find("camp05_campfire_stand");
        var lantern = camp.transform.Find("Lantern_Carry");
        var fire = fireRoot != null ? fireRoot.GetComponentsInChildren<Light>(true).FirstOrDefault(l => l.type == LightType.Point) : null;
        var lanLight = lantern != null ? lantern.GetComponentsInChildren<Light>(true).FirstOrDefault() : null;

        if (fire != null)
        {
            Undo.RecordObject(fire, "fire shadow");
            fire.shadows = on ? LightShadows.Soft : LightShadows.None;
            fire.shadowStrength = 0.85f;
            fire.shadowNearPlane = 0.1f;
            fire.shadowBias = 0.05f; fire.shadowNormalBias = 0.4f;
            fire.range = on ? FIRE_RANGE : 3f;
            fire.renderMode = on ? LightRenderMode.ForcePixel : LightRenderMode.Auto;
            sb.Append("fire range ").Append(fire.range).Append(" shadows ").Append(fire.shadows).Append(" | ");
            foreach (var r in fireRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Undo.RecordObject(r, "fire caster");
                r.shadowCastingMode = on ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
        }
        if (lanLight != null)
        {
            Undo.RecordObject(lanLight, "lantern shadow");
            lanLight.shadows = on ? LightShadows.Soft : LightShadows.None;
            lanLight.shadowStrength = 0.8f;
            lanLight.shadowNearPlane = 0.1f;
            lanLight.shadowBias = 0.05f; lanLight.shadowNormalBias = 0.4f;
            sb.Append("lantern shadows ").Append(lanLight.shadows).Append(" | ");
            foreach (var r in lantern.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                Undo.RecordObject(r, "lantern caster");
                r.shadowCastingMode = on ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
        }
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        // 밤 근접 렌더 (Z5 와 같은 시점)
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        var cam = Camera.main;
        if (tod != null && cam != null)
        {
            var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(tod, false);
            var p0 = cam.transform.position; var r0 = cam.transform.rotation; var f0 = cam.fieldOfView; var tt = cam.targetTexture;
            try
            {
                tod.index = 1; tod.Apply();
                foreach (var ps in Object.FindObjectsOfType<ParticleSystem>())
                    if (ps.emission.enabled && ps.emission.rateOverTime.constantMax > 0.001f) ps.Simulate(4f, true, true, true);
                Shot(cam, new Vector3(-4f, 4.5f, 60f), new Vector3(-11f, 1.8f, 54f), "Assets/_preview/night_camp" + (on ? "" : "_off") + ".png");
                Shot(cam, new Vector3(-12.5f, 3.2f, 47.0f), new Vector3(-11f, 1.8f, 54f), "Assets/_preview/night_camp2" + (on ? "" : "_off") + ".png");
                Shot(cam, new Vector3(-7.0f, 6.5f, 49.5f), new Vector3(-11.5f, 1.8f, 53.5f), "Assets/_preview/night_camp3" + (on ? "" : "_off") + ".png");
            }
            finally
            {
                cam.transform.SetPositionAndRotation(p0, r0); cam.fieldOfView = f0; cam.targetTexture = tt;
                if (f != null) f.SetValue(tod, false);
                tod.index = 0; tod.Apply();
            }
            AssetDatabase.Refresh();
        }
        Debug.Log(sb.ToString());
        File.WriteAllText("Assets/_preview/fireshadows.txt", sb.ToString());
    }

    static void Shot(Camera cam, Vector3 eye, Vector3 look, string path)
    {
        const int W = 1600, H = 900;
        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation((look - eye).normalized, Vector3.up));
        cam.fieldOfView = 60f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt; cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToPNG());
        cam.targetTexture = null; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
    }
}
#endif
