import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
from h2 import H, LAKE_C, LAKE_R, PAD_C

RES=512; SIZE=200.0
xs=(-100.0+SIZE*(np.arange(RES)+0.5)/RES)
X,Z=np.meshgrid(xs,xs,indexing='xy')     # [iz, ix]
Hh=np.zeros((RES,RES))
for i in range(RES):
    for j in range(RES):
        Hh[i,j]=H(X[i,j],Z[i,j])

def ss(a,b,t):
    t=np.clip((t-a)/(b-a),0,1); return t*t*(3-2*t)

rl=np.hypot(X-LAKE_C[0], Z-LAKE_C[1]); R=np.hypot(X,Z)
gx,gz=np.gradient(Hh, SIZE/RES)
slope=np.degrees(np.arctan(np.hypot(gx,gz)))

# 경계가 완벽한 원이 되지 않게 저주파 노이즈로 흔들기
jit=(2.6*np.sin(X*0.055+0.4)*np.cos(Z*0.049-0.9)
    +1.5*np.sin(X*0.112-1.7)*np.sin(Z*0.101+1.3)
    +0.8*np.sin(X*0.209+2.5)*np.cos(Z*0.188-0.3))
rlj=rl+jit; Rj=R+jit*0.8

wet   = 1.0-ss(40.0,56.0,rlj)                       # 호수 깊은 곳
w_rock  = np.maximum.reduce([ss(27.0,41.0,slope), ss(66.0,75.0,Rj), wet*0.75])
w_shore = (1.0-ss(4.0,15.0,np.abs(rlj-57.0)))       # 물가 띠
w_shore = np.maximum(w_shore, wet*0.55)             # 호수 바닥도 자갈
w_mead  = ss(58.0,66.0,rlj)*(1.0-ss(64.0,74.0,Rj))*(1.0-ss(28.0,40.0,slope))
w_mead  = np.maximum(w_mead, 0.0)

# 캠프 패드는 밟아 다진 땅 → 자갈 비중 조금 올림
dpad=np.hypot(X-PAD_C[0], Z-PAD_C[1])
pad=1.0-ss(5.0,9.5,dpad)
w_shore=np.maximum(w_shore, pad*0.75)
w_mead =w_mead*(1.0-pad*0.85)

# ---- 꽃 지면 레이어: 실제 디테일 메시 밀도를 그대로 따라간다 ----
import numpy as _np
raw=open('/mnt/user-data/outputs/flora/FlowerDensity.bin','rb').read()
DR=int(_np.frombuffer(raw[:4],dtype='<u4')[0])
fd=_np.frombuffer(raw[4:],dtype=_np.uint8).reshape(DR,DR,2).astype(float)
# 커버리지(m^2/셀) → 0..1
cellA=(SIZE/DR)**2
covr=(fd[:,:,0]*0.137 + fd[:,:,1]*0.504)/cellA
# 256 → 512 업샘플 (최근접 후 살짝 흐리게)
rep=RES//DR
cov=_np.repeat(_np.repeat(covr,rep,0),rep,1)
k=9
ker=_np.ones(k)/k
cov=_np.apply_along_axis(lambda r: _np.convolve(r,ker,mode='same'),0,cov)
cov=_np.apply_along_axis(lambda r: _np.convolve(r,ker,mode='same'),1,cov)
w_flower = w_mead*_np.clip(cov/2.0,0,1)          # 꽃이 실제로 깔린 만큼만
w_grass  = w_mead*(1.0-_np.clip(cov/2.0,0,1))    # 나머지는 맨 풀

eps=1e-4
w=np.stack([w_grass,w_flower,w_shore,w_rock],-1)+eps
w/=w.sum(-1,keepdims=True)
u8=np.clip(np.rint(w*255),0,255).astype(np.uint8)

open('/mnt/user-data/outputs/terrain/Splat.bin','wb').write(
    np.uint32(RES).tobytes()+np.uint32(4).tobytes()+u8.tobytes())
print('RES',RES,'layers 4  bytes',8+RES*RES*4)
for i,n in enumerate(['grass','flower','shore','rock']):
    print('%-7s 평균 %.3f  최대영역 %.1f%%'%(n, w[:,:,i].mean(), 100*(w.argmax(-1)==i).mean()))

from PIL import Image
prev=np.zeros((RES,RES,3),np.uint8)
prev[:,:,0]=u8[:,:,1]          # R = 꽃지면
prev[:,:,1]=u8[:,:,0]          # G = 풀
prev[:,:,2]=u8[:,:,3]          # B = 암반
Image.fromarray(prev[::-1]).resize((512,512),Image.NEAREST).save('/home/claude/splat_preview.png')
