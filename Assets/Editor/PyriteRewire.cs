// Tools ▸ Pyrite ▸ Y5. Rewire Flowers + Fireflies
//  I(꽃밭 메시 재생성)·E(LightFX 재생성) 뒤에 시간대 스크립트의 참조를 다시 잇는다.
//  J 를 통째로 다시 돌리면 다이얼·패널·결정 조명까지 새로 만들기 때문에, 필요한 배열만 갱신한다.
//   - flowerRenderers ← FlowerField 의 MeshRenderer 전부
//   - fireflies       ← LightFX 의 FF_* 파티클 전부 (꽃밭 반딧불 + 결정 반딧불)
//  순서: I → E → Y4 → Y5
#if UNITY_EDITOR
using System.Linq;
using UdonSharpEditor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PyriteRewire
{
    [MenuItem("Tools/Pyrite/Y5. Rewire Flowers + Fireflies", false, 294)]
    public static void Run()
    {
        var roots = SceneManager.GetActiveScene().GetRootGameObjects();
        var flora = roots.FirstOrDefault(g => g.name == "FlowerField");
        var fx = roots.FirstOrDefault(g => g.name == "LightFX");
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (flora == null || fx == null || tod == null) { Debug.LogError("[Y5] FlowerField/LightFX/ToD 없음"); return; }

        tod.flowerRenderers = flora.GetComponentsInChildren<MeshRenderer>(true).Cast<Renderer>().ToArray();
        tod.fireflies = fx.GetComponentsInChildren<ParticleSystem>(true).Where(p => p.name.StartsWith("FF_")).ToArray();

        var f = typeof(PyriteTimeOfDay).GetField("ready", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (f != null) f.SetValue(tod, false);
        tod.index = 0; tod.Apply();
        UdonSharpEditorUtility.CopyProxyToUdon(tod);
        EditorUtility.SetDirty(tod);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Y5] flowerRenderers " + tod.flowerRenderers.Length + " / fireflies " + tod.fireflies.Length
                  + " (결정 " + tod.fireflies.Count(p => p.name.StartsWith("FF_Crystal_")) + ")");
    }
}
#endif
