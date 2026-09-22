from PIL import Image, ImageDraw, ImageFilter
import math, numpy as np

S=512; H=S//2
rng=np.random.default_rng(4242)

def petal(dr, cx, cy, ang, L, W, fill, outline=None):
    pts=[]
    for t in np.linspace(0,1,20):
        w=W*math.sin(math.pi*t)**0.75; pts.append((t*L, w))
    for t in np.linspace(1,0,20):
        w=W*math.sin(math.pi*t)**0.75; pts.append((t*L,-w))
    ca,sa=math.cos(ang),math.sin(ang)
    dr.polygon([(cx+x*ca-y*sa, cy+x*sa+y*ca) for x,y in pts], fill=fill, outline=outline)

def bloom(dr, cx, cy, R, base, edge, vein, squash=1.0, rot=0.0):
    """네모필라 한 송이. squash<1 이면 비스듬히 본 모양"""
    for i in range(5):
        a=rot + i*2*math.pi/5
        petal(dr, cx, cy, a, R, R*0.47, base, edge)
    for i in range(5):
        a=rot + i*2*math.pi/5
        for k in (-0.15,0.0,0.15):
            aa=a+k
            dr.line([(cx+R*0.22*math.cos(aa), cy+R*0.22*math.sin(aa)*squash),
                     (cx+R*0.90*math.cos(aa), cy+R*0.90*math.sin(aa)*squash)], fill=vein, width=1)
    r=R*0.30
    dr.ellipse([cx-r,cy-r*squash,cx+r,cy+r*squash], fill=(250,251,248,255), outline=(206,219,236,255))
    r=R*0.085
    dr.ellipse([cx-r,cy-r*squash,cx+r,cy+r*squash], fill=(60,68,98,255))

def leafblade(dr, bx, by, ang, L, W, col):
    petal(dr, bx, by, ang, L, W, col)

PAL=[((156,190,230,255),(126,162,208,255),(112,150,200,150)),   # 옅은 하늘색
     ((128,168,218,255),(100,140,196,255),( 88,126,186,160)),   # 중간 파랑
     ((196,216,240,255),(168,192,224,255),(150,178,214,150)),   # 거의 흰색
     ((110,150,206,255),( 84,122,180,255),( 74,110,172,165))]   # 진한 파랑

def cluster(cell_size, nblooms, rmin, rmax, seed, leafy=0.55, squash=1.0):
    """한 셀 = 꽃 여러 송이 군집. 카드 하나가 카펫 한 뙈기를 표현한다."""
    r=np.random.default_rng(seed)
    lay=Image.new('RGBA',(cell_size,cell_size),(0,0,0,0)); dr=ImageDraw.Draw(lay)
    C=cell_size
    # 잎을 먼저 (뒤에 깔림)
    for _ in range(int(nblooms*leafy*2.2)):
        bx=r.uniform(0.08,0.92)*C; by=r.uniform(0.35,1.02)*C
        a=r.uniform(-2.85,-0.30); L=r.uniform(0.10,0.22)*C; W=r.uniform(0.022,0.045)*C
        g=int(r.uniform(92,142))
        leafblade(dr,bx,by,a,L,W,(int(g*0.52),g,int(g*0.44),255))
    # 꽃
    pts=[]
    for _ in range(nblooms*8):
        if len(pts)>=nblooms: break
        x=r.uniform(0.10,0.90)*C; y=r.uniform(0.12,0.90)*C
        R=r.uniform(rmin,rmax)*C
        if any(math.hypot(x-px,y-py) < (R+pr)*0.62 for px,py,pr in pts): continue
        pts.append((x,y,R))
    pts.sort(key=lambda p:p[1])
    for x,y,R in pts:
        b,e,v=PAL[int(r.integers(0,4))]
        bloom(dr,x,y,R,b,e,v,squash=squash,rot=r.uniform(0,2*math.pi))
    return lay

img=Image.new('RGBA',(S,S),(0,0,0,0))
# (0,0) 위에서 본 조밀 군집
img.alpha_composite(cluster(H, 9, 0.105, 0.150, 1, leafy=0.5, squash=1.0), (0,0))
# (1,0) 비스듬히 본 군집 (측면 카드용)
img.alpha_composite(cluster(H, 8, 0.110, 0.155, 2, leafy=0.8, squash=0.72), (H,0))
# (0,1) 성긴 군집 + 잎 많음 (가장자리용)
img.alpha_composite(cluster(H, 5, 0.100, 0.140, 3, leafy=1.4, squash=0.85), (0,H))
# (1,1) 봉오리·잎 위주
lay=cluster(H, 3, 0.075, 0.105, 4, leafy=2.2, squash=0.8)
dr=ImageDraw.Draw(lay)
r=np.random.default_rng(55)
for _ in range(7):
    bx=r.uniform(0.15,0.85)*H; by=r.uniform(0.15,0.75)*H; rr=r.uniform(0.018,0.032)*H
    dr.ellipse([bx-rr,by-rr,bx+rr,by+rr], fill=(132,164,212,255), outline=(98,132,186,255))
    dr.ellipse([bx-rr*0.4,by-rr*0.4,bx+rr*0.4,by+rr*0.4], fill=(178,202,236,255))
img.alpha_composite(lay,(H,H))

img.save('/mnt/user-data/outputs/flora/T_Nemophila_Atlas.png')
a=np.array(img)
for r_ in range(2):
    for c in range(2):
        cell=a[r_*H:(r_+1)*H, c*H:(c+1)*H, 3]
        print('cell(%d,%d) 알파>127 비율 %.3f'%(c,r_,(cell>127).mean()))
