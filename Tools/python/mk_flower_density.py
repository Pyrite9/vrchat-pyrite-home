import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
from h2 import H, LAKE_C, LAKE_R, PAD_C

DRES=256; SIZE=200.0
xs=(-100.0+SIZE*(np.arange(DRES)+0.5)/DRES)

def ss(a,b,t):
    t=np.clip((t-a)/(b-a),0,1); return t*t*(3-2*t)

X,Z=np.meshgrid(xs,xs,indexing='xy')     # X[iz,ix], Z[iz,ix]
Hh=np.zeros((DRES,DRES))
for i in range(DRES):
    for j in range(DRES):
        Hh[i,j]=H(X[i,j],Z[i,j])

rl=np.hypot(X-LAKE_C[0], Z-LAKE_C[1])
R =np.hypot(X,Z)
dpad=np.hypot(X-PAD_C[0], Z-PAD_C[1])

# 경사도 (도)
gx,gz=np.gradient(Hh, SIZE/DRES)
slope=np.degrees(np.arctan(np.hypot(gx,gz)))

# [E3] 물가 경계를 흩트린다 — 등고선을 따라 매끈하게 잘리던 꽃밭 끝을
#      (1) 저주파 노이즈로 들쭉날쭉하게, (2) 6m 에 걸쳐 서서히, (3) 모래 쪽에 흩어진 몇 송이
rng=np.random.default_rng(7)
def meander(X,Z):
    n=np.zeros_like(X)
    for k,(wl,amp) in enumerate(((23.0,1.0),(11.0,0.55),(5.5,0.30))):
        for d in range(3):
            a=rng.uniform(0,2*np.pi); ph=rng.uniform(0,2*np.pi)
            n+=amp*np.sin((X*np.cos(a)+Z*np.sin(a))*2*np.pi/wl+ph)
    return n/np.abs(n).max()
nz=meander(X,Z)
edge=56.0+2.0*nz                       # 경계 시작 반경 54 ~ 58 (원래 56.5 근처)
m  = ss(edge,edge+5.5,rl)             # 물가에서 멀어질수록 (5.5m 페더 — 원래 56.5→62 와 평균이 같게)
m *= (1.0-ss(67.0,76.0,R))            # 절벽 바닥에서 사라짐
m *= ss(5.5,9.5,np.hypot(X+10.5,Z-53.5))   # 캠프 소품 주변만 비우기 (v3 배치 중심)
m *= (1.0-ss(30.0,42.0,slope))        # 급경사 제외
m *= (Hh>0.20)                        # 수면 위만

# 캠프 → 부두로 밟고 다니는 길은 비워 둔다
def seg_dist(px,pz, ax,az, bx,bz):
    vx,vz=bx-ax,bz-az; wx,wz=px-ax,pz-az
    t=np.clip((wx*vx+wz*vz)/(vx*vx+vz*vz),0,1)
    return np.hypot(wx-t*vx, wz-t*vz)
dpath=seg_dist(X,Z, -10.5,51.5, -10.0,43.0)
m *= ss(1.8,3.6,dpath)

# ★ 패치 노이즈 제거 — 잔디가 비쳐서 얼룩덜룩해 보였다.
#   마스크가 살아 있는 곳은 전부 COV_FULL(1.35)을 넘기게 채운다.
#   coverage = (L0*0.137 + L1*0.504) / 0.6104  →  L0=2,L1=3 이면 2.93 (>1.35)
# (확률적 반올림은 I 의 타일 중심 샘플이 0 을 집어 10m 타일이 통째로 빠지는 구멍을 만들었다 → rint 유지.
#  페더 구간의 흩어짐은 I 가 삼각형 단위 확률로 이미 만든다)
L0=np.rint(np.clip(m*2.0,0,2)).astype(np.uint8)   # 작은 클럼프(20 tris)
L1=np.rint(np.clip(m*3.2,0,4)).astype(np.uint8)   # 큰 클럼프(40 tris)
# 모래 쪽에 흩어진 꽃 — 물 위(H>0.2)이고 경계 안쪽 3m, 셀 4% 에만 작은 클럼프 1개
stray=(Hh>0.20)&(rl>edge-3.0)&(rl<edge)&(rng.random(m.shape)<0.04)
stray&=(1.0-ss(67.0,76.0,R))>0.5
stray&=ss(5.5,9.5,np.hypot(X+10.5,Z-53.5))>0.5
stray&=ss(1.8,3.6,dpath)>0.5
L0=np.where(stray&(L0==0),1,L0).astype(np.uint8)

buf=np.stack([L0,L1],axis=-1).astype(np.uint8)   # [iz, ix, layer]
open('/mnt/user-data/outputs/flora/FlowerDensity.bin','wb').write(
    np.uint32(DRES).tobytes()+buf.tobytes())

cell=(SIZE/DRES)**2
print('DRES %d  cell %.3f m^2'%(DRES,cell))
print('L0 인스턴스 %d  L1 인스턴스 %d  합 %d'%(L0.sum(),L1.sum(),L0.sum()+L1.sum()))
print('꽃 있는 셀 %d (%.1f%%)  면적 %.0f m^2'%((L0+L1>0).sum(),100*(L0+L1>0).mean(),(L0+L1>0).sum()*cell))
print('평균 밀도 %.2f clump/m^2'%((L0.sum()+L1.sum())/max(1,(L0+L1>0).sum())/cell))
cellA=(SIZE/DRES)**2
print('예상 최대 tris %d'%(L0.sum()*20+L1.sum()*48))
print('수평 커버리지 %.2f (1.0 이상이면 지면이 안 보임)'%((L0.sum()*0.137+L1.sum()*0.504)/max(1,(L0+L1>0).sum())/cellA))
# 마스크 미리보기
from PIL import Image
img=np.zeros((DRES,DRES,3),np.uint8)
img[:,:,2]=np.clip(L0*60,0,255); img[:,:,1]=np.clip(L1*110,0,255)
img[Hh<0.0]=[20,30,60]
img[(dpad<9)]=[200,120,0]
Image.fromarray(img[::-1]).resize((512,512),Image.NEAREST).save('/home/claude/flower_mask.png')
