import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
from terr import H, ss, LAKE_C, LAKE_R
RES=513; SIZE=200.0; TY=60.0; TBASE=-10.0
PAD_C=(-10.0, 41.5); PAD_FLAT=7.8; PAD_BLEND=10.2

rl_pad = math.hypot(PAD_C[0]-LAKE_C[0], PAD_C[1]-LAKE_C[1])
print(f'패드 중심 → 호수 중심 거리 {rl_pad:.2f}m, 호수 반경 {LAKE_R}')
print(f'패드 최외곽이 호수에 가장 가까워지는 지점: {rl_pad-PAD_BLEND:.2f}m  (44보다 커야 안전)')

# pad height = mean terrain over the flat disc
acc=[]
for i in range(40):
    for j in range(40):
        a=2*math.pi*i/40; r=PAD_FLAT*math.sqrt(j/39)
        acc.append(H(PAD_C[0]+r*math.cos(a), PAD_C[1]+r*math.sin(a)))
PAD_Y=float(np.mean(acc))
print(f'패드 높이(평탄 구역 평균): {PAD_Y:.4f}')

xs=np.linspace(-SIZE/2, SIZE/2, RES)
Y=np.zeros((RES,RES))
for i in range(RES):
    for j in range(RES):
        x,z=xs[j],xs[i]
        y=H(x,z)
        d=math.hypot(x-PAD_C[0], z-PAD_C[1])
        w=1.0-ss(PAD_FLAT, PAD_BLEND, d)     # 1 inside flat, 0 outside blend
        Y[i,j]=y*(1-w)+PAD_Y*w
u16=(np.clip((Y-TBASE)/TY,0,1)*65535.0).astype('<u2')
open('/mnt/user-data/outputs/PyriteHome_Terrain.raw','wb').write(u16.tobytes())

# verification
X,Z=np.meshgrid(xs,xs)
rl=np.hypot(X-LAKE_C[0], Z-LAKE_C[1]); R=np.hypot(X,Z)
stray=((Y<0)&(rl>LAKE_R+1)).sum()
print(f'호수 밖 y<0 셀: {stray}')
print(f'전체 높이 범위: {Y.min():.2f} ~ {Y.max():.2f}')
# cut / fill
cut=fill=0.0
for i in range(RES):
    for j in range(RES):
        d=math.hypot(xs[j]-PAD_C[0], xs[i]-PAD_C[1])
        if d<PAD_BLEND:
            dy=Y[i,j]-H(xs[j],xs[i])
            cut=min(cut,dy); fill=max(fill,dy)
print(f'최대 절토 {-cut:.2f}m / 최대 성토 {fill:.2f}m')
