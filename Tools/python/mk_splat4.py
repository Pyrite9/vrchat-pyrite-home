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

w_rock  = np.maximum(ss(27.0,41.0,slope), ss(66.0,75.0,Rj))
w_shore = (1.0-ss(2.5,9.0,np.abs(rlj-58.0)))
w_mead  = ss(58.0,66.0,rlj)*(1.0-ss(64.0,74.0,Rj))*(1.0-ss(28.0,40.0,slope))
w_mead  = np.maximum(w_mead, 0.0)

dpad=np.hypot(X-PAD_C[0], Z-PAD_C[1])
pad=1.0-ss(3.2,6.5,dpad)
w_shore=np.maximum(w_shore, pad*0.80)
w_mead =w_mead*(1.0-pad*0.85)

# 꽃 레이어 없음 — 실제 꽃 메시가 깔리므로 지면은 풀만
w_grass = w_mead

# ── 호수 바닥 전용 레이어 ─────────────────────────────────────────
# 물 밑을 흙(탄색)으로 칠하면 반투명 수면으로 누렇게 비쳐서 호수가 더러워 보인다.
# 수면(y=0) 아래는 전용 실트 텍스처로 덮고, 다른 레이어는 물 밖에서만 남긴다.
under = 1.0 - ss(-0.70, -0.05, Hh)          # 1 = 물속 깊음, 0 = 물가/뭍
w_lake  = under
w_rock  = w_rock  * (1.0-under)
w_shore = w_shore * (1.0-under)
w_grass = w_grass * (1.0-under)

eps=1e-4
w=np.stack([w_grass,w_shore,w_rock,w_lake],-1)+eps
w/=w.sum(-1,keepdims=True)
u8=np.clip(np.rint(w*255),0,255).astype(np.uint8)

open('/mnt/user-data/outputs/terrain/Splat4.bin','wb').write(
    np.uint32(RES).tobytes()+np.uint32(4).tobytes()+u8.tobytes())
print('RES',RES,'layers 4  bytes',8+RES*RES*4)
for i,n in enumerate(['grass','ground','rock','lakebed']):
    print('%-7s 평균 %.3f  최대영역 %.1f%%'%(n, w[:,:,i].mean(), 100*(w.argmax(-1)==i).mean()))
