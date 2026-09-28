// PyriteClipTool.cs — Claude 가 computer use 로 프로젝트 파일을 읽고 쓰는 도구
// 클립보드에 명령을 넣고 메뉴 실행 → 결과가 클립보드로 돌아온다.
//   cat|<프로젝트 상대 경로>[|시작줄|줄수]
//   ls|<폴더>|<패턴>
//   grep|<정규식>|<폴더>|<패턴>   (정규식에 | 못 씀)
//   b64|<파일>
//   put|<경로>\n<내용>  (Assets/Editor·Udon·Shaders, Logs 아래 파일 쓰기 → 그 뒤 Ctrl+R)
//   rep|<경로>\n@@OLD\n<옛 글>\n@@NEW\n<새 글>\n@@END\n ... (여러 개. 각 옛 글은 파일에 정확히 1번 있어야 함, 하나라도 틀리면 아무것도 안 씀)
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
        string cmd = (EditorGUIUtility.systemCopyBuffer ?? "").TrimStart();
        string outText;
        try { outText = Exec(cmd); }
        catch (System.Exception e) { outText = "ERR " + e.GetType().Name + ": " + e.Message; }
        EditorGUIUtility.systemCopyBuffer = outText;
    }

    static string Exec(string cmd)
    {
        if (cmd.StartsWith("put|"))   // put|<경로>\n<내용>  — 파일 쓰기 (Assets/Editor, Assets/Udon, Assets/Shaders, Logs 아래만)
        {
            int nl = cmd.IndexOf('\n');
            string path = cmd.Substring(4, nl - 4).Trim();
            if (!(path.StartsWith("Assets/Editor/") || path.StartsWith("Assets/Udon/") || path.StartsWith("Assets/Shaders/") || path.StartsWith("Logs/")))
                return "ERR put 금지 경로 " + path;
            string body = cmd.Substring(nl + 1).Replace("\r\n", "\n");
            File.WriteAllText(path, body, new UTF8Encoding(false));
            return "OK put " + path + " " + body.Length + " chars md5 " + Md5(body);
        }
        if (cmd.StartsWith("rep|"))
        {
            int nl = cmd.IndexOf('\n');
            string path = cmd.Substring(4, nl - 4).Trim();
            if (!(path.StartsWith("Assets/Editor/") || path.StartsWith("Assets/Udon/") || path.StartsWith("Assets/Shaders/") || path.StartsWith("Logs/")))
                return "ERR rep 금지 경로 " + path;
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            string body = cmd.Substring(nl + 1).Replace("\r\n", "\n");
            int k = 0, pos = 0;
            while (true)
            {
                int o = body.IndexOf("@@OLD\n", pos); if (o < 0) break;
                int w = body.IndexOf("\n@@NEW\n", o); int e = w < 0 ? -1 : body.IndexOf("\n@@END", w + 7);
                if (w < 0 || e < 0) return "ERR rep 형식 (블록 " + (k + 1) + ")";
                string oldS = body.Substring(o + 6, w - o - 6), newS = body.Substring(w + 7, e - w - 7);
                int first = text.IndexOf(oldS, System.StringComparison.Ordinal);
                if (first < 0) return "ERR rep 블록 " + (k + 1) + " 못 찾음: " + (oldS.Length > 60 ? oldS.Substring(0, 60) : oldS);
                if (text.IndexOf(oldS, first + 1, System.StringComparison.Ordinal) >= 0) return "ERR rep 블록 " + (k + 1) + " 여러 곳";
                text = text.Substring(0, first) + newS + text.Substring(first + oldS.Length);
                k++; pos = e + 6;
            }
            if (k == 0) return "ERR rep 블록 없음";
            File.WriteAllText(path, text, new UTF8Encoding(false));
            return "OK rep " + path + " " + k + " blocks, " + text.Length + " chars md5 " + Md5(text);
        }
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
        return "ERR unknown command: " + (cmd.Length > 80 ? cmd.Substring(0, 80) : cmd);
    }

    static string Md5(string s)
    {
        using (var md = System.Security.Cryptography.MD5.Create())
            return System.BitConverter.ToString(md.ComputeHash(new UTF8Encoding(false).GetBytes(s))).Replace("-", "").ToLower();
    }
}
