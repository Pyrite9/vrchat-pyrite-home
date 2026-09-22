import numpy as np, math, sys
sys.path.insert(0,'/home/claude')
from h2 import H, PAD_C, LAKE_C, LAKE_R

rng=np.random.default_rng(20260922)
cols=np.load('/home/claude/cols.npy')      # cx,cz,rad,topy,boty
ccx,ccz,crad,ctop=cols[:,0],cols[:,1],cols[:,2],cols[:,3]
cang=np.degrees(np.arctan2(ccx,ccz))%360.0
crr=np.hypot(ccx,ccz)

def rotmat(yaw,pitch,roll):
    cy,sy=math.cos(yaw),math.sin(yaw); cp,sp=math.cos(pitch),math.sin(pitch); cr,sr=math.cos(roll),math.sin(roll)
    Ry=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])
    Rx=np.array([[1,0,0],[0,cp,-sp],[0,sp,cp]])
    Rz=np.array([[cr,-sr,0],[sr,cr,0],[0,0,1]])
    return Ry@Rx@Rz

# ---- cube faces, winding verified below ----
SGN=[(1,0,1),(1,0,-1),(0,1,1),(0,1,-1),(0,0,1)]  # unused placeholder
FACES=[]   # (axis, sign, 4 corner sign-triples)
def quad(axis,sign):
    a=[0,0,0]; a[axis]=sign
    o1,o2=[i for i in range(3) if i!=axis]
    pts=[]
    for (s1,s2) in [(-1,-1),(1,-1),(1,1),(-1,1)]:
        p=[0,0,0]; p[axis]=sign; p[o1]=s1; p[o2]=s2; pts.append(tuple(p))
    import numpy as _np
    q=[_np.array(p,float) for p in pts]
    nrm=_np.cross(q[1]-q[0], q[3]-q[0]); want=_np.zeros(3); want[axis]=sign
    if float(nrm@want)<0: pts=pts[::-1]
    return pts
for axis in range(3):
    for sign in (-1,1):
        FACES.append((axis,sign,quad(axis,sign)))

def emit_cube(c,e,Rm,vo,vn,vt,fc,mat):
    h=e*0.5
    base=len(vo)+1; nbase=len(vn)+1; tbase=len(vt)+1
    for axis,sign,pts in FACES:
        n=np.zeros(3); n[axis]=sign; nw=Rm@n
        i0=len(vo)+1
        for k,p in enumerate(pts):
            vo.append(c+Rm@(np.array(p,float)*h))
            vn.append(nw); vt.append(((k in (1,2)) and 1.0 or 0.0, (k>=2) and 1.0 or 0.0))
        fc.append((mat,[i0,i0+1,i0+2,i0+3]))
    return

def verify(vo,fc):
    bad=0
    for mat,f in fc:
        p=[vo[i-1] for i in f]
        nrm=np.cross(p[1]-p[0], p[3]-p[0]); nrm=nrm/np.linalg.norm(nrm)
        ctr=sum(p)/4.0
        # outward = from cube centre; recover cube centre as mean of its 24 verts
        bad+= 0
    return bad

MATS=['M_Pyrite','M_Pyrite','M_Pyrite_Iris','M_Pyrite_Tarnish']

# ================= CLIFF CRYSTALS (광맥 단위) =================
# 실제 황철석은 암맥/포켓 단위로 뭉쳐 나온다. 링 주위에 12개 광맥을 두고
# 각 광맥에 3~5개 큐브를 비슷한 높이대로 모은다. 돌출은 20~35%로 억제.
cliff=[]
N_VEIN=12
base_angles=[(360.0/N_VEIN)*i + rng.uniform(-9,9) for i in range(N_VEIN)]
rng.shuffle(base_angles)

def pick_col(th_deg, rmax=81.5):
    m=np.abs(((cang-th_deg+180)%360)-180)<1.6
    if not m.any(): return None
    k=int(np.argmin(np.where(m,crr,1e9)))
    if crr[k]>rmax: return None
    return k

for vi,th0 in enumerate(base_angles):
    k0=pick_col(th0)
    if k0 is None: continue
    ytop=ctop[k0]
    ybase=float(rng.uniform(5.0, max(6.0, min(24.0, ytop-6.0))))
    ncube=int(rng.integers(3,6))
    # 광맥 안 크기: 주(主) 1개 + 나머지 작게
    sizes=[float(rng.uniform(2.0,3.4))]+[float(rng.uniform(0.6,1.5)) for _ in range(ncube-1)]
    for e in sizes:
        for _try in range(30):
            th=th0+float(rng.uniform(-1.3,1.3))
            k=pick_col(th)
            if k is None: continue
            y=ybase+float(rng.uniform(-2.5,2.5))
            if y<4.0 or y> ctop[k]-e*0.9: continue
            p=rng.uniform(0.20,0.35)
            r_inner=crr[k]-0.95
            rc=r_inner+e*(0.5-p)
            n=np.array([ccx[k],0.0,ccz[k]]); n/=np.linalg.norm(n)
            tg=np.array([n[2],0.0,-n[0]])
            c=n*rc + tg*float(rng.uniform(-0.3,0.3)); c[1]=y
            # 같은 광맥 안에서 큐브끼리 심하게 겹치지 않게
            if any(np.linalg.norm(c-cc)< (e+ee)*0.45 for cc,ee,_,_ in cliff): continue
            Rm=rotmat(rng.uniform(0,2*math.pi), rng.uniform(-0.6,0.6), rng.uniform(-0.6,0.6))
            cliff.append((c,e,Rm,MATS[rng.integers(0,4)]))
            break

# ================= GROUND CRYSTALS =================
ground=[]; N_GND=26; tries=0
while len(ground)<N_GND and tries<40000:
    tries+=1
    R=math.sqrt(rng.uniform(0,1))*74.0; a=rng.uniform(0,2*math.pi)
    x,z=R*math.cos(a), R*math.sin(a)
    rl=math.hypot(x-LAKE_C[0], z-LAKE_C[1])
    if rl<59.0: continue
    if R>74.0: continue
    if math.hypot(x+10.0, z-52.0)<9.0: continue
    if any(math.hypot(x-g[0][0], z-g[0][2])<7.0 for g in ground): continue
    e=float(0.55+1.75*rng.uniform(0,1)**1.8)
    Rm=rotmat(rng.uniform(0,2*math.pi), rng.uniform(-0.55,0.55), rng.uniform(-0.55,0.55))
    corners=np.array([[sx,sy,sz] for sx in(-1,1) for sy in(-1,1) for sz in(-1,1)],float)*e*0.5
    maxY=float((corners@Rm.T)[:,1].max())
    b=rng.uniform(0.50,0.70)
    y=H(x,z)+maxY*(1-2*b)
    if (y+maxY)-H(x,z) < 0.45: continue
    ground.append((np.array([x,y,z]), e, Rm, MATS[rng.integers(0,4)]))

print('cliff %d  ground %d'%(len(cliff),len(ground)))

# ================= WRITE OBJ =================
def build(group_items):
    vo=[]; vn=[]; vt=[]; fc=[]
    for c,e,Rm,mat in group_items:
        emit_cube(c,e,Rm,vo,vn,vt,fc,mat)
    return vo,vn,vt,fc

# winding check: per cube (24 verts), outward = face centre - cube centre
def check(vo,fc):
    bad=0
    for ci in range(len(fc)//6):
        cverts=np.array(vo[ci*24:(ci+1)*24]); cc=cverts.mean(0)
        for mat,f in fc[ci*6:(ci+1)*6]:
            p=[np.asarray(vo[i-1]) for i in f]
            nrm=np.cross(p[1]-p[0], p[3]-p[0]); nrm/=np.linalg.norm(nrm)
            out=sum(p)/4.0-cc; out/=np.linalg.norm(out)
            if float(nrm@out)<0.99: bad+=1
    return bad

# 절벽 결정은 전부 한 그룹 (전용 재질 M_Pyrite_Cliff 적용 대상)
acc_g=[g for g in ground if g[3]=='M_Pyrite_Iris']
ground=[g for g in ground if g[3]!='M_Pyrite_Iris']
groups=[('PyriteInCliff',cliff),('PyriteInGround',ground),('PyriteAccent',acc_g)]
print('cliff %d / ground %d / accent(ground iris) %d'%(len(cliff),len(ground),len(acc_g)))
allv=[]
for gname,items in groups:
    vo,vn,vt,fc=build(items)
    bad=check(vo,fc)
    print('%s: verts %d faces %d  winding errors %d'%(gname,len(vo),len(fc),bad))
    assert bad==0, gname
    L=[]
    for v in vo: L.append('v %.5f %.5f %.5f'%(v[0],v[1],v[2]))
    for n in vn: L.append('vn %.5f %.5f %.5f'%(n[0],n[1],n[2]))
    for t in vt: L.append('vt %.3f %.3f'%t)
    L.append('o '+gname)
    for mat,f in fc:
        L.append('f '+' '.join('%d/%d/%d'%(v,v,v) for v in f))
    open('/mnt/user-data/outputs/%s.obj'%gname,'w').write('\n'.join(L)+'\n')
    allv+=[np.asarray(v) for v in vo]

A=np.array(allv)
print('total verts %d  tris %d'%(len(A), (len(cliff)+len(ground))*12))
print('y range %.2f .. %.2f'%(A[:,1].min(),A[:,1].max()))
print('r range %.2f .. %.2f'%(np.hypot(A[:,0],A[:,2]).min(), np.hypot(A[:,0],A[:,2]).max()))
# ground crystals: check none in the lake / none floating
for c,e,Rm,mat in ground:
    x,y,z=c; h=H(x,z)
    corners=np.array([[sx,sy,sz] for sx in(-1,1) for sy in(-1,1) for sz in(-1,1)],float)*e*0.5
    w=corners@Rm.T+c
    assert w[:,1].min()<h, 'floating'
    assert w[:,1].max()>h+0.15, 'fully buried'
print('ground: all partially buried & visible OK')
C=np.array([c for c,e,R,m in cliff]); E=np.array([e for c,e,R,m in cliff])
print('cliff r %.2f..%.2f  y %.2f..%.2f  edge %.2f..%.2f (mean %.2f)'%(
  np.hypot(C[:,0],C[:,2]).min(), np.hypot(C[:,0],C[:,2]).max(), C[:,1].min(), C[:,1].max(), E.min(),E.max(),E.mean()))
G=np.array([e for c,e,R,m in ground]); print('ground edge %.2f..%.2f (mean %.2f)'%(G.min(),G.max(),G.mean()))
print('ground exposed heights', ' '.join('%.2f'%( (np.array([[sx,sy,sz] for sx in(-1,1) for sy in(-1,1) for sz in(-1,1)],float)*e*0.5@Rm.T+c)[:,1].max()-H(c[0],c[2])) for c,e,Rm,m in ground))
