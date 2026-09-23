// Tools ▸ Pyrite ▸ Z1. Sky Horizon Probe
//  태양·달이 절벽에 가리는 문제 측정용 (읽기 전용 — 씬을 바꾸지 않는다)
//   1) 캠프·물가·스폰에서 방위 5°마다 "벽 꼭대기 앙각" (지형 + 모든 메시 정점)
//   2) 시간대별 방향광 방향, 스카이박스 머티리얼의 Sun/Moon 관련 속성
//  결과 → Logs/pyrite_sky_probe.txt
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PyriteSkyProbe
{
    const int BINS = 72;   // 5°

    [MenuItem("Tools/Pyrite/Z1. Sky Horizon Probe", false, 5)]
    public static void Run()
    {
        var sb = new StringBuilder();
        var t = Terrain.activeTerrain;
        float G(float x, float z) => t != null ? t.SampleHeight(new Vector3(x, 0f, z)) + t.transform.position.y : 0f;

        // 정점 모음 (활성 메시 렌더러, 물·파티클 제외)
        var pts = new List<Vector3>();
        foreach (var mf in Object.FindObjectsOfType<MeshFilter>())
        {
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || !r.enabled || !mf.gameObject.activeInHierarchy || mf.sharedMesh == null) continue;
            if (mf.transform.root.name == "Water") continue;
            if (!mf.sharedMesh.isReadable) { var b = r.bounds; for (int k = 0; k < 8; k++) pts.Add(new Vector3((k & 1) == 0 ? b.min.x : b.max.x, (k & 2) == 0 ? b.min.y : b.max.y, (k & 4) == 0 ? b.min.z : b.max.z)); continue; }
            var m = mf.transform.localToWorldMatrix;
            foreach (var v in mf.sharedMesh.vertices) pts.Add(m.MultiplyPoint3x4(v));
        }
        sb.AppendLine("mesh points " + pts.Count);

        var eyes = new List<KeyValuePair<string, Vector3>>
        {
            new KeyValuePair<string, Vector3>("camp",  new Vector3(-10f, 0f, 47f)),
            new KeyValuePair<string, Vector3>("shore", new Vector3( 12f, 0f, 36f)),
            new KeyValuePair<string, Vector3>("center",new Vector3(  0f, 0f,-14f)),
        };
        var spawn = Object.FindObjectsOfType<Transform>().FirstOrDefault(x => x.name.ToLower().Contains("spawn"));
        if (spawn != null) eyes.Add(new KeyValuePair<string, Vector3>("spawn(" + spawn.name + ")", spawn.position));

        foreach (var e in eyes)
        {
            var eye = e.Value; eye.y = Mathf.Max(0f, G(eye.x, eye.z)) + 1.7f;
            var hor = new float[BINS]; var dist = new float[BINS];
            // 지형: 방위마다 0.5m 간격 행군
            for (int a = 0; a < BINS; a++)
            {
                float az = a * 5f * Mathf.Deg2Rad;             // 0 = +z(북), 90 = +x(동)
                var d = new Vector2(Mathf.Sin(az), Mathf.Cos(az));
                for (float s = 2f; s < 200f; s += 0.5f)
                {
                    float h = G(eye.x + d.x * s, eye.z + d.y * s) - eye.y;
                    float el = Mathf.Atan2(h, s) * Mathf.Rad2Deg;
                    if (el > hor[a]) { hor[a] = el; dist[a] = s; }
                }
            }
            // 메시 정점
            foreach (var p in pts)
            {
                var dx = p.x - eye.x; var dz = p.z - eye.z;
                float s = Mathf.Sqrt(dx * dx + dz * dz); if (s < 3f) continue;
                float az = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg; if (az < 0) az += 360f;
                int a = Mathf.Clamp(Mathf.RoundToInt(az / 5f) % BINS, 0, BINS - 1);
                float el = Mathf.Atan2(p.y - eye.y, s) * Mathf.Rad2Deg;
                if (el > hor[a]) { hor[a] = el; dist[a] = s; }
            }
            sb.AppendLine("== " + e.Key + " eye " + eye.ToString("F1"));
            for (int a = 0; a < BINS; a++)
                sb.Append(a * 5).Append(':').Append(hor[a].ToString("F1")).Append('@').Append(dist[a].ToString("F0")).Append(a % 6 == 5 ? "\n" : "  ");
        }

        // 시간대별 태양·스카이
        var tod = Object.FindObjectOfType<PyriteTimeOfDay>();
        if (tod != null)
        {
            sb.AppendLine("== ToD fields");
            foreach (var f in typeof(PyriteTimeOfDay).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                var n = f.Name.ToLower();
                if (!(n.Contains("sun") || n.Contains("moon") || n.Contains("sky") || n.Contains("rot"))) continue;
                var v = f.GetValue(tod);
                string s = v is System.Array arr ? string.Join(" | ", arr.Cast<object>().Select(o => o == null ? "null" : (o is Object uo ? uo.name : o.ToString()))) : (v == null ? "null" : (v is Object uo2 ? uo2.name : v.ToString()));
                sb.AppendLine(f.Name + " = " + s);
            }
            if (tod.sun != null)
            {
                var fw = tod.sun.transform.forward;   // 빛이 가는 방향 → 태양은 -fw
                var sd = -fw;
                float el = Mathf.Asin(sd.y) * Mathf.Rad2Deg, az = Mathf.Atan2(sd.x, sd.z) * Mathf.Rad2Deg; if (az < 0) az += 360;
                sb.AppendLine("sun light euler " + tod.sun.transform.eulerAngles.ToString("F2") + " → sun dir el " + el.ToString("F1") + " az " + az.ToString("F1"));
            }
            if (tod.skybox != null)
                foreach (var m in tod.skybox.Where(x => x != null).Distinct())
                {
                    sb.AppendLine("== skybox " + m.name + " shader " + m.shader.name + " path " + AssetDatabase.GetAssetPath(m));
                    var sh = m.shader;
                    for (int i = 0; i < sh.GetPropertyCount(); i++)
                    {
                        var pn = sh.GetPropertyName(i); var ty = sh.GetPropertyType(i);
                        string val = ty == UnityEngine.Rendering.ShaderPropertyType.Texture ? (m.GetTexture(pn) ? m.GetTexture(pn).name : "-")
                                   : ty == UnityEngine.Rendering.ShaderPropertyType.Color ? m.GetColor(pn).ToString("F3")
                                   : ty == UnityEngine.Rendering.ShaderPropertyType.Vector ? m.GetVector(pn).ToString("F3")
                                   : m.GetFloat(pn).ToString("F3");
                        sb.Append(pn).Append('=').Append(val).Append("  ");
                    }
                    sb.AppendLine();
                }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/pyrite_sky_probe.txt", sb.ToString());
        Debug.Log("[Z1] Logs/pyrite_sky_probe.txt 저장");
    }
}
#endif
