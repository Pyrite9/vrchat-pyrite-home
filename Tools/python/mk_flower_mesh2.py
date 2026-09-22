import numpy as np, math

CELL={'dense':(0,0),'side':(1,0),'sparse':(0,1),'bud':(1,1)}
def uv_cell(name, pad=0.006):
    c,r=CELL[name]
    u0,u1=c*0.5+pad,(c+1)*0.5-pad
    v0,v1=1.0-((r+1)*0.5-pad), 1.0-(r*0.5+pad)
    return u0,v0,u1,v1

def rotm(yaw,pitch):
    cy,sy=math.cos(yaw),math.sin(yaw); cp,sp=math.cos(pitch),math.sin(pitch)
    Ry=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])
    Rx=np.array([[1,0,0],[0,cp,-sp],[0,sp,cp]])
    return Ry@Rx

class Mesh:
    def __init__(s): s.v=[];s.n=[];s.t=[];s.f=[]
    def quad(s,c,uv,nrm):
        u0,v0,u1,v1=uv; uvs=[(u0,v0),(u1,v0),(u1,v1),(u0,v1)]
        i0=len(s.v)+1
        for p,q in zip(c,uvs): s.v.append(p); s.n.append(nrm); s.t.append(q)
        s.f.append([i0,i0+1,i0+2,i0+3])
        j0=len(s.v)+1
        for p,q in zip(c,uvs): s.v.append(p); s.n.append(nrm); s.t.append(q)
        s.f.append([j0+3,j0+2,j0+1,j0])
    def write(s,path,name):
        L=[]
        for p in s.v: L.append('v %.5f %.5f %.5f'%tuple(p))
        for p in s.n: L.append('vn %.5f %.5f %.5f'%tuple(p))
        for p in s.t: L.append('vt %.5f %.5f'%tuple(p))
        L.append('o '+name)
        for f in s.f: L.append('f '+' '.join('%d/%d/%d'%(i,i,i) for i in f))
        open(path,'w').write('\n'.join(L)+'\n')
        return len(s.f)*2

def card(m, base, w, h, yaw, tilt, cell):
    """바닥에서 솟아 기울어진 카드. tilt = 수직에서 기운 각(rad)."""
    R=rotm(yaw, tilt)
    loc=[(-w/2,0,0),(w/2,0,0),(w/2,h,0),(-w/2,h,0)]
    pts=[base+R@np.array(p) for p in loc]
    qn=R@np.array([0.,0.,-1.])
    n=qn*0.45+np.array([0.,1.,0.])*0.85; n/=np.linalg.norm(n)
    m.quad(pts, uv_cell(cell), n)

def build(ncard, spread, smin, smax, path, name, seed):
    r=np.random.default_rng(seed)
    m=Mesh()
    for i in range(ncard):
        a=r.uniform(0,2*math.pi); d=spread*math.sqrt(r.uniform(0,1))
        base=np.array([d*math.cos(a), 0.0, d*math.sin(a)])
        w=r.uniform(smin,smax); h=w*r.uniform(0.85,1.05)
        tilt=math.radians(r.uniform(28.0,62.0))      # 수직에서 기움 → 28~62도
        cell='dense' if r.random()<0.55 else ('side' if r.random()<0.7 else 'sparse')
        card(m, base, w, h, r.uniform(0,2*math.pi), tilt, cell)
    tris=m.write(path,name)
    V=np.array(m.v)
    cov=sum(1 for _ in range(1))  # 아래에서 별도 계산
    print('%-20s cards %d  tris %d  지름 %.2fm  높이 %.2fm'%(
        name, ncard, tris, max(V[:,0].max()-V[:,0].min(), V[:,2].max()-V[:,2].min()), V[:,1].max()))
    return tris, V

t1,_=build(12, 0.33, 0.185, 0.300, '/mnt/user-data/outputs/flora/Nemophila_Clump.obj', 'Nemophila_Clump', 77)
t2,_=build( 5, 0.19, 0.160, 0.245, '/mnt/user-data/outputs/flora/Nemophila_Small.obj', 'Nemophila_Small', 88)

# 커버리지 추정: 카드 면적 × cos(기울기) 의 수평 투영 합
def cov(ncard,spread,smin,smax,seed):
    r=np.random.default_rng(seed); s=0
    for i in range(ncard):
        r.uniform(0,2*math.pi); r.uniform(0,1)
        w=r.uniform(smin,smax); h=w*r.uniform(0.85,1.05)
        tilt=math.radians(r.uniform(28.0,62.0))
        s+= w*h*math.sin(tilt)     # 수평면 투영
        r.uniform(0,2*math.pi)
        r.random(); r.random()
    return s
print('수평 투영 커버리지  clump %.3f m²  small %.3f m²'%(cov(12,0.33,0.185,0.300,77), cov(5,0.19,0.160,0.245,88)))
