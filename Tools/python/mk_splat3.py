import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
from h2 import H, LAKE_C, LAKE_R, PAD_C

RES=512; SIZE=200.0
xs=(-100.0+SIZE*(np.arange(RES)+0.5)/RES)
X,Z=np.meshgrid(xs,xs,indexing='xy')
Hh=np.zeros((RES,RES))
for i in range(RES):
    for j in range(RES):
        Hh[i,j]=H(X[i,j],Z[i,j])

def ss(a,b,t):
    t=np.clip((t-a)/(b-a),0,1); return t*t*(3-2*t)

rl=np.hypot(X-LAKE_C[0], Z-LAKE_C[1]); R=np.hypot(X,Z)
gx,gz=np.gradient(Hh, SIZE/RES)
slope=np.degrees(np.arctan(np.hypot(gx,gz)))

jit=(2.6*np.sin(X*0.055+0.4)*np.cos(Z*0.049-0.9)
    +1.5*np.sin(X*0.112-1.7)*np.sin(Z*0.101+1.3)
    +0.8*np.sin(X*0.209+2.5)*np.cos(Z*0.188-0.3))
rlj=rl+jit; Rj=R+jit*0.8

wet     = 1.0-ss(40.0,56.0,rlj)
w_rock  = np.maximum.reduce([ss(27.0,41.0,slope), ss(66.0,75.0,Rj), wet*0.75])
w_shore = (1.0-ss(2.5,9.0,np.abs(rlj-58.0)))
w_shore = np.maximum(w_shore, wet*0.55)
w_mead  = ss(58.0,66.0,rlj)*(1.0-ss(64.0,74.0,Rj))*(1.0-ss(28.0,40.0,slope))
w_mead  = np.maximum(w_mead, 0.0)

dpad=np.hypot(X-PAD_C[0], Z-PAD_C[1])
pad=1.0-ss(3.2,6.5,dpad)
w_shore=np.maximum(w_shore, pad*0.80)
w_mead =w_mead*(1.0-pad*0.85)

# 꽃 레이어 없음 — 실제 꽃 메시가 깔리므로 지면은 풀만
w_grass = w_mead

eps=1e-4
w=np.stack([w_grass,w_shore,w_rock],-1)+eps
w/=w.sum(-1,keepdims=True)
u8=np.clip(np.rint(w*255),0,255).astype(np.uint8)

open('/mnt/user-data/outputs/terrain/Splat3.bin','wb').write(
    np.uint32(RES).tobytes()+np.uint32(3).tobytes()+u8.tobytes())
print('RES',RES,'layers 3  bytes',8+RES*RES*3)
for i,n in enumerate(['grass','ground','rock']):
    print('%-7s 평균 %.3f  최대영역 %.1f%%'%(n, w[:,:,i].mean(), 100*(w.argmax(-1)==i).mean()))
