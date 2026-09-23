// Tools ▸ Pyrite ▸ X1/X2/X3 — git runner for PyriteHome (no credentials handled here)
#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class PyriteGit
{
    static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static string LogPath => Path.Combine(Root, "Logs", "pyrite_git.txt");
    static string MsgPath => Path.Combine(Root, "Logs", "pyrite_git_msg.txt");
    static string RemotePath => Path.Combine(Root, "Logs", "pyrite_git_remote.txt");
    const long MAX_BYTES = 50L * 1024 * 1024;

    static StringBuilder log;

    static int Git(string args, out string stdout, int timeoutMs = 120000, bool allowPrompt = false)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            WorkingDirectory = Root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!allowPrompt) psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
        var so = new StringBuilder(); var se = new StringBuilder();
        int code;
        try
        {
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) se.AppendLine(e.Data); };
                p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } code = -2; se.AppendLine("TIMEOUT"); }
                else { p.WaitForExit(); code = p.ExitCode; }
            }
        }
        catch (Exception ex) { code = -1; se.AppendLine("START FAILED: " + ex.Message); }
        stdout = so.ToString();
        log.AppendLine("$ git " + args + "   -> " + code);
        if (so.Length > 0) log.AppendLine(Trunc(so.ToString(), 6000));
        if (se.Length > 0) log.AppendLine("[stderr] " + Trunc(se.ToString(), 3000));
        return code;
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "\n...(truncated " + (s.Length - n) + " chars)";

    static void Begin(string title) { log = new StringBuilder(); log.AppendLine("=== " + title + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ==="); log.AppendLine("root: " + Root); }
    static void End()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
        File.WriteAllText(LogPath, log.ToString(), new UTF8Encoding(false));
        Debug.Log("[PyriteGit] log -> " + LogPath + "\n" + Trunc(log.ToString(), 4000));
    }

    static bool IsRepo() => Directory.Exists(Path.Combine(Root, ".git"));

    [MenuItem("Tools/Pyrite/X1. Git Check", false, 1)]
    public static void Check()
    {
        Begin("X1 Git Check");
        string o;
        if (Git("--version", out o) != 0) { log.AppendLine("RESULT: git NOT FOUND"); End(); return; }
        Git("config --global user.name", out o);
        Git("config --global user.email", out o);
        Git("config --global --get-all safe.directory", out o);
        log.AppendLine("repo exists: " + IsRepo());
        if (IsRepo())
        {
            Git("status --short --branch", out o);
            Git("log --oneline -5", out o);
            Git("remote -v", out o);
        }
        log.AppendLine("RESULT: OK");
        End();
    }

    static readonly Regex[] Forbidden =
    {
        new Regex(@"^Assets/つきのすとあ/"),
        new Regex(@"^Assets/Noagami/"),
        new Regex(@"^Assets/Sorafield Procedural Skies - VRChat Addon/"),
        new Regex(@"^Assets/Flora/Generated/"),
        new Regex(@"_dusk\.png(\.meta)?$"),
        new Regex(@"^Assets/_preview/"),
    };

    static bool Allowed(string f)
    {
        if (Forbidden.Any(r => r.IsMatch(f))) return false;
        if (f.StartsWith("Assets/Sakana-Water") || f.StartsWith("Assets/Sorafield")) return false;
        if (f.StartsWith("Assets/つきのすとあ") || f.StartsWith("Assets/Noagami")) return false;
        return true;
    }

    [MenuItem("Tools/Pyrite/X2. Git Commit", false, 2)]
    public static void Commit() { DoCommit(false); }

    // X4: re-apply .gitignore to the index and rewrite the (unpushed) last commit
    [MenuItem("Tools/Pyrite/X4. Reindex + Amend (before first push)", false, 4)]
    public static void Amend() { DoCommit(true); }

    static void DoCommit(bool amend)
    {
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Begin(amend ? "X4 Reindex + Amend" : "X2 Git Commit");
        string o;
        if (!IsRepo())
        {
            if (amend) { log.AppendLine("RESULT: no repo"); End(); return; }
            if (Git("init -b main", out o) != 0) { log.AppendLine("RESULT: init failed"); End(); return; }
        }
        if (amend)
        {
            if (Git("rev-parse --verify -q refs/remotes/origin/main", out o) == 0) { log.AppendLine("RESULT: already pushed, refusing to amend"); End(); return; }
            Git("rm -r -q --cached .", out o, 600000);
        }
        Git("config core.quotepath false", out o);
        Git("config core.autocrlf false", out o);
        Git("add -A", out o, 600000);
        Git("ls-files -z", out o, 300000);
        var files = o.Split('\0').Select(s => s.Trim('\r', '\n')).Where(s => s.Length > 0).ToList();
        log.AppendLine("index files: " + files.Count);

        var bad = files.Where(f => !Allowed(f)).ToList();
        var big = files.Where(f => { var p = Path.Combine(Root, f); return File.Exists(p) && new FileInfo(p).Length > MAX_BYTES; }).ToList();
        long total = files.Sum(f => { var p = Path.Combine(Root, f); return File.Exists(p) ? new FileInfo(p).Length : 0; });
        log.AppendLine("staged bytes: " + (total / 1048576.0).ToString("F1") + " MB");
        if (bad.Count > 0 || big.Count > 0)
        {
            foreach (var b in bad) log.AppendLine("FORBIDDEN: " + b);
            foreach (var b in big) log.AppendLine("TOO BIG: " + b);
            Git("reset -q", out o);
            log.AppendLine("RESULT: ABORTED (nothing committed, index reset)");
            End(); return;
        }
        if (files.Count == 0 && !amend) { log.AppendLine("RESULT: nothing to commit"); End(); return; }

        // top-level summary for review
        foreach (var g in files.GroupBy(f => { var parts = f.Split('/'); return parts.Length > 2 ? parts[0] + "/" + parts[1] : parts[0]; }).OrderBy(g => g.Key))
            log.AppendLine("  " + g.Count().ToString().PadLeft(5) + "  " + g.Key);

        if (!File.Exists(MsgPath))
            File.WriteAllText(MsgPath, "PyriteHome snapshot " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\n", new UTF8Encoding(false));
        if (Git("commit -q " + (amend ? "--amend " : "") + "-F \"" + MsgPath + "\"", out o) != 0) { log.AppendLine("RESULT: commit failed"); End(); return; }
        File.Delete(MsgPath);
        Git("log --oneline -3", out o);
        Git("status --short", out o);
        // audit: every path in the whole reachable history must pass Allowed()
        Git("log --all --name-only --format=", out o, 300000);
        var hist = o.Split('\n').Select(s => s.Trim('\r')).Where(s => s.Length > 0).Distinct().ToList();
        var histBad = hist.Where(f => !Allowed(f)).ToList();
        log.AppendLine("history paths: " + hist.Count + ", forbidden in history: " + histBad.Count);
        foreach (var b in histBad) log.AppendLine("HISTORY FORBIDDEN: " + b);
        Git("ls-files -- \"Assets/Sakana-Water*\" \"Assets/Sorafield*\" \"Assets/Noagami*\" \"Assets/Flora/Generated*\"", out o);
        log.AppendLine("paid-path ls-files lines: " + o.Split('\n').Count(l => l.Trim().Length > 0));
        log.AppendLine("RESULT: COMMITTED");
        End();
    }

    [MenuItem("Tools/Pyrite/X3. Git Push", false, 3)]
    public static void Push()
    {
        Begin("X3 Git Push");
        string o;
        if (!IsRepo()) { log.AppendLine("RESULT: no repo"); End(); return; }
        if (!File.Exists(RemotePath)) { log.AppendLine("RESULT: no Logs/pyrite_git_remote.txt"); End(); return; }
        string url = File.ReadAllText(RemotePath).Trim();
        if (!Regex.IsMatch(url, @"^https://github\.com/[\w.-]+/[\w.-]+(\.git)?$")) { log.AppendLine("RESULT: bad url " + url); End(); return; }
        Git("log main --name-only --format=", out o, 300000);
        var histBad = o.Split('\n').Select(s => s.Trim('\r')).Where(s => s.Length > 0 && !Allowed(s)).Distinct().ToList();
        if (histBad.Count > 0) { foreach (var b in histBad) log.AppendLine("HISTORY FORBIDDEN: " + b); log.AppendLine("RESULT: push refused"); End(); return; }
        if (Git("remote get-url origin", out o) == 0) Git("remote set-url origin " + url, out o);
        else Git("remote add origin " + url, out o);
        // no GIT_TERMINAL_PROMPT: Git Credential Manager opens its own browser/login window for the user
        int code = Git("push -u origin main", out o, 900000, true);
        log.AppendLine(code == 0 ? "RESULT: PUSHED" : "RESULT: push failed (" + code + ")");
        End();
    }
}
#endif
