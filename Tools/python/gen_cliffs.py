import math, random
random.seed(20260922)

r = 1.10                      # hex circumradius
W = math.sqrt(3)*r            # flat-to-flat width
ROW = 1.5*r                   # row spacing
R_IN, R_OUT = 79.0, 87.0
BOTTOM = -9.0
SEGMENTS = 8

def smoothstep(a,b,x):
    t = max(0.0, min(1.0, (x-a)/(b-a)))
    return t*t*(3-2*t)

# pointy-top hexagon corner offsets
corners = [(r*math.cos(math.radians(90+60*k)), r*math.sin(math.radians(90+60*k))) for k in range(6)]

cols = []
imax = int(R_OUT/W)+2
jmax = int(R_OUT/ROW)+2
for j in range(-jmax, jmax+1):
    for i in range(-imax, imax+1):
        cx = W*(i + 0.5*(j & 1))
        cz = ROW*j
        R = math.hypot(cx, cz)
        if not (R_IN <= R <= R_OUT):
            continue
        th = math.degrees(math.atan2(cx, -cz)) % 360   # 0 = -Z ... we want low section at -Z
        # low section centered on -Z (th=0 here)
        d = min(abs(th), 360-abs(th))
        low = 1.0 - 0.60*(1.0 - smoothstep(14, 34, d))
        ang = math.atan2(cz, cx)
        base = 26 + (R-R_IN)/(R_OUT-R_IN)*12
        arc  = 4.0*math.sin(3*ang+0.7) + 3.0*math.sin(7*ang+2.1) + 2.0*math.sin(11*ang+4.4)
        h = (base+arc)*low + random.uniform(-2.0, 2.0)
        h = max(6.5, h)
        tx, tz = random.uniform(-0.09,0.09), random.uniform(-0.09,0.09)
        seg = int((math.degrees(math.atan2(cz,cx)) % 360)/360*SEGMENTS)
        cols.append((seg, cx, cz, h, tx, tz))

cols.sort(key=lambda c: c[0])

V=[]; VT=[]; VN=[]; out=[]
# shared side UVs
VT += [(0,0),(1,0),(1,1),(0,1)]
cur_seg = None
for seg, cx, cz, h, tx, tz in cols:
    if seg != cur_seg:
        out.append("o PyriteCliff_Seg%02d" % (seg+1))
        cur_seg = seg
    b0 = len(V)+1
    for (ox,oz) in corners:
        V.append((cx+ox, BOTTOM, cz+oz))
    for (ox,oz) in corners:
        V.append((cx+ox, h + tx*ox + tz*oz + random.uniform(-0.35,0.35), cz+oz))
    n0 = len(VN)+1
    for k in range(6):
        ax,az = corners[k]; bx,bz = corners[(k+1)%6]
        mx,mz = (ax+bx)/2,(az+bz)/2
        L = math.hypot(mx,mz)
        VN.append((mx/L, 0.0, mz/L))
    VN.append((0.0,1.0,0.0))
    t0 = len(VT)+1
    for (ox,oz) in corners:
        VT.append(((cx+ox)*0.25, (cz+oz)*0.25))
    for k in range(6):
        a=b0+k; b=b0+(k+1)%6; c=b0+6+(k+1)%6; d=b0+6+k
        n=n0+k
        out.append("f %d/1/%d %d/2/%d %d/3/%d %d/4/%d" % (a,n,b,n,c,n,d,n))
    nt=n0+6
    for k in range(1,5):
        out.append("f %d/%d/%d %d/%d/%d %d/%d/%d" % (
            b0+6,   t0,   nt,
            b0+6+k, t0+k, nt,
            b0+6+k+1, t0+k+1, nt))

with open("/home/claude/out/PyriteCliffs_Visual.obj","w") as f:
    f.write("# PyriteHome columnar basalt cliffs\n")
    for v in V: f.write("v %.4f %.4f %.4f\n" % v)
    for t in VT: f.write("vt %.4f %.4f\n" % t)
    for n in VN: f.write("vn %.4f %.4f %.4f\n" % n)
    f.write("\n".join(out)+"\n")

tris = len(cols)*(6*2+4)
print("columns:", len(cols), "tris:", tris, "verts:", len(V))

# ---- low-poly collision wall ----
SIDES=40; RC=79.0; TOP=34.0; BOT=-9.0
V2=[]; F2=[]
for k in range(SIDES):
    a=2*math.pi*k/SIDES
    V2.append((RC*math.cos(a), BOT, RC*math.sin(a)))
    V2.append((RC*math.cos(a), TOP, RC*math.sin(a)))
for k in range(SIDES):
    a=2*k+1; b=2*k+2; c=2*((k+1)%SIDES)+2; d=2*((k+1)%SIDES)+1
    F2.append("f %d %d %d %d" % (a,d,c,b))
with open("/home/claude/out/PyriteCliffs_Collider.obj","w") as f:
    f.write("o PyriteCliff_Collider\n")
    for v in V2: f.write("v %.4f %.4f %.4f\n" % v)
    f.write("\n".join(F2)+"\n")
print("collider tris:", SIDES*2)
