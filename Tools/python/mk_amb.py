import numpy as np, wave, struct, math

SR = 44100
rng = np.random.default_rng(20260923)

def circ_noise(n, seed):
    """주파수 영역에서 만든 백색잡음 → N샘플 주기로 정확히 순환한다(루프 이음매 0)."""
    r = np.random.default_rng(seed)
    half = n//2 + 1
    mag = np.ones(half)
    ph  = r.uniform(0, 2*np.pi, half)
    ph[0] = 0
    if n % 2 == 0: ph[-1] = 0
    spec = mag*np.exp(1j*ph)
    return np.fft.irfft(spec, n)

def shape(x, curve):
    """curve(f_hz) -> gain. 순환성을 유지한 채 스펙트럼을 깎는다."""
    n = len(x)
    S = np.fft.rfft(x)
    f = np.fft.rfftfreq(n, 1.0/SR)
    return np.fft.irfft(S*curve(f), n)

def norm(x, peak=0.9):
    m = np.max(np.abs(x))
    return x if m < 1e-9 else x*(peak/m)

def write(path, x, sr=SR):
    x = np.clip(x, -1.0, 1.0)
    d = (x*32767).astype('<i2')
    w = wave.open(path, 'wb'); w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
    w.writeframes(d.tobytes()); w.close()
    return len(x)/sr

def periodic_lfo(n, dur, comps):
    """comps = [(cycles_per_loop, amp, phase)] — 정수 사이클만 써서 루프가 끊기지 않게."""
    t = np.arange(n)/n
    o = np.zeros(n)
    for c, a, p in comps:
        o += a*np.sin(2*np.pi*c*t + p)
    return o

# ---------------------------------------------------------------- 1. 물가
DUR_L = 24.0
nL = int(SR*DUR_L)
base = circ_noise(nL, 101)
# 물결: 250~2200Hz 대역, 고역은 완만히 롤오프
def curve_lake(f):
    lo = 1.0/(1.0 + (180.0/np.maximum(f,1.0))**4)      # 하이패스 180
    hi = 1.0/(1.0 + (np.maximum(f,1.0)/1600.0)**2)     # 로우패스 1600
    return lo*hi
lake = shape(base, curve_lake)
lake = norm(lake, 1.0)
# 잔물결 스웰 — 서로소 사이클 수로 겹쳐서 주기감이 안 들리게
sw = periodic_lfo(nL, DUR_L, [(7,0.30,0.0),(11,0.20,1.7),(17,0.13,3.1),(29,0.08,0.4)])
lake = lake*(0.62 + sw)
# 아주 낮은 웅웅거림 한 겹
low = shape(circ_noise(nL,102), lambda f: 1.0/(1.0+(np.maximum(f,1.0)/120.0)**3))
lake = lake*0.85 + norm(low,1.0)*0.18
print('lake  %.2fs  rms=%.4f' % (write('/home/claude/audio/A_Lake.wav', norm(lake,0.82)), np.sqrt(np.mean(lake**2))))

# ---------------------------------------------------------------- 2. 풀벌레
DUR_C = 30.0
nC = int(SR*DUR_C)
t = np.arange(nC)/SR
crick = np.zeros(nC)

def voice(f0, chirps_per_loop, pulses, pulse_ms, gap_ms, amp, jitter_seed):
    """chirps_per_loop을 정수로 두면 루프 경계에서 리듬이 안 끊긴다."""
    r = np.random.default_rng(jitter_seed)
    out = np.zeros(nC)
    period = nC//chirps_per_loop
    pl = int(SR*pulse_ms/1000.0)
    gp = int(SR*gap_ms/1000.0)
    env_p = np.sin(np.pi*np.arange(pl)/pl)**1.6          # 펄스 한 개 포락선
    for k in range(chirps_per_loop):
        start = k*period + int(r.uniform(0, period*0.18))
        ff = f0*(1.0 + r.uniform(-0.02, 0.02))
        aa = amp*(0.75 + 0.5*r.random())
        for p in range(pulses):
            s = start + p*(pl+gp)
            idx = (np.arange(pl) + s) % nC                # 순환 배치 → 경계 안전
            ph = 2*np.pi*ff*np.arange(pl)/SR
            tone = np.sin(ph) + 0.45*np.sin(2*ph) + 0.18*np.sin(3*ph)
            out[idx] += tone*env_p*aa
    return out

crick += voice(4200, 40, 4, 16, 26, 0.55, 201)
crick += voice(3650, 33, 5, 14, 22, 0.42, 202)
crick += voice(5100, 27, 3, 12, 30, 0.30, 203)
crick += voice(2900, 21, 6, 18, 20, 0.26, 204)
# 멀리서 깔리는 벌레 소리 층 — 고역 잡음을 약하게
bed = shape(circ_noise(nC,205), lambda f: (1.0/(1.0+(3000.0/np.maximum(f,1.0))**6))*(1.0/(1.0+(np.maximum(f,1.0)/7000.0)**4)))
bed = norm(bed,1.0)*(0.5+0.5*periodic_lfo(nC,DUR_C,[(5,0.4,0.0),(13,0.25,2.2)]))
crick = norm(crick,1.0)*0.92 + bed*0.10
print('crick %.2fs  rms=%.4f' % (write('/home/claude/audio/A_Crickets.wav', norm(crick,0.78)), np.sqrt(np.mean(crick**2))))

# ---------------------------------------------------------------- 3. 모닥불
DUR_F = 20.0
nF = int(SR*DUR_F)
# 바탕: 낮은 쉭쉭거림
hiss = shape(circ_noise(nF,301), lambda f: (1.0/(1.0+(90.0/np.maximum(f,1.0))**3))*(1.0/(1.0+(np.maximum(f,1.0)/1100.0)**2)))
hiss = norm(hiss,1.0)*(0.55 + periodic_lfo(nF,DUR_F,[(3,0.18,0.0),(7,0.12,1.1),(13,0.07,2.5)]))
# 탁탁 터지는 소리
r = np.random.default_rng(302)
pops = np.zeros(nF)
NPOP = 190
for i in range(NPOP):
    s   = int(r.uniform(0, nF))
    ln  = int(SR*r.uniform(0.004, 0.030))
    amp = r.uniform(0.15, 1.0)**2.2                      # 대부분 작고 가끔 크게
    env = np.exp(-np.arange(ln)/(ln*r.uniform(0.18,0.35)))
    nz  = r.standard_normal(ln)
    # 팝마다 밝기를 달리 — 간이 1차 로우패스
    a = r.uniform(0.25, 0.75)
    y = np.zeros(ln); acc = 0.0
    for j in range(ln):
        acc = acc*(1-a) + nz[j]*a
        y[j] = acc
    idx = (np.arange(ln) + s) % nF                        # 순환 배치
    pops[idx] += y*env*amp
fire = norm(hiss,1.0)*0.55 + norm(pops,1.0)*0.75
print('fire  %.2fs  rms=%.4f' % (write('/home/claude/audio/A_Campfire.wav', norm(fire,0.80)), np.sqrt(np.mean(fire**2))))

# 루프 이음매 검증: 끝 512샘플과 앞 512샘플의 연속성
for name in ['A_Lake','A_Crickets','A_Campfire']:
    w = wave.open('/home/claude/audio/%s.wav'%name,'rb')
    d = np.frombuffer(w.readframes(w.getnframes()), dtype='<i2').astype(float)/32768
    seam = abs(d[0]-d[-1]); local = np.mean(np.abs(np.diff(d[:512])))
    print('%-12s seam=%.5f  평균인접차=%.5f  비율=%.2f' % (name, seam, local, seam/max(local,1e-9)))
