// Tools ▸ Pyrite2 ▸ Z33c. Lighting State Audit
//  Z33a 재베이크 뒤 낮 렌더가 어두워졌다(절벽 42→15, 꽃 59→32) → 베이크 설정·혼합 모드·해 상태·라이트맵 파일·git 기록 실측 (씬 변경 없음)
#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteLightAudit
{
    [MenuItem("Tools/Pyrite2/Z33c. Lighting State Audit", false, 72)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z33c] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var ls = Lightmapping.lightingSettings;
        sb.AppendLine(string.Format("lightingSettings {0} | mixed {1} | lightmapper {2} | bakedGI {3} realtimeGI {4} | res {5} max {6} | indirectScale {7} | ao {8}",
            ls.name, ls.mixedBakeMode, ls.lightmapper, ls.bakedGI, ls.realtimeGI, ls.lightmapResolution, ls.lightmapMaxSize, ls.indirectScale, ls.ao));
        sb.AppendLine("QualitySettings.shadowmaskMode " + QualitySettings.shadowmaskMode + " | ambientMode " + RenderSettings.ambientMode + " sky " + RenderSettings.ambientSkyColor + " int " + RenderSettings.ambientIntensity + " | skybox " + (RenderSettings.skybox ? RenderSettings.skybox.name : "-"));
        foreach (var l in Object.FindObjectsOfType<Light>().Where(l => l.type == LightType.Directional))
            sb.AppendLine(string.Format("dir light {0} bake {1} | rot {2} | int {3:0.00} | bakingOutput {4} mixed {5}", l.name, l.lightmapBakeType, l.transform.eulerAngles.ToString("F1"), l.intensity, l.bakingOutput.lightmapBakeType, l.bakingOutput.mixedLightingMode));
        int i = 0;
        foreach (var d in LightmapSettings.lightmaps)
        {
            string p = d.lightmapColor ? AssetDatabase.GetAssetPath(d.lightmapColor) : "-";
            sb.AppendLine(string.Format("  lm[{0}] {1} {2} | mtime {3} | shadowmask {4}", i++, p, d.lightmapColor ? d.lightmapColor.width + "px" : "", File.Exists(p) ? File.GetLastWriteTime(p).ToString("MM-dd HH:mm") : "-", d.shadowMask ? AssetDatabase.GetAssetPath(d.shadowMask) : "none"));
        }
        var lda = Lightmapping.lightingDataAsset;
        sb.AppendLine("lightingData " + (lda ? AssetDatabase.GetAssetPath(lda) : "-"));
        var dir = lda ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(lda)).Replace('\\', '/') : "Assets/Scenes/PyriteHome";
        sb.AppendLine(Git("log --format=%h|%ci|%s -n 8 -- \"" + dir + "\""));
        sb.AppendLine(Git("ls-files -- \"" + dir + "\""));
        Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_light.txt", sb.ToString());
    }

    static string Git(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Directory.GetCurrentDirectory() };
            using (var p = Process.Start(psi)) { string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd(); p.WaitForExit(15000); return "$ git " + args + "\n" + o; }
        }
        catch (System.Exception e) { return "git 실패 " + e.Message; }
    }
}
#endif
