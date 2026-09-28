// PyriteClipTool.cs — Claude 가 computer use 로 프로젝트 파일을 읽기 위한 도구 (읽기 전용)
// 클립보드에 명령을 넣고 메뉴 실행 → 결과가 클립보드로 돌아온다.
//   cat|<프로젝트 상대 경로>[|시작줄|줄수]
//   ls|<폴더>|<패턴>
//   grep|<정규식>|<폴더>|<패턴>
//   b64|<파일>  (렌더 jpg 를 base64 로)
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;

public static class PyriteClipTool
{
    [MenuItem("Tools/Pyrite3/Z49z. Clipboard Read (Claude)", false, 4999)]
    public static void Run()
    {
        string cmd = (EditorGUIUtility.systemCopyBuffer ?? "").Trim();
        string outText;
        try { outText = Exec(cmd); }
        catch (System.Exception e) { outText = "ERR " + e.GetType().Name + ": " + e.Message; }
        EditorGUIUtility.systemCopyBuffer = outText;
    }

    static string Exec(string cmd)
    {
        var a = cmd.Split('|');
        switch (a[0])
        {
            case "cat":
            {
                var lines = File.ReadAllLines(a[1]);
                int start = a.Length > 2 ? int.Parse(a[2]) : 1;
                int count = a.Length > 3 ? int.Parse(a[3]) : 400;
                var sb = new StringBuilder("# " + a[1] + " lines " + lines.Length + "\n");
                for (int i = start - 1; i < lines.Length && i < start - 1 + count; i++)
                    sb.Append(i + 1).Append(": ").AppendLine(lines[i]);
                return sb.ToString();
            }
            case "b64":
                return System.Convert.ToBase64String(File.ReadAllBytes(a[1]));
            case "ls":
                return string.Join("\n", Directory.GetFiles(a[1], a.Length > 2 ? a[2] : "*", SearchOption.AllDirectories)
                    .Select(f => f.Replace('\\', '/') + "  " + new FileInfo(f).Length));
            case "grep":
            {
                var rx = new Regex(a[1]);
                var sb = new StringBuilder();
                int n = 0;
                foreach (var f in Directory.GetFiles(a[2], a.Length > 3 ? a[3] : "*.cs", SearchOption.AllDirectories))
                {
                    var lines = File.ReadAllLines(f);
                    for (int i = 0; i < lines.Length; i++)
                        if (rx.IsMatch(lines[i]))
                        {
                            sb.Append(f.Replace('\\', '/')).Append(':').Append(i + 1).Append(": ").AppendLine(lines[i].Trim());
                            if (++n >= 300) return sb.ToString() + "... (300 limit)";
                        }
                }
                return sb.Length == 0 ? "(no match)" : sb.ToString();
            }
        }
        return "ERR unknown command: " + cmd;
    }
}
