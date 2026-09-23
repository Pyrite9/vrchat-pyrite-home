// Tools ▸ Pyrite2 ▸ Z30a. Import WoodBoat Package
//  관리자가 산 BOOTH 에셋 (ootwn 木製ボート ¥100, 수정 허용 / 재배포 금지) 을 E:\VRC\WoodBoat 에서 임포트한다 (대화창 없이)
//  임포트된 경로는 Logs/pyrite_boat.txt 에 적는다 → .gitignore 와 X2 유료 경로 검사에 넣을 폴더를 정한다
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteBoatImport
{
    const string PKG = "E:/VRC/WoodBoat/WoodBoat.unitypackage";

    [MenuItem("Tools/Pyrite2/Z30a. Import WoodBoat Package", false, 40)]
    public static void Run()
    {
        if (!File.Exists(PKG)) { Log("패키지 없음: " + PKG); return; }
        AssetDatabase.importPackageCompleted -= Done;
        AssetDatabase.importPackageFailed -= Failed;
        AssetDatabase.importPackageCompleted += Done;
        AssetDatabase.importPackageFailed += Failed;
        Log("[Z30a] " + System.DateTime.Now.ToString("HH:mm:ss") + " import start " + PKG);
        AssetDatabase.ImportPackage(PKG, false);
    }

    static void Done(string name)
    {
        AssetDatabase.importPackageCompleted -= Done;
        var sb = new StringBuilder("  completed " + name + "\n");
        var dirs = Directory.GetDirectories("Assets").Select(d => d.Replace('\\', '/')).ToArray();
        sb.AppendLine("  Assets dirs: " + string.Join(", ", dirs));
        foreach (var p in AssetDatabase.GetAllAssetPaths().Where(p => p.ToLower().Contains("boat")).OrderBy(p => p))
            sb.AppendLine("   " + p);
        Log(sb.ToString());
    }

    static void Failed(string name, string err) { AssetDatabase.importPackageFailed -= Failed; Log("  FAILED " + name + ": " + err); }

    static void Log(string s) { Directory.CreateDirectory("Logs"); File.AppendAllText("Logs/pyrite_boat.txt", s + "\n"); }
}
#endif
