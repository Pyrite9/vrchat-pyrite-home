// Tools ▸ Pyrite ▸ Z6. Realtime Prop Shadows / Z6b. Revert
//  캠프 인공물은 구운 그림자(지형 라이트맵 5텍셀/m)라 흐렸다. 회전 의자처럼 실시간 그림자로 바꾼다.
//   · ContributeGI 정적 플래그만 뺀다 → 라이트맵에서 빠지고 실시간 그림자를 드리운다. 밝기는 라이트 프로브(캠프 3m 격자)로
//   · 배칭·오클루전 플래그는 그대로 (움직이지 않으니까)
//   · 대상 루트: Camp, Cot, Mirror, MediaPlayer, Dock (파티클·UI·투명 카드 제외)
//  재베이크 필요 (지형에서 이 소품들의 구운 그림자가 빠져야 이중 그림자가 안 생긴다)
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PyriteRealtimeProps
{
    static readonly string[] ROOTS = { "Camp", "Cot", "Mirror", "MediaPlayer", "Dock" };

    [MenuItem("Tools/Pyrite/Z6. Realtime Prop Shadows", false, 10)]
    public static void Run() { Apply(true); }

    [MenuItem("Tools/Pyrite/Z6b. Realtime Prop Shadows Revert", false, 11)]
    public static void Revert() { Apply(false); }

    static void Apply(bool realtime)
    {
        var sb = new StringBuilder(realtime ? "[Z6] " : "[Z6b] ");
        var roots = SceneManager.GetActiveScene().GetRootGameObjects().Where(g => ROOTS.Contains(g.name)).ToList();
        int n = 0;
        foreach (var root in roots)
        {
            int k = 0;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var go = r.gameObject;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                bool wasGI = (flags & StaticEditorFlags.ContributeGI) != 0;
                if (realtime)
                {
                    if (!wasGI) continue;                                   // 원래 동적이던 것(표시등·카드·랜턴 등)은 손대지 않음
                    if (r.shadowCastingMode == ShadowCastingMode.Off) continue;
                    Undo.RecordObject(go, "rt shadow"); Undo.RecordObject(r, "rt shadow");
                    GameObjectUtility.SetStaticEditorFlags(go, flags & ~StaticEditorFlags.ContributeGI);
                    r.lightProbeUsage = LightProbeUsage.BlendProbes;
                    r.shadowCastingMode = ShadowCastingMode.On;
                    r.receiveShadows = true;
                    // 되돌리기용 표시 — 이름이 아니라 태그 대신 전역 목록 파일에 기록
                    k++;
                }
                else
                {
                    if (wasGI) continue;
                    if (!IsMarked(go)) continue;
                    Undo.RecordObject(go, "rt shadow revert");
                    GameObjectUtility.SetStaticEditorFlags(go, flags | StaticEditorFlags.ContributeGI);
                    k++;
                }
                if (realtime) Mark(go);
            }
            sb.Append(root.name).Append(' ').Append(k).Append(" | ");
            n += k;
        }
        if (realtime) SaveMarks(); else { marks.Clear(); SaveMarks(); }
        sb.Append("total ").Append(n);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log(sb.ToString());
        Directory.CreateDirectory("Assets/_preview/");
        File.WriteAllText("Assets/_preview/realtimeprops.txt", sb.ToString());
    }

    // 되돌리기 대상 기록 — 씬 경로 목록 (Logs 는 git 제외라 Assets/Editor 옆 텍스트로)
    const string MARKS = "Assets/Editor/PyriteRealtimeProps.list.txt";
    static System.Collections.Generic.HashSet<string> marks;
    static string PathOf(GameObject g) { var s = g.name; for (var t = g.transform.parent; t != null; t = t.parent) s = t.name + "/" + s; return s; }
    static void Load() { if (marks == null) marks = new System.Collections.Generic.HashSet<string>(File.Exists(MARKS) ? File.ReadAllLines(MARKS) : new string[0]); }
    static void Mark(GameObject g) { Load(); marks.Add(PathOf(g)); }
    static bool IsMarked(GameObject g) { Load(); return marks.Contains(PathOf(g)); }
    static void SaveMarks() { Load(); File.WriteAllLines(MARKS, marks.OrderBy(x => x)); AssetDatabase.ImportAsset(MARKS); }
}
#endif
