import numpy as np, math

A=np.load('/home/claude/cols_full.npy')       # (1350, 12, 3)  아래6 + 위6
bot=A[:,:6,:]; top=A[:,6:,:]
N=len(A)
Y0=-4.0; YCAP=14.0

V=[]; F=[]
def add_prism(xz, y0, y1):
    """xz: (6,2) 육각형. 아래/위 캡 포함 닫힌 기둥."""
    base=len(V)
    for p in xz: V.append((p[0], y0, p[1]))
    for p in xz: V.append((p[0], y1, p[1]))
    for i in range(6):
        j=(i+1)%6
        F.append([base+i, base+j, base+6+j, base+6+i])      # 측면
    F.append([base+6, base+7, base+8, base+9])              # 위 (부채꼴 대신 사각 2장)
    F.append([base+6, base+9, base+10, base+11])
    F.append([base+3, base+2, base+1, base+0])              # 아래
    F.append([base+5, base+4, base+3, base+0])

for i in range(N):
    xz=bot[i][:,[0,2]]
    y1=min(float(top[i][:,1].max()), YCAP)
    add_prism(xz, Y0, y1)

n_col=len(F)

# 바깥 밀봉 링 — 기둥 사이 틈으로 관통하는 것을 막는다
SEG=64; RIN=88.0; ROUT=98.0
for s in range(SEG):
    a0=2*math.pi*s/SEG; a1=2*math.pi*(s+1.15)/SEG      # 15% 겹침
    def pt(a,r): return (r*math.sin(a), r*math.cos(a))
    quad=[pt(a0,RIN), pt(a1,RIN), pt(a1,ROUT), pt(a0,ROUT)]
    base=len(V)
    for p in quad: V.append((p[0], Y0, p[1]))
    for p in quad: V.append((p[0], 16.0, p[1]))
    for i in range(4):
        j=(i+1)%4
        F.append([base+i, base+j, base+4+j, base+4+i])
    F.append([base+4, base+5, base+6, base+7])
    F.append([base+3, base+2, base+1, base+0])

Vn=np.array(V,float)

# ---- 권취 검증: 각 면의 기하 법선이 해당 덩어리 바깥을 향해야 한다 ----
def fix_and_check():
    bad=0; flipped=0
    # 기둥: 덩어리 중심 = 해당 기둥 중심
    idx=0
    for i in range(N):
        cen=np.array([bot[i][:,0].mean(), (Y0+min(float(top[i][:,1].max()),YCAP))*0.5, bot[i][:,2].mean()])
        for k in range(10):     # 기둥당 면 10개
            f=F[idx+k]
            p=[Vn[v] for v in f]
            nrm=np.cross(p[1]-p[0], p[3]-p[0])
            if np.linalg.norm(nrm)<1e-9: continue
            nrm/=np.linalg.norm(nrm)
            out=(sum(p)/4.0)-cen
            if np.linalg.norm(out)<1e-9: continue
            out/=np.linalg.norm(out)
            if float(nrm@out)<0: F[idx+k]=f[::-1]; flipped+=1
        idx+=10
    # 밀봉 링
    for s in range(SEG):
        a=2*math.pi*(s+0.575)/SEG
        cen=np.array([(RIN+ROUT)*0.5*math.sin(a), (Y0+16.0)*0.5, (RIN+ROUT)*0.5*math.cos(a)])
        for k in range(6):
            f=F[idx+k]
            p=[Vn[v] for v in f]
            nrm=np.cross(p[1]-p[0], p[3]-p[0]); nrm/=np.linalg.norm(nrm)
            out=(sum(p)/4.0)-cen; out/=np.linalg.norm(out)
            if float(nrm@out)<0: F[idx+k]=f[::-1]; flipped+=1
        idx+=6
    # 재검증
    idx=0
    for i in range(N):
        cen=np.array([bot[i][:,0].mean(), (Y0+min(float(top[i][:,1].max()),YCAP))*0.5, bot[i][:,2].mean()])
        for k in range(10):
            f=F[idx+k]; p=[Vn[v] for v in f]
            nrm=np.cross(p[1]-p[0], p[3]-p[0])
            if np.linalg.norm(nrm)<1e-9: continue
            nrm/=np.linalg.norm(nrm)
            out=(sum(p)/4.0)-cen; out/=np.linalg.norm(out)
            if float(nrm@out)<0: bad+=1
        idx+=10
    return flipped, bad

flipped,bad=fix_and_check()
print('권취 뒤집은 면 %d개, 남은 오류 %d개'%(flipped,bad))
assert bad==0

L=[]
for v in Vn: L.append('v %.5f %.5f %.5f'%tuple(v))
L.append('o CliffColumnsCollider')
for f in F: L.append('f '+' '.join(str(i+1) for i in f))
open('/mnt/user-data/outputs/PyriteCliffs_Collider.obj','w').write('\n'.join(L)+'\n')

tris=sum((len(f)-2) for f in F)
print('기둥 %d개 + 밀봉링 %d세그  →  정점 %d  면 %d  삼각형 %d'%(N,SEG,len(Vn),len(F),tris))
r=np.hypot(Vn[:,0],Vn[:,2])
print('반경 %.1f ~ %.1f   y %.1f ~ %.1f'%(r.min(),r.max(),Vn[:,1].min(),Vn[:,1].max()))
