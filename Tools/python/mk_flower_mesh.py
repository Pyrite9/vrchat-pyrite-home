import numpy as np, math
rng=np.random.default_rng(1234)

CELL={'f0':(0,0),'f1':(1,0),'leaf':(0,1),'bud':(1,1)}   # (col,row) in 2x2, row0 = top
def uv_cell(name, pad=0.01):
    c,r=CELL[name]
    u0,u1=c*0.5+pad, (c+1)*0.5-pad
    # PNG row0 = top,  OBJ v=0 = bottom  →  뒤집기
    v0,v1=1.0-((r+1)*0.5-pad), 1.0-(r*0.5+pad)
    return u0,v0,u1,v1

def rotm(yaw,pitch):
    cy,sy=math.cos(yaw),math.sin(yaw); cp,sp=math.cos(pitch),math.sin(pitch)
    Ry=np.array([[cy,0,sy],[0,1,0],[-sy,0,cy]])
    Rx=np.array([[1,0,0],[0,cp,-sp],[0,sp,cp]])
    return Ry@Rx

class Mesh:
    def __init__(s): s.v=[]; s.n=[]; s.t=[]; s.f=[]
    def quad(s, corners, uv, nrm, double=True):
        u0,v0,u1,v1=uv
        uvs=[(u0,v0),(u1,v0),(u1,v1),(u0,v1)]
        i0=len(s.v)+1
        for p,uvp in zip(corners,uvs):
            s.v.append(p); s.n.append(nrm); s.t.append(uvp)
        s.f.append([i0,i0+1,i0+2,i0+3])
        if double:
            j0=len(s.v)+1
            for p,uvp in zip(corners,uvs):
                s.v.append(p); s.n.append(nrm); s.t.append(uvp)
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

def flower_quad(m, centre, size, yaw, tilt, cell):
    R=rotm(yaw,tilt)
    h=size*0.5
    loc=[(-h,0,-h),(h,0,-h),(h,0,h),(-h,0,h)]
    pts=[centre+R@np.array(p) for p in loc]
    qn=R@np.array([0.,1.,0.])
    nrm=qn*0.35+np.array([0.,1.,0.]); nrm/=np.linalg.norm(nrm)
    m.quad(pts, uv_cell(cell), nrm)

def leaf_quad(m, base, w, h, yaw, cell):
    R=rotm(yaw,0.0)
    loc=[(-w/2,0,0),(w/2,0,0),(w/2,h,0),(-w/2,h,0)]
    pts=[base+R@np.array(p) for p in loc]
    qn=R@np.array([0.,0.,-1.])
    nrm=qn*0.45+np.array([0.,1.,0.])*0.85; nrm/=np.linalg.norm(nrm)
    m.quad(pts, uv_cell(cell), nrm)

def build(nflower, nleaf, spread, path, name, seed):
    r=np.random.default_rng(seed)
    m=Mesh()
    for i in range(nleaf):
        a=r.uniform(0,2*math.pi); d=r.uniform(0,spread*0.7)
        base=np.array([d*math.cos(a),0.0,d*math.sin(a)])
        leaf_quad(m, base, r.uniform(0.13,0.20), r.uniform(0.10,0.17),
                  r.uniform(0,math.pi), 'leaf' if i%3 else 'bud')
    for i in range(nflower):
        a=r.uniform(0,2*math.pi); d=r.uniform(0,spread)
        c=np.array([d*math.cos(a), r.uniform(0.11,0.21), d*math.sin(a)])
        flower_quad(m, c, r.uniform(0.050,0.072), r.uniform(0,2*math.pi),
                    r.uniform(-0.45,0.45), 'f0' if r.random()<0.6 else 'f1')
    tris=m.write(path,name)
    V=np.array(m.v)
    print('%-22s quads %d  tris %d  w %.2f  h %.2f'%(name,len(m.f),tris,
          max(V[:,0].max()-V[:,0].min(), V[:,2].max()-V[:,2].min()), V[:,1].max()))

build(6, 4, 0.13, '/mnt/user-data/outputs/flora/Nemophila_Clump.obj', 'Nemophila_Clump', 11)
build(3, 2, 0.09, '/mnt/user-data/outputs/flora/Nemophila_Small.obj', 'Nemophila_Small', 22)
