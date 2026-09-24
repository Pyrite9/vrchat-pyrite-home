// Tools ▸ Pyrite2 ▸ Z38a. Build SFX / Z38b. SFX Revert
//  상호작용 효과음 (2026-09-24): 물수제비 물보라 · 머그 따르기/마시기 · 마시멜로 한입. 소리는 Python 으로 만든 절차적 WAV (Assets/Audio/SFX)
//  기존 오브젝트에 AudioSource(+VRC 공간 음향)만 붙이고 스크립트 필드를 채운다. Z31b(캠프 소품)·Z34b(분위기 FX)를 다시 돌리면 지워지므로 그 뒤 Z38a 를 다시
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PyriteSfx
{
    const string DIR = "Assets/Audio/SFX/";
    static StringBuilder sb;

    [MenuItem("Tools/Pyrite2/Z38a. Build SFX", false, 120)]
    public static void Build()
    {
        sb = new StringBuilder("[Z38a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        try { Inner(); sb.AppendLine("RESULT: DONE"); } catch (System.Exception e) { sb.AppendLine("EXCEPTION " + e); }
        Flush();
    }

    static AudioClip Clip(string name)
    {
        string p = DIR + name + ".wav";
        var ai = AssetImporter.GetAtPath(p) as AudioImporter;
        if (ai == null) { sb.AppendLine("  !! 없음 " + p); return null; }
        ai.forceToMono = true; ai.loadInBackground = false;
        var st = ai.defaultSampleSettings; st.loadType = AudioClipLoadType.DecompressOnLoad; st.compressionFormat = AudioCompressionFormat.Vorbis; st.quality = 0.6f; st.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
        st.preloadAudioData = true;   // 2026-09-24: 꺼져 있으면 빌드(VRChat)에서 PlayOneShot 이 무음 — 에디터는 항상 로드돼 있어 들림
        ai.defaultSampleSettings = st; ai.SaveAndReimport();
        var c = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
        sb.AppendLine(string.Format("  clip {0} {1:0.00}s preload {2} load {3}", name, c != null ? c.length : 0f, ai.defaultSampleSettings.preloadAudioData, ai.defaultSampleSettings.loadType));
        return c;
    }

    static AudioSource Src(GameObject go, float maxDist)
    {
        var a = go.GetComponent<AudioSource>(); if (a == null) a = go.AddComponent<AudioSource>();
        a.playOnAwake = false; a.loop = false; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Logarithmic;
        a.minDistance = 0.6f; a.maxDistance = maxDist; a.dopplerLevel = 0f; a.volume = 1f; a.clip = null;
        var sp = go.GetComponent<VRC.SDK3.Components.VRCSpatialAudioSource>(); if (sp == null) sp = go.AddComponent<VRC.SDK3.Components.VRCSpatialAudioSource>();
        var so = new SerializedObject(sp);
        // 2026-09-24: 인게임 무음 → 들리는 환경음(AMB_*)과 같은 설정으로. Spatialization 1 / VolumeCurve 0 이던 6개만 안 들렸다
        foreach (var (n, v) in new[] { ("Gain", 0f), ("Near", 0f), ("Far", maxDist) }) { var pr = so.FindProperty(n); if (pr != null) pr.floatValue = v; }
        var e = so.FindProperty("EnableSpatialization"); if (e != null) e.boolValue = false;
        var u = so.FindProperty("UseAudioSourceVolumeCurve"); if (u != null) u.boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        return a;
    }

    static void Inner()
    {
        var splash = Clip("SFX_Splash"); var pour = Clip("SFX_Pour"); var sip = Clip("SFX_Sip"); var bite = Clip("SFX_Bite");

        var sp = GameObject.Find("AmbientFX/SkipStones/SkipSplash");
        var stones = Object.FindObjectsOfType<PyriteSkipStone>(true);
        if (sp != null)
        {
            var a = Src(sp, 30f);
            foreach (var s in stones) { s.splashAudio = a; s.splashClip = splash; UdonSharpEditorUtility.CopyProxyToUdon(s); EditorUtility.SetDirty(s); }
            sb.AppendLine("  splash → " + stones.Length + " stones");
        }
        else sb.AppendLine("  !! SkipSplash 없음 (Z34b 먼저)");

        foreach (var m in Object.FindObjectsOfType<PyriteMugState>(true))
        {
            m.audioSrc = Src(m.gameObject, 10f); m.pourClip = pour; m.sipClip = sip;
            UdonSharpEditorUtility.CopyProxyToUdon(m); EditorUtility.SetDirty(m);
            sb.AppendLine("  mug " + m.transform.parent.name + "/" + m.name);
        }
        foreach (var k in Object.FindObjectsOfType<PyriteSkewerState>(true))
        {
            k.audioSrc = Src(k.gameObject, 8f); k.biteClip = bite;
            UdonSharpEditorUtility.CopyProxyToUdon(k); EditorUtility.SetDirty(k);
            sb.AppendLine("  skewer " + k.transform.parent.name + "/" + k.name);
        }
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("Tools/Pyrite2/Z38b. SFX Revert", false, 121)]
    public static void Revert()
    {
        sb = new StringBuilder("[Z38b] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        foreach (var s in Object.FindObjectsOfType<PyriteSkipStone>(true)) { s.splashAudio = null; s.splashClip = null; UdonSharpEditorUtility.CopyProxyToUdon(s); EditorUtility.SetDirty(s); }
        foreach (var m in Object.FindObjectsOfType<PyriteMugState>(true)) { Strip(m.gameObject); m.audioSrc = null; m.pourClip = null; m.sipClip = null; UdonSharpEditorUtility.CopyProxyToUdon(m); EditorUtility.SetDirty(m); }
        foreach (var k in Object.FindObjectsOfType<PyriteSkewerState>(true)) { Strip(k.gameObject); k.audioSrc = null; k.biteClip = null; UdonSharpEditorUtility.CopyProxyToUdon(k); EditorUtility.SetDirty(k); }
        var sp = GameObject.Find("AmbientFX/SkipStones/SkipSplash"); if (sp != null) Strip(sp);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene()); EditorSceneManager.SaveOpenScenes();
        sb.AppendLine("RESULT: DONE"); Flush();
    }

    static void Strip(GameObject go)
    {
        var sp = go.GetComponent<VRC.SDK3.Components.VRCSpatialAudioSource>(); if (sp != null) Object.DestroyImmediate(sp);
        var a = go.GetComponent<AudioSource>(); if (a != null) Object.DestroyImmediate(a);
    }

    static void Flush() { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_sfx.txt", sb.ToString()); }
}
#endif
