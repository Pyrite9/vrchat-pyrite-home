// Tools ▸ Pyrite ▸ Z13. Dock Plank Detail (normal + AO)  /  Z13b. Revert
//  인게임: 밤 부두 판자가 밋밋하다. 밤 조명은 달빛 0.04 + 색조 곱한 간접광이라 거의 균일 → 재질에 입체감이 없으면 한 톤.
//  M_DockWood 에 노멀맵·AO 가 없었다(알베도 한 장, smoothness 0.18).
//  Tools/python/mk_dock_maps.py 로 만든 두 장을 붙인다 (판자 한 장 = 텍스처 띠 1개, u 1 = 2.40 m, 띠 폭 0.235 m 기준)
//   T_DockWood_N.png  — 둥근 모서리 R 12 mm, 가운데 1.2 mm 휨, 판자별 비틀림, 알베도 고주파에서 뽑은 결 0.6 mm
//   T_DockWood_AO.png — 판자 틈 25 mm 안에서 0.55 까지, 결 골 살짝. Pyrite/StandardNight 에 _OcclusionMap 추가(간접광에만)
//  [Z13c 후] 밤 부두 밝기의 대부분은 PyriteNight 의 밤 하늘빛(반구)인데 AO·방향을 무시했다 → cginc 에서 AO 적용 + _NightAmbTilt.
//   스윕(밤, 판자 행 평균 표준편차 rowSd): 맵 없음 3.2 → AO 3.7 → +bump2 3.9 → +tilt0.8 4.2. 적용값 bump 2, tilt 0.8, AO 1
//  재베이크 불필요(메타 패스는 알베도만 본다. 방향성 라이트맵·AO 는 런타임 적용). 전후 렌더 Assets/_preview/dock/
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteDockDetail
{
    const string MAT = "Assets/Materials/M_DockWood.mat";
    const string NRM = "Assets/TerrainAssets/T_DockWood_N.png";
    const string AO = "Assets/TerrainAssets/T_DockWood_AO.png";

    [MenuItem("Tools/Pyrite/Z13. Dock Plank Detail (normal + AO)", false, 25)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z13] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (m == null) { sb.AppendLine("M_DockWood 없음"); Flush(sb); return; }
        sb.AppendLine("shader " + m.shader.name + "  has _OcclusionMap " + m.HasProperty("_OcclusionMap"));

        sb.Append(PyriteDockLightmap.CaptureTagged("detail_before"));

        Import(NRM, true, sb);
        Import(AO, false, sb);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(NRM);
        var a = AssetDatabase.LoadAssetAtPath<Texture2D>(AO);
        Undo.RecordObject(m, "dock detail");
        m.SetTexture("_BumpMap", n);
        m.SetFloat("_BumpScale", 2f);                                   // Z13c 스윕: 1 → 2 에서 판자 모서리가 밤에 조금 더 선다
        if (m.HasProperty("_NightAmbTilt")) m.SetFloat("_NightAmbTilt", 0.8f);   // 밤 하늘빛을 달 쪽으로 — 노멀 음영이 밤에 보이게
        m.EnableKeyword("_NORMALMAP");
        if (m.HasProperty("_OcclusionMap")) { m.SetTexture("_OcclusionMap", a); m.SetFloat("_OcclusionStrength", 1f); }
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        sb.AppendLine(string.Format("M_DockWood _BumpMap {0}, _OcclusionMap {1}", n ? n.name : "null", a ? a.name : "null"));

        sb.Append(PyriteDockLightmap.CaptureTagged("detail_after"));
        sb.AppendLine("RESULT: DONE");
        Flush(sb);
    }

    [MenuItem("Tools/Pyrite/Z13b. Dock Plank Detail Revert", false, 26)]
    public static void Revert()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(MAT);
        if (m == null) return;
        m.SetTexture("_BumpMap", null);
        if (m.HasProperty("_NightAmbTilt")) m.SetFloat("_NightAmbTilt", 0f);
        m.DisableKeyword("_NORMALMAP");
        if (m.HasProperty("_OcclusionMap")) m.SetTexture("_OcclusionMap", null);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        Flush(new StringBuilder("[Z13b] reverted\n"));
    }

    static void Import(string path, bool normal, StringBuilder sb)
    {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) { sb.AppendLine("임포터 없음 " + path); return; }
        ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        ti.sRGBTexture = false;
        ti.wrapMode = TextureWrapMode.Repeat;
        ti.mipmapEnabled = true;
        ti.anisoLevel = 8;                       // 부두는 비스듬히 보인다 — 판자 결이 뭉개지지 않게
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.CompressedHQ;
        ti.SaveAndReimport();
        sb.AppendLine(string.Format("  import {0} normal={1} sRGB=off aniso 8", path, normal));
    }

    static void Flush(StringBuilder sb) { Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_dockdetail.txt", sb.ToString()); }
}
#endif
