// Tools ▸ Pyrite2 ▸ Z26a (Pyrite 메뉴가 화면을 넘어 스크롤이 안 된다 → 새 도구는 Pyrite2). Beam Audit (mirror + video)
//  설정 빔에 거울, 오른쪽에 영상 빔 — 만들기 전 실측 (씬 변경 없음)
//   1) 테이블 bounds (좌우 폭)  2) 캠프 거울 구조 (MirrorSurface 메시·크기·VRCMirrorReflection 값, 루트)
//   3) MediaPlayer(ProTV) 계층: 렌더러·머티리얼·셰이더·오디오·캔버스·Udon  4) 오른쪽 방향 지면 높이
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteBeamAudit
{
    [MenuItem("Tools/Pyrite2/Z26a. Beam Audit (mirror + video)", false, 1)]
    public static void Run()
    {
        var sb = new StringBuilder("[Z26a] " + System.DateTime.Now.ToString("HH:mm:ss") + "\n");
        var all = Resources.FindObjectsOfTypeAll<GameObject>().Where(g => g.scene.IsValid()).ToArray();
        GameObject F(string n) => all.FirstOrDefault(g => g.name == n);

        var table = F("camp03_table");
        if (table != null)
        {
            var b = table.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, c) => { a.Encapsulate(c); return a; });
            sb.AppendLine("table bounds min " + b.min.ToString("F3") + " max " + b.max.ToString("F3"));
        }
        var proj = F("SettingsProjector");
        if (proj != null)
        {
            sb.AppendLine("SettingsProjector pos " + proj.transform.position.ToString("F3") + " yaw " + proj.transform.eulerAngles.y.ToString("F1"));
            var panel = proj.transform.Find("Panel");
            if (panel) sb.AppendLine("  Panel pos " + panel.position.ToString("F3") + " scale " + panel.lossyScale.ToString("F3"));
        }

        // 거울
        foreach (var mr in Object.FindObjectsOfType<VRC.SDK3.Components.VRCMirrorReflection>(true))
        {
            var t = mr.transform;
            var mf = t.GetComponent<MeshFilter>();
            sb.AppendLine(string.Format("mirror {0} active {1} | root {2} | pos {3} rot {4} scale {5} | mesh {6} bounds {7} | layer {8}",
                Path(t), mr.gameObject.activeInHierarchy, t.root.name, t.position.ToString("F2"), t.eulerAngles.ToString("F0"), t.lossyScale.ToString("F2"),
                mf && mf.sharedMesh ? mf.sharedMesh.name : "-", mf && mf.sharedMesh ? mf.sharedMesh.bounds.size.ToString("F2") : "-", t.gameObject.layer));
            var so = new SerializedObject(mr);
            foreach (var p in new[] { "m_ReflectLayers", "mirrorResolution", "maximumAntialiasing", "m_DisablePixelLights", "TurnOffMirrorOcclusion", "customShader", "customSkybox", "cameraClearFlags" })
            {
                var sp = so.FindProperty(p);
                if (sp == null) continue;
                string v = sp.propertyType == SerializedPropertyType.LayerMask ? sp.intValue.ToString() :
                    sp.propertyType == SerializedPropertyType.ObjectReference ? (sp.objectReferenceValue ? sp.objectReferenceValue.name : "null") :
                    sp.propertyType == SerializedPropertyType.Boolean ? sp.boolValue.ToString() :
                    sp.propertyType == SerializedPropertyType.Enum ? sp.enumNames[Mathf.Clamp(sp.enumValueIndex, 0, sp.enumNames.Length - 1)] : sp.intValue.ToString();
                sb.AppendLine("    " + p + " = " + v);
            }
            var rootT = t.root;
            sb.AppendLine("    root children: " + string.Join(", ", rootT.GetComponentsInChildren<Transform>(true).Where(x => x.parent == rootT).Select(x => x.name)));
        }

        // ProTV
        var mp = F("MediaPlayer");
        if (mp != null)
        {
            sb.AppendLine("MediaPlayer pos " + mp.transform.position.ToString("F2") + " rot " + mp.transform.eulerAngles.ToString("F0") + " active " + mp.activeInHierarchy);
            foreach (var t in mp.GetComponentsInChildren<Transform>(true))
            {
                var comps = t.GetComponents<Component>().Where(c => c != null && !(c is Transform)).Select(c => c.GetType().Name).ToArray();
                var r = t.GetComponent<Renderer>();
                string rinfo = r ? string.Format(" | mats [{0}] shaders [{1}] bounds {2}", string.Join(",", r.sharedMaterials.Select(m => m ? m.name : "null")),
                    string.Join(",", r.sharedMaterials.Select(m => m && m.shader ? m.shader.name : "-")), r.bounds.size.ToString("F2")) : "";
                var a = t.GetComponent<AudioSource>();
                string ainfo = a ? string.Format(" | audio vol {0:0.00} spatial {1:0.00} min {2:0.0} max {3:0.0}", a.volume, a.spatialBlend, a.minDistance, a.maxDistance) : "";
                sb.AppendLine(string.Format("  {0} [{1}] active {2} pos {3}{4}{5}", Path(t).Replace(Path(mp.transform), "~"), string.Join(",", comps), t.gameObject.activeSelf, t.position.ToString("F2"), rinfo, ainfo));
            }
        }
        else sb.AppendLine("MediaPlayer 없음");

        // 오른쪽 후보 지면
        var terr = Terrain.activeTerrain;
        if (proj != null && terr != null)
        {
            var o = proj.transform.position;
            foreach (var yaw in new[] { 171f, 191f, 201f, 211f, 221f })
                foreach (var d in new[] { 3.5f, 4.5f, 5.5f })
                {
                    var q = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                    var p = o + q * d;
                    sb.AppendLine(string.Format("  yaw {0} d {1}: ground {2:0.00} at {3}", yaw, d, terr.SampleHeight(p) + terr.transform.position.y, p.ToString("F1")));
                }
        }
        Directory.CreateDirectory("Logs"); File.WriteAllText("Logs/pyrite_beam_audit.txt", sb.ToString());
    }

    static string Path(Transform t) { var s = t.name; while (t.parent != null) { t = t.parent; s = t.name + "/" + s; } return s; }
}
#endif
