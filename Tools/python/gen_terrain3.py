import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
import h2
from h2 import RES,SIZE,TY,TBASE,LAKE_C,LAKE_R,LAKE_D,PAD_C,PAD_FLAT,PAD_BLEND,PAD_Y
from h2 import PAD_LAKE_CUT,PAD_LAKE_TAPER,PAD_JITTER

i=np.arange(RES); x=-100.0+SIZE*i/(RES-1)
X,Z=np.meshgrid(x,x,indexing='xy')          # [iz, ix] — gen_terrain.py와 동일 규약
def ss(a,b,t):
    t=np.clip((t-a)/(b-a),0,1); return t*t*(3-2*t)

rl=np.hypot(X-LAKE_C[0],Z-LAKE_C[1]); R=np.hypot(X,Z)
depth=LAKE_D*(1.0-ss(0.5*LAKE_R,LAKE_R,rl))
beach=1.8*ss(LAKE_R,LAKE_R+26,rl)
n=(0.75*np.sin(X*.068)*np.cos(Z*.059)
  +0.45*np.sin(X*.131+1.2)*np.sin(Z*.113+.4)
  +0.25*np.sin(X*.241+3.1)*np.cos(Z*.207+2.2))
n=(n+1.45)*0.55
land=ss(LAKE_R-1.0,LAKE_R+12.0,rl)*(1.0-ss(74.0,86.0,R))
cliff=3.2*ss(72.0,90.0,R); outer=9.0*ss(90.0,104.0,R)
Y0=-depth+beach+n*land+cliff+outer

dx=X-PAD_C[0]; dz=Z-PAD_C[1]; d=np.hypot(dx,dz)
lw=np.clip(np.where(d<1e-6,0.0,-dz/np.maximum(d,1e-6)),0,1)
lw=lw*lw*(3-2*lw)
ang=np.arctan2(dx,dz)
jit=PAD_JITTER*(0.62*np.sin(ang*3.0+0.7)+0.28*np.sin(ang*5.0-1.9)+0.10*np.sin(ang*8.0+2.6))
flat=np.maximum(1.0, PAD_FLAT-PAD_LAKE_CUT*lw+jit)
blend=flat+(PAD_BLEND-PAD_FLAT)+PAD_LAKE_TAPER*lw
w=1.0-ss(flat,blend,d)
Y=Y0*(1-w)+PAD_Y*w
Y=np.clip(Y,-9.5,22.0)

# 스칼라 h2.H와 대조 (규약이 어긋나면 여기서 잡힌다)
mx=0.0
for iz in range(0,RES,37):
    for ix in range(0,RES,41):
        mx=max(mx, abs(Y[iz,ix]-h2.H(float(X[iz,ix]),float(Z[iz,ix]))))
print('스칼라 H와 최대 편차 %.6f m'%mx)
assert mx<1e-9

u16=(np.clip((Y-TBASE)/TY,0,1)*65535.0).astype('<u2')
u16.tofile('/mnt/user-data/outputs/terrain/PyriteHome_Terrain_v3.raw')
print('bytes',u16.nbytes,' Ymin %.2f Ymax %.2f'%(Y.min(),Y.max()))

old=np.fromfile('/mnt/user-data/outputs/PyriteHome_Terrain.raw',dtype='<u2').reshape(RES,RES)
dif=(u16.astype(int)-old.astype(int))*TY/65535.0
ch=np.abs(dif)>0.01
print('변경 셀 %d / %d (%.2f%%)  최대 상승 %+.2fm  최대 하강 %+.2fm'%(
    ch.sum(), RES*RES, 100*ch.mean(), dif.max(), dif.min()))
ii,jj=np.where(ch)
if len(ii): print('변경 영역  x %.1f~%.1f   z %.1f~%.1f'%(x[jj.min()],x[jj.max()],x[ii.min()],x[ii.max()]))
