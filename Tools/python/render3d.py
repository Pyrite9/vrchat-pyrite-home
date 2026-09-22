import numpy as np, math, matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

# ---- load columns ----
V=[]
for line in open('/home/claude/out/PyriteCliffs_Visual.obj'):
    if line.startswith('v '):
        p=line.split(); V.append((float(p[1]),float(p[2]),float(p[3])))
V=np.array(V); cols=V.reshape(-1,12,3)

RES=513
H=np.fromfile('/home/claude/out/PyriteHome_Terrain.raw',dtype='<u2').reshape(RES,RES)
Yt=-10+H.astype(float)/65535.0*60.0
def terr(x,z):
    i=np.clip(((x+100)/200*(RES-1)).astype(int),0,RES-1)
    j=np.clip(((z+100)/200*(RES-1)).astype(int),0,RES-1)
    return Yt[j,i]

CAM=np.array([0.0,3.2,60.0]); TGT=np.array([0.0,6.0,-40.0]); FOV=62.0
fwd=TGT-CAM; fwd/=np.linalg.norm(fwd)
right=np.cross(fwd,[0,1,0]); right/=np.linalg.norm(right)
up=np.cross(right,fwd)
f=1.0/math.tan(math.radians(FOV)/2)
def proj(P):
    d=P-CAM
    z=d@fwd; x=d@right; y=d@up
    z=np.where(z<0.05,0.05,z)
    return np.stack([f*x/z, f*y/z],-1), z

SUN=np.array([math.sin(math.radians(196))*math.cos(math.radians(9)),
              -math.sin(math.radians(9)),
              math.cos(math.radians(196))*math.cos(math.radians(9))])
SUN/=np.linalg.norm(SUN); L=-SUN

def shade(n, base):
    lam=max(0.0, float(n@L))
    amb=0.30+0.22*max(0.0,n[1])
    warm=np.array([1.00,0.72,0.45]); cool=np.array([0.42,0.52,0.72])
    c=np.array(base)*(amb*cool + 1.25*lam*warm)
    return np.clip(c,0,1)

polys=[];cols_c=[];depth=[]

# water disc
th=np.linspace(0,2*np.pi,96)
wx=0+44*np.cos(th); wz=-12+44*np.sin(th)
P=np.stack([wx,np.zeros_like(wx),wz],-1)
uv,zz=proj(P)
polys.append(uv); cols_c.append((0.13,0.30,0.38)); depth.append(zz.mean()+1e5)

# ground ring (coarse)
for k in range(64):
    a0=2*np.pi*k/64; a1=2*np.pi*(k+1)/64
    for r0,r1 in [(44,60),(60,74),(74,88)]:
        pts=[]
        for a,r in [(a0,r0),(a1,r0),(a1,r1),(a0,r1)]:
            X=r*np.cos(a); Z=-12+r*np.sin(a) if r<50 else r*np.sin(a)
            pts.append([X, float(terr(np.array(X),np.array(Z))), Z])
        P=np.array(pts); uv,zz=proj(P)
        if (zz>0.2).all():
            polys.append(uv); cols_c.append(shade(np.array([0,1,0]),(0.30,0.28,0.26))); depth.append(zz.mean())

BASE=(0.26,0.25,0.24)
for c in cols:
    b=c[:6]; t=c[6:]
    for k in range(6):
        a=k; bb=(k+1)%6
        quad=np.array([b[a],b[bb],t[bb],t[a]])
        e=b[bb]-b[a]; n=np.array([e[2],0,-e[0]]); n/=np.linalg.norm(n)
        mid=quad.mean(0)
        if n@(mid-CAM)>0: continue
        uv,zz=proj(quad)
        if (zz<0.2).any(): continue
        polys.append(uv); cols_c.append(shade(n,BASE)); depth.append(zz.mean())
    uv,zz=proj(t)
    if (zz>0.2).all():
        polys.append(uv); cols_c.append(shade(np.array([0,1,0]),BASE)); depth.append(zz.mean())

order=np.argsort(-np.array(depth))
polys=[polys[i] for i in order]; cols_c=[cols_c[i] for i in order]

fig=plt.figure(figsize=(16,9),facecolor='k')
ax=fig.add_axes([0,0,1,1]); ax.set_facecolor('k')
# sky gradient
grad=np.linspace(0,1,256).reshape(-1,1)
sky=np.zeros((256,1,3)); 
sky[:,0,0]=0.10+0.90*grad[:,0]**2.4; sky[:,0,1]=0.13+0.52*grad[:,0]**2.0; sky[:,0,2]=0.26+0.22*grad[:,0]**1.4
ax.imshow(sky[::-1],extent=[-1.8,1.8,-1.0,1.0],aspect='auto',zorder=0)
ax.add_collection(PolyCollection(polys,facecolors=cols_c,edgecolors='none',zorder=2))
ax.set_xlim(-0.95,0.95); ax.set_ylim(-0.52,0.52); ax.axis('off')
plt.savefig('/home/claude/out/dusk_preview.png',dpi=100,facecolor='k')
print('polys',len(polys))
