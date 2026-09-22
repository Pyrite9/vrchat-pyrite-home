import sys, math, numpy as np
sys.path.insert(0,'/home/claude')
from h2 import H

# ── 치수 (월드 좌표) ──────────────────────────────────────────
CX      = -10.0     # 부두 중심 x (캠프 화로와 같은 축)
Z0      =  42.6     # 뭍 쪽 끝 (v3 지형에서 H=+0.512, 덱 상면 0.50과 거의 평평)
Z1      =  31.0     # 물 쪽 끝 (지면 H≈-2.3)
HALF_W  =  1.00     # 덱 반폭
DECK_Y  =  0.50     # 덱 상면
PLANK_T =  0.06
PITCH   =  0.25     # 판자 간격(중심 간)
PLANK_D =  0.22     # 판자 폭 → 틈 0.03
STR_X   =  0.70     # 장선 x 오프셋
STR_W   =  0.16
STR_TOP =  DECK_Y - PLANK_T      # 0.44
STR_BOT =  DECK_Y - PLANK_T-0.22 # 0.22
POST_S  =  0.18
POST_Z  = [42.2, 39.9, 37.6, 35.3, 33.0, 31.3]
BOLLARD = (CX-STR_X, 31.35)      # 배 묶는 말뚝

V, F, T = [], [], []             # 정점 / 면(1-based) / uv

def box(cx,cy,cz, sx,sy,sz, band, ulen=2.0):
    """축 정렬 박스. band = 0..7 목재 텍스처 가로 띠 번호"""
    hx,hy,hz = sx/2, sy/2, sz/2
    v0=len(V)
    c=[(-1,-1,-1),( 1,-1,-1),( 1,-1, 1),(-1,-1, 1),
       (-1, 1,-1),( 1, 1,-1),( 1, 1, 1),(-1, 1, 1)]
    for ax,ay,az in c:
        V.append((cx+ax*hx, cy+ay*hy, cz+az*hz))
    v_lo = band/8.0 + 0.004
    v_hi = (band+1)/8.0 - 0.004
    # 면마다 가로축 길이를 ulen으로 나눠 U, 세로는 띠 안에 가둔다
    faces=[(0,1,2,3),(7,6,5,4),(4,5,1,0),(6,7,3,2),(5,6,2,1),(7,4,0,3)]
    dims =[(sx,sz),(sx,sz),(sx,sy),(sx,sy),(sz,sy),(sz,sy)]
    for (a,b,cc,d),(du,dv) in zip(faces,dims):
        t0=len(T)
        for (uu,vv) in [(0,0),(du/ulen,0),(du/ulen,1),(0,1)]:
            T.append((uu, v_lo+vv*(v_hi-v_lo)))
        q=[v0+a, v0+b, v0+cc, v0+d]
        F.append(((q[0]+1,t0+1),(q[1]+1,t0+2),(q[2]+1,t0+3)))
        F.append(((q[0]+1,t0+1),(q[2]+1,t0+3),(q[3]+1,t0+4)))

rng=np.random.default_rng(7)

# 판자
n=int((Z0-Z1)/PITCH)+1
for i in range(n):
    z=Z0-i*PITCH
    jitter=float(rng.uniform(-0.012,0.012))
    box(CX, DECK_Y-PLANK_T/2, z, HALF_W*2+jitter, PLANK_T, PLANK_D,
        int(rng.integers(0,8)), ulen=2.4)

# 장선 2줄
for sx in (-STR_X, STR_X):
    box(CX+sx, (STR_TOP+STR_BOT)/2, (Z0+Z1)/2, STR_W, STR_TOP-STR_BOT, Z0-Z1+0.2, 3, ulen=3.0)

# 기둥
for z in POST_Z:
    for sx in (-STR_X, STR_X):
        x=CX+sx
        bed=H(x,z)-0.40
        top=STR_BOT+0.02
        box(x, (top+bed)/2, z, POST_S, top-bed, POST_S, 6, ulen=1.0)

# 계선주
bx,bz=BOLLARD
box(bx, (DECK_Y+1.10)/2, bz, 0.20, 1.10-DECK_Y+0.30, 0.20, 5, ulen=0.8)

# ── 와인딩 검증: 박스는 중심에서 바깥을 향해야 한다 ──────────────
Vn=np.array(V); bad=0
for i in range(0,len(F),12):          # 박스 단위
    vs=np.array([Vn[a-1] for tri in F[i:i+12] for (a,_) in tri])
    ctr=vs.mean(0)
    for tri in F[i:i+12]:
        p=[Vn[a-1] for (a,_) in tri]
        nrm=np.cross(p[1]-p[0], p[2]-p[0])
        if np.dot(nrm, (p[0]+p[1]+p[2])/3 - ctr) <= 0: bad+=1
print('뒤집힌 삼각형:', bad, '/', len(F))
assert bad==0

with open('/mnt/user-data/outputs/PyriteDock.obj','w') as f:
    f.write('# PyriteHome dock\no dock\n')
    # ★ Unity OBJ 임포터는 X를 뒤집으면서 면 순서도 같이 뒤집는다(방향 보존).
    #   실측: 바운즈 중심 -10 → +10, 부호부피 -2.858(뒤집힘).
    #   그래서 X를 미리 뒤집으면 위치는 맞지만 면이 한 번 더 뒤집힌다.
    #   → 정점 x 부호 반전 + 면 순서 반전, 둘 다 해야 제자리 + 바깥면.
    for x,y,z in V: f.write('v %.4f %.4f %.4f\n'%(-x,y,z))
    for u,v in T:   f.write('vt %.5f %.5f\n'%(u,v))
    for tri in F:   f.write('f '+' '.join('%d/%d'%(a,b) for a,b in tri[::-1])+'\n')
print('정점 %d / 삼각형 %d / 길이 %.1fm'%(len(V), len(F), Z0-Z1))
print('덱 상면 y=%.2f  뭍끝 지면 %.2f  물끝 지면 %.2f'%(DECK_Y, H(CX,Z0), H(CX,Z1)))
