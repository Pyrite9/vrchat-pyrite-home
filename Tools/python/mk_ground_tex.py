import numpy as np
from PIL import Image

N=512
rng=np.random.default_rng(99)

def fbm(octaves, lac=2.0, gain=0.5, base=4, seed=0):
    """FFT 기반 타일링 가능한 fractal noise"""
    r=np.random.default_rng(seed)
    out=np.zeros((N,N)); amp=1.0; tot=0.0; f=base
    for o in range(octaves):
        w=r.standard_normal((N,N))
        F=np.fft.fft2(w)
        fy=np.fft.fftfreq(N)*N; fx=np.fft.fftfreq(N)*N
        FY,FX=np.meshgrid(fy,fx,indexing='ij')
        R=np.hypot(FY,FX)
        band=np.exp(-((R-f)/(f*0.55+1e-6))**2)
        band[0,0]=0
        n=np.real(np.fft.ifft2(F*band))
        n/= (n.std()+1e-9)
        out+=amp*n; tot+=amp
        amp*=gain; f*=lac
    return out/tot

def norm(a,lo=0.0,hi=1.0):
    a=(a-a.min())/(a.max()-a.min()+1e-9)
    return lo+a*(hi-lo)

def streaks(seed, n=9000, L=26, w=1):
    """풀잎 같은 가는 선들 (타일링 되게 wrap)"""
    img=np.zeros((N,N))
    r=np.random.default_rng(seed)
    for _ in range(n):
        x0,y0=r.integers(0,N,2); ang=r.uniform(-0.55,0.55)+ (np.pi/2)
        ln=r.integers(L//2,L)
        v=r.uniform(0.4,1.0)
        for t in range(ln):
            x=int(x0+np.cos(ang)*t)%N; y=int(y0-np.sin(ang)*t)%N
            img[y,x]=max(img[y,x], v*(1-t/ln))
    return img

def save(rgb, path):
    Image.fromarray(np.clip(rgb*255,0,255).astype(np.uint8)).save(path)
    print(path, 'mean', np.round(rgb.reshape(-1,3).mean(0),3))

# ---- 1. 초원 (네모필라 카펫 밑의 지면) ----
n1=norm(fbm(5, base=3, seed=1))
n2=norm(fbm(4, base=14, seed=2))
g =streaks(3, n=14000, L=22)
base=np.stack([
    0.135+0.075*n1+0.030*n2,
    0.190+0.105*n1+0.045*n2,
    0.110+0.055*n1+0.028*n2],-1)
base+= (g[...,None]*np.array([0.055,0.105,0.045]))
# 군데군데 마른 땅
dry=np.clip(norm(fbm(3, base=2, seed=7))*1.4-0.62,0,1)[...,None]
base=base*(1-dry*0.55)+dry*0.55*np.array([0.30,0.27,0.19])
# 먼 거리에서 디테일 메시가 컬링돼도 꽃밭으로 읽히도록, 지면 자체에 연한 꽃 점묘
grass=base.copy()
save(grass, '/mnt/user-data/outputs/terrain/T_Ground_Grass.png')

# ---- 1b. 꽃 지면 — 실제 꽃이 깔린 셀에만 칠해진다 (스플랫이 꽃 밀도를 따라감) ----
sp=np.random.default_rng(31)
dots=np.zeros((N,N))
yy,xx=np.mgrid[0:N,0:N]
for _ in range(7000):
    cx,cy=sp.integers(0,N,2); r=sp.uniform(2.0,4.4)
    d=np.minimum(np.abs(xx-cx),N-np.abs(xx-cx))**2+np.minimum(np.abs(yy-cy),N-np.abs(yy-cy))**2
    dots=np.maximum(dots, np.clip(1.0-d/(r*r),0,1)**0.6)
tint=np.array([0.70,0.76,0.90])
base=grass*(1-dots[...,None]*0.88)+dots[...,None]*0.88*tint
clumpy=np.clip(norm(fbm(4, base=6, seed=41))*1.5-0.42,0,1)[...,None]
base=base*(1-clumpy*0.45)+clumpy*0.45*np.array([0.78,0.82,0.92])
save(base, '/mnt/user-data/outputs/terrain/T_Ground_Flower.png')

# ---- 2. 물가 자갈/모래 ----
n1=norm(fbm(5, base=6, seed=11))
n2=norm(fbm(3, base=26, seed=12))
peb=np.clip(norm(fbm(4, base=34, seed=13))*1.6-0.55,0,1)
base=np.stack([
    0.330+0.110*n1+0.060*n2,
    0.310+0.100*n1+0.058*n2,
    0.275+0.085*n1+0.050*n2],-1)
base+= peb[...,None]*np.array([0.10,0.095,0.085])
save(base, '/mnt/user-data/outputs/terrain/T_Ground_Shore.png')

# ---- 3. 암반 (M_Bedrock 색 계열) ----
n1=norm(fbm(6, base=4, seed=21))
n2=norm(fbm(4, base=22, seed=22))
crack=np.clip(1.0-np.abs(norm(fbm(3, base=9, seed=23))-0.5)*6.0,0,1)
base=np.stack([
    0.150+0.090*n1+0.040*n2,
    0.145+0.085*n1+0.038*n2,
    0.135+0.078*n1+0.035*n2],-1)
base*= (1-crack[...,None]*0.35)
save(base, '/mnt/user-data/outputs/terrain/T_Ground_Rock.png')
