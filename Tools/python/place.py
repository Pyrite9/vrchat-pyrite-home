import numpy as np, math, random
random.seed(4242)
RES=513
H=np.fromfile('/home/claude/out/PyriteHome_Terrain.raw',dtype='<u2').reshape(RES,RES)
Yt=-10+H.astype(float)/65535.0*60.0
def terr(x,z):
    i=int(np.clip((x+100)/200*(RES-1),0,RES-1)); j=int(np.clip((z+100)/200*(RES-1),0,RES-1))
    return float(Yt[j,i])

LAKE=(0.0,-12.0); LR=44.0
SPAWN=(0.0,58.0)
placed=[]
def ok(x,z,minsep):
    if math.hypot(x-LAKE[0],z-LAKE[1]) < LR+5: return False     # not in the lake
    if math.hypot(x,z) > 76: return False                        # not inside the cliffs
    if math.hypot(x-SPAWN[0],z-SPAWN[1]) < 5: return False       # keep spawn clear
    for px,pz,_ in placed:
        if math.hypot(x-px,z-pz) < minsep: return False
    return True

def sample(n, rmin, rmax, smin, smax, around=None, sep=9):
    out=[]
    tries=0
    while len(out)<n and tries<40000:
        tries+=1
        if around:
            a=random.uniform(0,2*math.pi); d=random.uniform(rmin,rmax)
            x=around[0]+d*math.cos(a); z=around[1]+d*math.sin(a)
        else:
            a=random.uniform(0,2*math.pi); d=random.uniform(rmin,rmax)
            x=d*math.cos(a); z=d*math.sin(a)
        if not ok(x,z,sep): continue
        s=random.uniform(smin,smax)
        placed.append((x,z,s)); out.append((x,z,s))
    return out

near  = sample(4,  7, 20, 0.60, 1.00, around=SPAWN, sep=7)
mid   = sample(12, 48, 68, 1.00, 1.60, sep=13)
base  = sample(7,  62, 75, 1.80, 2.40, sep=17)

groups=[('PyriteCluster_A', near+mid[:5]),
        ('PyriteCluster_B', mid[5:]),
        ('PyriteCluster_C', base)]

print('| prefab | # | Position | Rotation | Scale |')
for name,items in groups:
    for k,(x,z,s) in enumerate(items,1):
        y = terr(x,z) - random.uniform(0.25,0.75)*s
        rx=random.uniform(-13,13); ry=random.uniform(0,360); rz=random.uniform(-13,13)
        print('| %s | %02d | (%.1f, %.2f, %.1f) | (%.1f, %.1f, %.1f) | %.2f |'%(name[-1],k,x,y,z,rx,ry,rz,s))
    print('| | | | | |')
print('total', len(placed))
