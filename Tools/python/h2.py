import math, numpy as np
RES=513; SIZE=200.0; TY=60.0; TBASE=-10.0
LAKE_C=(0.0,-14.0); LAKE_R=56.0; LAKE_D=8.0
PAD_C=(-10.0,53.5); PAD_FLAT=7.8; PAD_BLEND=10.2

def ss(a,b,x):
    t=max(0.0,min(1.0,(x-a)/(b-a))); return t*t*(3-2*t)

def H0(X,Z):
    rl=math.hypot(X-LAKE_C[0], Z-LAKE_C[1]); R=math.hypot(X,Z)
    depth=LAKE_D*(1.0-ss(0.5*LAKE_R,LAKE_R,rl))
    beach=1.8*ss(LAKE_R,LAKE_R+26,rl)
    n=(0.75*math.sin(X*.068)*math.cos(Z*.059)
       +0.45*math.sin(X*.131+1.2)*math.sin(Z*.113+.4)
       +0.25*math.sin(X*.241+3.1)*math.cos(Z*.207+2.2))
    n=(n+1.45)*0.55
    land=ss(LAKE_R-1.0,LAKE_R+12.0,rl)*(1.0-ss(74.0,86.0,R))
    cliff=3.2*ss(72.0,90.0,R); outer=9.0*ss(90.0,104.0,R)
    return -depth+beach+n*land+cliff+outer

# PAD_Y는 v2와 같은 방식으로 계산한다 — 캠프 소품의 높이를 안 건드리려고 고정.
acc=[]
for i in range(40):
    for j in range(40):
        a=2*math.pi*i/40; r=PAD_FLAT*math.sqrt(j/39)
        acc.append(H0(PAD_C[0]+r*math.cos(a), PAD_C[1]+r*math.sin(a)))
PAD_Y=float(np.mean(acc))

# ── 패드 평면도(v3) ───────────────────────────────────────────────
# v2는 완전한 원판이라 사방이 똑같은 각도로 떨어져 "돔"으로 보였다.
# 두 가지를 넣는다.
#  ① 호수 쪽(-Z) 절단 — 그쪽 평탄 반경을 줄이고 블렌드를 길게 늘여
#     부두가 닿는 방향으로 완만한 비탈이 흘러내리게 한다.
#     캠프에서 호수에 제일 가까운 소품은 화로(z 51.5, 중심에서 2.0m)뿐이라
#     평탄 반경을 그쪽으로 4.4m까지 줄여도 아무것도 안 걸린다.
#  ② 경계 저주파 노이즈 — 원의 윤곽선을 ±1.0m 흔든다.
PAD_LAKE_CUT   = 3.4    # 호수쪽 평탄 반경 축소량
PAD_LAKE_TAPER = 7.2    # 호수쪽 블렌드 추가 길이
PAD_JITTER     = 1.0    # 경계 흔들림 진폭

def _pad_radii(dx, dz, d):
    lakeward = 0.0 if d < 1e-6 else max(0.0, min(1.0, -dz/d))
    lakeward = lakeward*lakeward*(3-2*lakeward)          # 부드럽게
    ang = math.atan2(dx, dz)
    jit = PAD_JITTER*(0.62*math.sin(ang*3.0+0.7)
                     +0.28*math.sin(ang*5.0-1.9)
                     +0.10*math.sin(ang*8.0+2.6))
    flat  = PAD_FLAT - PAD_LAKE_CUT*lakeward + jit
    blend = flat + (PAD_BLEND-PAD_FLAT) + PAD_LAKE_TAPER*lakeward
    return max(1.0, flat), blend

def H(X,Z):
    y=H0(X,Z)
    dx=X-PAD_C[0]; dz=Z-PAD_C[1]; d=math.hypot(dx,dz)
    flat,blend=_pad_radii(dx,dz,d)
    w=1.0-ss(flat,blend,d)
    return y*(1-w)+PAD_Y*w
