// 별똥별 — 서버 시각으로 약 1분(interval)마다 하나, 밤에만(해 고도 < nightSunEl). 네트워크 동기화 없이 모두 같은 순간·같은 궤적을 본다
//  (이벤트 번호 k = 서버 시각 / interval, 궤적·지연은 k 로 만든 의사난수)
//  꼬리는 TrailRenderer. 하늘 반지름 radius 의 구 위를 짧게 긋는다. 절벽(최대 앙각 약 30°) 위로만 지나가게 고도 32° 이상
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class PyriteMeteors : UdonSharpBehaviour
{
    public PyriteDayCycle cycle;
    public Transform head;              // TrailRenderer 가 붙은 머리
    public TrailRenderer trail;
    public Vector3 center = new Vector3(-10f, 0f, 45f);
    public float radius = 650f;
    public float interval = 60f;        // 초 (하루 12분 → 게임 속 2시간마다 하나)
    public float jitter = 35f;          // 간격 안에서 흩어짐
    public float nightSunEl = -8f;

    private int activeK = -1;
    private float t0, dur;
    private Vector3 d0, d1;

    // 정수 해시 → 0~1 (Udon 은 long % 가 막혀 있다. 큰 수를 float 로 곱하면 salt 차이가 정밀도에 묻힌다 → 정수 연산)
    private float H(int k, int salt)
    {
        int h = k * 73856093 ^ salt * 19349663;
        h = (h ^ (h >> 13)) * 1274126177;
        h = h ^ (h >> 16);
        return (h & 0xFFFF) / 65535f;
    }

    private Vector3 Dir(float el, float az)
    {
        float e = el * Mathf.Deg2Rad, a = az * Mathf.Deg2Rad;
        return new Vector3(Mathf.Sin(a) * Mathf.Cos(e), Mathf.Sin(e), Mathf.Cos(a) * Mathf.Cos(e));
    }

    private void Update()
    {
        if (head == null || trail == null) return;
        double now = Networking.GetServerTimeInSeconds();
        int k = (int)(now / interval);
        float start = (float)((double)k * interval - now) + H(k, 1) * jitter;   // 이번 이벤트 시작까지 남은 초 (음수면 지남). 서버 시각이 커서 float 로 곱하면 정밀도가 날아간다 → double
        float d = 0.55f + H(k, 2) * 0.6f;
        bool night = cycle == null || cycle.sunElNow < nightSunEl;
        float age = -start;
        if (night && age >= 0f && age <= d + trail.time)
        {
            if (activeK != k)
            {
                activeK = k;
                float az = H(k, 3) * 360f;
                float el = 42f + H(k, 4) * 26f;
                float daz = (H(k, 5) - 0.5f) * 36f;
                float del = -(9f + H(k, 6) * 10f);
                d0 = Dir(el, az);
                d1 = Dir(Mathf.Max(32f, el + del), az + daz);
                head.position = center + d0 * radius;
                trail.Clear();
                trail.emitting = true;
                if (!head.gameObject.activeSelf) head.gameObject.SetActive(true);
            }
            if (age <= d)
            {
                float u = age / d;
                head.position = center + Vector3.Slerp(d0, d1, u) * radius;
            }
            else trail.emitting = false;
        }
        else if (head.gameObject.activeSelf)
        {
            trail.emitting = false;
            trail.Clear();
            head.gameObject.SetActive(false);
            activeK = -1;
        }
    }
}
