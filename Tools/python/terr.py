import math
LAKE_C=(0.0,-12.0); LAKE_R=44.0; LAKE_D=6.5
def ss(a,b,x):
    t=max(0.0,min(1.0,(x-a)/(b-a)))
    return t*t*(3-2*t)
def H(X,Z):
    rl=math.hypot(X-LAKE_C[0], Z-LAKE_C[1]); R=math.hypot(X,Z)
    depth=LAKE_D*(1.0-ss(0.5*LAKE_R,LAKE_R,rl))
    beach=1.8*ss(LAKE_R,LAKE_R+26,rl)
    n=(0.75*math.sin(X*.068)*math.cos(Z*.059)
       +0.45*math.sin(X*.131+1.2)*math.sin(Z*.113+.4)
       +0.25*math.sin(X*.241+3.1)*math.cos(Z*.207+2.2))
    n=(n+1.45)*0.55
    land=ss(LAKE_R-1.0,LAKE_R+12.0,rl)*(1.0-ss(74.0,86.0,R))
    cliff=3.2*ss(72.0,90.0,R)
    outer=9.0*ss(90.0,104.0,R)
    return -depth+beach+n*land+cliff+outer
