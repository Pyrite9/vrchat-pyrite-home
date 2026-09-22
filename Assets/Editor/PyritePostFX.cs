// 포스트 프로세싱 세팅 (에디터 전용)
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.SceneManagement;

public static class PyritePostFX
{
    const int  PP_LAYER = 23;                 // 23~31은 VRChat이 안 쓰는 자유 슬롯
    const string PP_LAYER_NAME = "PostProcessing";
    const string PROFILE_PATH = "Assets/TerrainAssets/PP_PyriteDusk.asset";

    static void EnsureLayer()
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0];
        var so = new SerializedObject(asset);
        var layers = so.FindProperty("layers");
        var el = layers.GetArrayElementAtIndex(PP_LAYER);
        if (el.stringValue != PP_LAYER_NAME)
        {
            el.stringValue = PP_LAYER_NAME;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("[Pyrite] 레이어 " + PP_LAYER + " → \"" + PP_LAYER_NAME + "\"");
        }
    }

    static T Add<T>(PostProcessProfile p) where T : PostProcessEffectSettings
    {
        if (p.HasSettings<T>()) return p.GetSetting<T>();
        return p.AddSettings<T>();
    }

    [MenuItem("Tools/Pyrite/F. Setup Post Processing &#6")]
    public static void Setup()
    {
        EnsureLayer();
        if (!AssetDatabase.IsValidFolder("Assets/TerrainAssets"))
            AssetDatabase.CreateFolder("Assets", "TerrainAssets");

        // ---------- 프로파일 ----------
        var prof = AssetDatabase.LoadAssetAtPath<PostProcessProfile>(PROFILE_PATH);
        if (prof == null)
        {
            prof = ScriptableObject.CreateInstance<PostProcessProfile>();
            AssetDatabase.CreateAsset(prof, PROFILE_PATH);
        }

        var bloom = Add<Bloom>(prof);
        bloom.active = true;
        bloom.enabled.Override(true);
        bloom.intensity.Override(2.0f);
        bloom.threshold.Override(0.90f);
        bloom.softKnee.Override(0.6f);
        bloom.diffusion.Override(7.5f);
        bloom.anamorphicRatio.Override(0f);
        bloom.color.Override(new Color(1f, 0.93f, 0.84f, 1f));
        bloom.fastMode.Override(false);

        var cg = Add<ColorGrading>(prof);
        cg.active = true;
        cg.enabled.Override(true);
        cg.gradingMode.Override(GradingMode.HighDefinitionRange);
        cg.tonemapper.Override(Tonemapper.ACES);
        cg.postExposure.Override(1.15f);
        cg.temperature.Override(8f);
        cg.tint.Override(-3f);
        cg.contrast.Override(3f);
        cg.saturation.Override(4f);
        // 섀도우는 푸르게, 하이라이트는 따뜻하게 — 노을 대비를 살린다
        cg.lift.Override(new Vector4(0.97f, 1.00f, 1.07f, 0.015f));
        cg.gamma.Override(new Vector4(1f, 1f, 1f, 0f));
        cg.gain.Override(new Vector4(1.05f, 1.01f, 0.95f, 0.02f));

        var vig = Add<Vignette>(prof);
        vig.active = true;
        vig.enabled.Override(true);
        vig.mode.Override(VignetteMode.Classic);
        vig.intensity.Override(0.24f);
        vig.smoothness.Override(0.45f);
        vig.roundness.Override(1f);
        vig.rounded.Override(false);
        vig.color.Override(new Color(0.03f, 0.03f, 0.05f, 1f));

        EditorUtility.SetDirty(prof);
        AssetDatabase.SaveAssets();

        // ---------- 전역 볼륨 ----------
        GameObject volGo = null;
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == "PostProcessVolume") volGo = r;
        if (volGo == null)
        {
            volGo = new GameObject("PostProcessVolume");
            Undo.RegisterCreatedObjectUndo(volGo, "pp volume");
        }
        volGo.layer = PP_LAYER;
        volGo.transform.position = Vector3.zero;
        var vol = volGo.GetComponent<PostProcessVolume>();
        if (vol == null) vol = Undo.AddComponent<PostProcessVolume>(volGo);
        Undo.RecordObject(vol, "pp volume");
        vol.isGlobal = true;
        vol.priority = 0f;
        vol.weight = 1f;
        vol.blendDistance = 0f;
        vol.sharedProfile = prof;
        EditorUtility.SetDirty(vol);

        // ---------- 카메라 ----------
        var cam = Camera.main;
        if (cam == null)
            foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
                if (r.name == "Main Camera") cam = r.GetComponent<Camera>();
        if (cam == null) { Debug.LogError("[Pyrite] Main Camera 못 찾음"); return; }

        var lay = cam.GetComponent<PostProcessLayer>();
        if (lay == null) lay = Undo.AddComponent<PostProcessLayer>(cam.gameObject);
        Undo.RecordObject(lay, "pp layer");
        lay.volumeLayer = 1 << PP_LAYER;
        lay.volumeTrigger = cam.transform;
        lay.antialiasingMode = PostProcessLayer.Antialiasing.SubpixelMorphologicalAntialiasing;
        lay.subpixelMorphologicalAntialiasing.quality =
            SubpixelMorphologicalAntialiasing.Quality.High;
        lay.stopNaNPropagation = true;
        lay.finalBlitToCameraTarget = false;
        EditorUtility.SetDirty(lay);

        // ---------- VRC Scene Descriptor의 Reference Camera ----------
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            var sd = r.GetComponent<VRC.SDK3.Components.VRCSceneDescriptor>();
            if (sd == null) continue;
            if (sd.ReferenceCamera != cam.gameObject)
            {
                Undo.RecordObject(sd, "ref cam");
                sd.ReferenceCamera = cam.gameObject;
                EditorUtility.SetDirty(sd);
                Debug.Log("[Pyrite] VRC Scene Descriptor ▸ Reference Camera = Main Camera");
            }
            else Debug.Log("[Pyrite] Reference Camera 이미 Main Camera로 설정돼 있음");
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Pyrite] 포스트 프로세싱 세팅 완료 — Bloom 2.4/1.05, ACES ColorGrading, Vignette 0.24, SMAA High, 레이어 " + PP_LAYER);
    }
}
#endif
