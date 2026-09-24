// 물수제비 돌 — 부두 끝 쟁반에서 집어 던진다 (데스크톱: 우클릭 길게 = 던지기)
//  수면(WaterWalk 콜라이더)에 닿을 때: 수평 속도 minSpeed 이상 + 입사각 maxAngle 이하면 튀고(속도 82%), 아니면 가라앉는다(콜라이더 끔 → 2.5초 뒤 쟁반으로)
//  물보라·물결 고리·호수 파문. 던진 사람(주인)은 충돌로, 다른 사람은 수면 통과로 물보라를 낸다
//  멀리 놓인 채 30초 가만있으면 쟁반으로 돌아온다
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
public class PyriteSkipStone : UdonSharpBehaviour
{
    public Transform home;
    public Collider waterCollider;
    public ParticleSystem splash;
    public ParticleSystem ring;
    public PyriteLakeRipple ripple;
    public float surfaceY = 0.05f;
    public float minSpeed = 2.5f;
    public float maxAngle = 35f;
    public int maxSkips = 12;

    private Rigidbody rb;
    private Collider col;
    private VRCObjectSync sync;
    private int skips;
    private bool sinking, held;
    private float sinkT, idleT, prevY;

    private void Start()
    {
        rb = (Rigidbody)GetComponent(typeof(Rigidbody));
        col = (Collider)GetComponent(typeof(Collider));
        sync = (VRCObjectSync)GetComponent(typeof(VRCObjectSync));
        prevY = transform.position.y;
    }

    public override void OnPickup() { held = true; skips = 0; sinking = false; if (col != null) col.enabled = true; if (rb != null) rb.drag = 0.05f; }

    public override void OnDrop() { held = false; skips = 0; idleT = 0f; }

    private void OnCollisionEnter(Collision c)
    {
        if (!Networking.IsOwner(gameObject) || sinking || held || c == null || c.collider != waterCollider) return;
        Vector3 v = -c.relativeVelocity;
        float h = Mathf.Sqrt(v.x * v.x + v.z * v.z);
        float vy = Mathf.Abs(v.y);
        float ang = Mathf.Atan2(vy, Mathf.Max(h, 0.001f)) * Mathf.Rad2Deg;
        Vector3 p = transform.position;
        if (h > minSpeed && ang < maxAngle && skips < maxSkips)
        {
            skips++;
            rb.velocity = new Vector3(v.x * 0.82f, Mathf.Max(vy * 0.45f, 0.8f + h * 0.05f), v.z * 0.82f);
            rb.angularVelocity = new Vector3(0f, 25f, 0f);
            Splash(p, 0.6f);
        }
        else
        {
            Splash(p, 1f);
            sinking = true; sinkT = 0f;
            if (col != null) col.enabled = false;
            if (rb != null) rb.drag = 4f;
        }
    }

    private void Update()
    {
        float y = transform.position.y;
        if (!Networking.IsOwner(gameObject))
        {
            if (prevY > surfaceY + 0.03f && y <= surfaceY + 0.03f && OverWater()) Splash(transform.position, 0.7f);
        }
        else if (rb != null)
        {
            if (sinking)
            {
                sinkT += Time.deltaTime;
                if (sinkT > 2.5f) Respawn();
            }
            else if (!held && home != null && (transform.position - home.position).sqrMagnitude > 0.09f && rb.velocity.sqrMagnitude < 0.01f)
            {
                idleT += Time.deltaTime;
                if (idleT > 30f) Respawn();
            }
            else idleT = 0f;
        }
        prevY = y;
    }

    private bool OverWater()
    {
        if (waterCollider == null) return false;
        Bounds b = waterCollider.bounds;
        Vector3 p = transform.position;
        return p.x > b.min.x && p.x < b.max.x && p.z > b.min.z && p.z < b.max.z;
    }

    private void Respawn()
    {
        sinking = false; skips = 0; idleT = 0f;
        if (col != null) col.enabled = true;
        if (rb != null) { rb.drag = 0.05f; rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        if (home != null) transform.SetPositionAndRotation(home.position, home.rotation);
        if (sync != null) sync.FlagDiscontinuity();
    }

    private void Splash(Vector3 p, float s)
    {
        p.y = surfaceY;
        if (splash != null) { splash.transform.position = p; splash.Emit((int)(12f * s)); }
        if (ring != null) { ring.transform.position = p + Vector3.up * 0.01f; ring.Emit(1); }
        if (ripple != null) ripple.AddDrop(p.x, p.z, 0.012f * s);
    }
}
