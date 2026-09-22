from PIL import Image, ImageDraw
import math, numpy as np

S=256; H=S//2
img=Image.new('RGBA',(S,S),(0,0,0,0))
d=ImageDraw.Draw(img)

def petal(dr, cx, cy, ang, L, W, fill, outline=None):
    pts=[]
    for t in np.linspace(0,1,24):
        w=W*math.sin(math.pi*t)**0.75
        pts.append((t*L, w))
    for t in np.linspace(1,0,24):
        w=W*math.sin(math.pi*t)**0.75
        pts.append((t*L,-w))
    ca,sa=math.cos(ang),math.sin(ang)
    P=[(cx+x*ca-y*sa, cy+x*sa+y*ca) for x,y in pts]
    dr.polygon(P, fill=fill, outline=outline)

def flower(dst, ox, oy, size, base, edge, vein):
    """네모필라: 5장 꽃잎, 흰 중심, 방사 정맥, 짙은 중심점"""
    lay=Image.new('RGBA',(H,H),(0,0,0,0)); dr=ImageDraw.Draw(lay)
    c=H/2; R=size*H/2
    for i in range(5):
        a=-math.pi/2 + i*2*math.pi/5
        petal(dr, c, c, a, R, R*0.46, base, edge)
    # 방사 정맥
    for i in range(5):
        a=-math.pi/2 + i*2*math.pi/5
        for k in (-0.16,0.0,0.16):
            aa=a+k
            dr.line([(c+R*0.22*math.cos(aa), c+R*0.22*math.sin(aa)),
                     (c+R*0.92*math.cos(aa), c+R*0.92*math.sin(aa))], fill=vein, width=1)
    # 흰 중심
    dr.ellipse([c-R*0.30,c-R*0.30,c+R*0.30,c+R*0.30], fill=(252,252,250,255))
    dr.ellipse([c-R*0.30,c-R*0.30,c+R*0.30,c+R*0.30], outline=(210,222,238,255))
    # 중심점 + 수술
    dr.ellipse([c-R*0.085,c-R*0.085,c+R*0.085,c+R*0.085], fill=(58,66,96,255))
    for i in range(5):
        a=i*2*math.pi/5+0.4
        dr.ellipse([c+R*0.17*math.cos(a)-1.6, c+R*0.17*math.sin(a)-1.6,
                    c+R*0.17*math.cos(a)+1.6, c+R*0.17*math.sin(a)+1.6], fill=(236,232,206,255))
    dst.alpha_composite(lay,(ox,oy))

# (0,0) 정면 꽃 — 옅은 하늘색
flower(img, 0, 0, 0.92, (150,186,228,255), (120,158,206,255), (108,146,198,160))
# (1,0) 변주 — 조금 더 진한 파랑 + 작게
flower(img, H, 0, 0.80, (122,164,216,255), ( 96,136,192,255), ( 84,122,182,170))

# (0,1) 잎 덩어리
lay=Image.new('RGBA',(H,H),(0,0,0,0)); dr=ImageDraw.Draw(lay)
rng=np.random.default_rng(7)
for i in range(11):
    a=rng.uniform(-2.9,-0.25); L=rng.uniform(0.30,0.48)*H; W=rng.uniform(0.055,0.10)*H
    g=int(rng.uniform(88,132))
    petal(dr, H*0.5+rng.uniform(-10,10), H*0.95, a, L, W, (int(g*0.55),g,int(g*0.46),255))
# 잎 중앙맥
for i in range(6):
    a=rng.uniform(-2.8,-0.35); L=rng.uniform(0.30,0.46)*H
    dr.line([(H*0.5,H*0.95),(H*0.5+L*math.cos(a), H*0.95+L*math.sin(a))], fill=(66,104,58,200), width=1)
img.alpha_composite(lay,(0,H))

# (1,1) 봉오리 + 작은 잎
lay=Image.new('RGBA',(H,H),(0,0,0,0)); dr=ImageDraw.Draw(lay)
for i in range(7):
    a=rng.uniform(-2.7,-0.45); L=rng.uniform(0.22,0.34)*H; W=rng.uniform(0.05,0.085)*H
    g=int(rng.uniform(96,140))
    petal(dr, H*0.5+rng.uniform(-8,8), H*0.96, a, L, W, (int(g*0.55),g,int(g*0.46),255))
for (bx,by,r) in [(H*0.40,H*0.44,7),(H*0.60,H*0.52,6),(H*0.50,H*0.34,5)]:
    dr.ellipse([bx-r,by-r,bx+r,by+r], fill=(128,158,206,255), outline=(96,128,180,255))
    dr.ellipse([bx-r*0.45,by-r*0.45,bx+r*0.45,by+r*0.45], fill=(170,196,232,255))
img.alpha_composite(lay,(H,H))

img.save('/mnt/user-data/outputs/flora/T_Nemophila_Atlas.png')
print('atlas saved', img.size)
a=np.array(img)
print('알파>127 픽셀 비율 %.3f'%((a[:,:,3]>127).mean()))
