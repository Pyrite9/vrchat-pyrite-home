// PyritePreviewWindow.cs — 확인 렌더 폴더를 한 화면에 격자로 띄운다 (Claude 가 스크린샷으로 확인)
// Tools ▸ Pyrite3 ▸ Z49v. Preview Viewer — 폴더는 클립보드가 "view|<폴더>[|파일 필터]" 이면 그것, 아니면 마지막 폴더
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class PyritePreviewWindow : EditorWindow
{
    string dir = "Assets/_preview/bedroom";
    string filter = "";
    Texture2D[] texs = new Texture2D[0];
    string[] names = new string[0];

    [MenuItem("Tools/Pyrite3/Z49v. Preview Viewer", false, 4998)]
    public static void Open()
    {
        var w = GetWindow<PyritePreviewWindow>(true, "Pyrite Preview");
        string cb = (EditorGUIUtility.systemCopyBuffer ?? "").Trim();
        if (cb.StartsWith("view|")) { var a = cb.Split('|'); w.dir = a[1]; w.filter = a.Length > 2 ? a[2] : ""; }
        else w.dir = EditorPrefs.GetString("PyritePreviewDir", w.dir);
        EditorPrefs.SetString("PyritePreviewDir", w.dir);
        w.Load();
        w.position = new Rect(40, 60, 1440, 860);
        w.Show();
    }

    void Load()
    {
        foreach (var t in texs) if (t) DestroyImmediate(t);
        var files = Directory.Exists(dir) ? Directory.GetFiles(dir).Where(f => (f.EndsWith(".jpg") || f.EndsWith(".png")) && (filter == "" || Path.GetFileName(f).Contains(filter))).OrderBy(f => f).ToArray() : new string[0];
        texs = files.Select(f => { var t = new Texture2D(2, 2); t.LoadImage(File.ReadAllBytes(f)); return t; }).ToArray();
        names = files.Select(Path.GetFileNameWithoutExtension).ToArray();
    }

    void OnGUI()
    {
        if (GUILayout.Button("Reload  (" + dir + (filter != "" ? " / " + filter : "") + ")  " + texs.Length + " files")) Load();
        if (texs.Length == 0) return;
        int n = texs.Length;
        int cols = n <= 1 ? 1 : n <= 4 ? 2 : 3;
        int rows = Mathf.CeilToInt(n / (float)cols);
        float top = 24, W = position.width, H = position.height - top;
        float cw = W / cols, ch = H / rows;
        var style = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.yellow } };
        for (int i = 0; i < n; i++)
        {
            int c = i % cols, r = i / cols;
            var cell = new Rect(c * cw, top + r * ch, cw - 4, ch - 4);
            var t = texs[i];
            float s = Mathf.Min(cell.width / t.width, (cell.height - 16) / t.height);
            var img = new Rect(cell.x, cell.y + 16, t.width * s, t.height * s);
            GUI.DrawTexture(img, t, ScaleMode.ScaleToFit);
            GUI.Label(new Rect(cell.x + 2, cell.y, cell.width, 16), names[i], style);
        }
    }

    void OnDestroy() { foreach (var t in texs) if (t) DestroyImmediate(t); }
}
