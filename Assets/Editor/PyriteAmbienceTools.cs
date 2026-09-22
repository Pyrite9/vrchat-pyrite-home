using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

// 환경음 — 물 / 모닥불 / 풀벌레
//   · 오디오 소스는 전부 Ambience 루트 아래에 만든다. 다시 돌리면 통째로 지우고 새로 만든다.
//   · 이름이 AMB_N_ 으로 시작하면 "야행성" 그룹 → 밤에 커지고 새벽에 거의 사라진다.
//   · VRCSpatialAudioSource 를 직접 붙여서 EnableSpatialization 을 꺼 둔다.
//     붙여두지 않으면 VRChat 이 빌드할 때 기본값(Far 40, 공간화 ON)으로 알아서 붙여버려서
//     여기서 잡은 Unity 감쇠 커브가 통째로 무시된다.
public static class PyriteAmbienceTools
{
    const string AUDIO_DIR = "Assets/Audio/";

    class A
    {
        public string name, clip;
        public float x, y, z;        // y: snap 이면 지형 위 높이, 아니면 절대 높이
        public float vol, near, far;
        public bool  snap;
        public A(string n, string c, float x, float y, float z, float v, float near, float far, bool snap)
        { name=n; clip=c; this.x=x; this.y=y; this.z=z; vol=v; this.near=near; this.far=far; this.snap=snap; }
    }

    // 꽃밭은 호수 중심(0,-14)에서 반지름 56.5~62 고리다. 풀벌레는 그 위에 올린다.
    static readonly A[] SPEC =
    {
        new A("AMB_Water_Center", "A_Lake",      0.0f,  0.50f, -14.0f, 0.30f, 25.0f, 120.0f, false),
        new A("AMB_Water_ShoreN", "A_Lake",      0.0f,  0.80f,  42.0f, 0.42f,  4.0f,  30.0f, false),
        new A("AMB_Water_Dock",   "A_Lake",    -10.0f,  0.60f,  33.0f, 0.40f,  3.0f,  24.0f, false),
        new A("AMB_Campfire",     "A_Campfire",-10.5f,  0.30f,  51.5f, 0.55f,  1.2f,  15.0f, true ),
        new A("AMB_N_Crickets_A", "A_Crickets",-18.0f,  1.40f,  45.0f, 0.38f,  6.0f,  42.0f, true ),
        new A("AMB_N_Crickets_B", "A_Crickets",  5.0f,  1.40f,  46.0f, 0.34f,  6.0f,  42.0f, true ),
    };

    // 프리셋별 배수 — 순서는 노을 / 밤 / 새벽
    public static readonly float[] MUL_DAY   = { 1.00f, 0.85f, 0.90f };   // 물·모닥불
    public static readonly float[] MUL_NIGHT = { 0.35f, 1.00f, 0.12f };   // 풀벌레

    [MenuItem("Tools/Pyrite/K. Setup Ambience", false, 260)]
    public static void Setup()
    {
        foreach (var c in SPEC) ImportClip(c.clip);
        AssetDatabase.Refresh();

        // PyriteHome 은 씬 이름이지 오브젝트가 아니다. Ambience 는 씬 최상위에 만든다.
        var old = FindRoot("Ambience");
        if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var amb = new GameObject("Ambience");

        var terrain = Terrain.activeTerrain;
        int made = 0;

        foreach (var s in SPEC)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AUDIO_DIR + s.clip + ".wav");
            if (clip == null) { Debug.LogError("[AMB] 클립 없음: " + s.clip); continue; }

            float y = s.y;
            if (s.snap && terrain != null)
                y = terrain.SampleHeight(new Vector3(s.x, 0f, s.z)) + terrain.transform.position.y + s.y;

            var go = new GameObject(s.name);
            go.transform.SetParent(amb.transform, false);
            go.transform.position = new Vector3(s.x, y, s.z);

            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = true;
            a.volume = s.vol;
            a.spatialBlend = 1.0f;                       // 완전 3D
            a.dopplerLevel = 0f;                         // 환경음에 도플러는 방해만 된다
            a.rolloffMode = AudioRolloffMode.Custom;
            a.minDistance = s.near;
            a.maxDistance = s.far;
            // near 까지 1.0, 그 뒤로 부드럽게 0 — 로그 감쇠는 멀리서도 안 꺼져서 겹칠 때 지저분하다
            var curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, 1f, 0f, 0f));
            curve.AddKey(new Keyframe(s.near / s.far, 1f, 0f, 0f));
            curve.AddKey(new Keyframe(Mathf.Lerp(s.near / s.far, 1f, 0.45f), 0.38f));
            curve.AddKey(new Keyframe(1f, 0f, 0f, 0f));
            for (int k = 0; k < curve.length; k++)
                AnimationUtility.SetKeyLeftTangentMode(curve, k, AnimationUtility.TangentMode.ClampedAuto);
            a.SetCustomCurve(AudioSourceCurveType.CustomRolloff, curve);

            // 같은 클립을 여러 군데서 틀면 위상이 맞아 웅웅거린다. 시작 지점을 흩어 놓는다.
            a.time = (made * 3.77f) % Mathf.Max(0.1f, clip.length);

            AddSpatial(go);
            made++;
        }

        var tod = UnityEngine.Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod != null) { Wire(tod); EditorUtility.SetDirty(tod); }

        EditorSceneMark();
        Debug.Log(string.Format("[AMB] 완료 — 소스 {0}개 / 야행성 {1}개 / TimeOfDay 배선 {2}",
            made, CountNight(), tod != null ? "O" : "X (TimeOfDay 없음)"));
    }

    static GameObject FindRoot(string name)
    {
        foreach (var r in SceneManager.GetActiveScene().GetRootGameObjects())
            if (r.name == name) return r;
        return null;
    }

    static int CountNight() { int n = 0; foreach (var s in SPEC) if (s.name.StartsWith("AMB_N_")) n++; return n; }

    // J. Setup Time Of Day 에서도 이걸 부른다 — 두 메뉴를 어느 순서로 돌려도 배선이 유지되도록.
    public static void Wire(PyriteTimeOfDay tod)
    {
        var ambGo = FindRoot("Ambience");
        Transform amb = ambGo == null ? null : ambGo.transform;
        if (amb == null)
        {
            tod.ambience = new AudioSource[0];       tod.ambienceMul      = MUL_DAY;
            tod.nightAmbience = new AudioSource[0];  tod.nightAmbienceMul = MUL_NIGHT;
            return;
        }
        var all = amb.GetComponentsInChildren<AudioSource>(true);
        var day = new System.Collections.Generic.List<AudioSource>();
        var ngt = new System.Collections.Generic.List<AudioSource>();
        foreach (var a in all) (a.gameObject.name.StartsWith("AMB_N_") ? ngt : day).Add(a);
        tod.ambience         = day.ToArray();
        tod.ambienceMul      = MUL_DAY;
        tod.nightAmbience    = ngt.ToArray();
        tod.nightAmbienceMul = MUL_NIGHT;
    }

    static void ImportClip(string name)
    {
        string path = AUDIO_DIR + name + ".wav";
        var imp = AssetImporter.GetAtPath(path) as AudioImporter;
        if (imp == null) { Debug.LogWarning("[AMB] 임포터 없음: " + path); return; }
        imp.forceToMono = true;
        imp.loadInBackground = false;
        var ss = imp.defaultSampleSettings;
        ss.loadType          = AudioClipLoadType.DecompressOnLoad;   // 짧은 루프라 통째로 풀어두는 게 낫다
        ss.compressionFormat = AudioCompressionFormat.Vorbis;
        ss.quality           = 0.55f;
        ss.sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate;
        imp.defaultSampleSettings = ss;
        imp.SaveAndReimport();
    }

    static void AddSpatial(GameObject go)
    {
        Type t = FindType("VRC.SDK3.Components.VRCSpatialAudioSource")
              ?? FindType("VRC.SDKBase.VRC_SpatialAudioSource");
        if (t == null) { Debug.LogWarning("[AMB] VRCSpatialAudioSource 타입을 못 찾음 — 수동 확인 필요"); return; }
        var c = go.GetComponent(t) ?? go.AddComponent(t);
        SetMember(c, "EnableSpatialization", false);   // Unity 커브를 그대로 쓰겠다는 뜻
        SetMember(c, "Gain", 0f);
        SetMember(c, "UseAudioSourceVolumeCurve", true);
    }

    static Type FindType(string full)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        { var t = asm.GetType(full); if (t != null) return t; }
        return null;
    }

    static void SetMember(object o, string name, object v)
    {
        var t = o.GetType();
        var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.FieldType == v.GetType()) { f.SetValue(o, v); return; }
        var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType == v.GetType()) p.SetValue(o, v, null);
    }

    static void EditorSceneMark()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
