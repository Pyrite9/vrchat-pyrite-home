import numpy as np
RES=513; SIZE=200.0; TY=60.0; TBASE=-10.0
LAKE_C=(0.0,-12.0); LAKE_R=44.0; LAKE_D=6.5

i=np.arange(RES); x=-100.0+SIZE*i/(RES-1)
X,Z=np.meshgrid(x,x,indexing='xy')
def ss(a,b,t):
    t=np.clip((t-a)/(b-a),0,1); return t*t*(3-2*t)

rl=np.hypot(X-LAKE_C[0], Z-LAKE_C[1])
R =np.hypot(X,Z)

# lake: flat bottom to 0.5R, rim reaches y=0 exactly at LAKE_R
depth = LAKE_D*(1.0 - ss(0.5*LAKE_R, LAKE_R, rl))

# beach rises away from the water; noise kept strictly positive
beach = 1.8*ss(LAKE_R, LAKE_R+26, rl)
noise = (0.75*np.sin(X*0.068)*np.cos(Z*0.059)
        +0.45*np.sin(X*0.131+1.2)*np.sin(Z*0.113+0.4)
        +0.25*np.sin(X*0.241+3.1)*np.cos(Z*0.207+2.2))
noise = (noise+1.45)*0.55                      # 0.15 .. 1.45, always positive
land  = ss(LAKE_R-1.0, LAKE_R+12.0, rl)*(1.0-ss(74.0,86.0,R))

cliff_rise = 3.2*ss(72.0,90.0,R)
outer_rise = 9.0*ss(90.0,104.0,R)

Y = -depth + beach + noise*land + cliff_rise + outer_rise
Y = np.clip(Y,-9.5,22.0)

u16=(np.clip((Y-TBASE)/TY,0,1)*65535.0).astype('<u2')
u16.tofile('/home/claude/out/PyriteHome_Terrain.raw')

# shoreline sanity: no land below 0 outside the lake
outside = rl>LAKE_R+2
print('bytes',u16.nbytes,' Ymin %.2f Ymax %.2f'%(Y.min(),Y.max()))
print('lake bottom %.2f'%Y[rl<6].mean())
print('stray puddles outside lake (y<0): %d cells'%int((Y[outside]<0).sum()))
print('shore band y range %.2f..%.2f'%(Y[(rl>LAKE_R+2)&(rl<LAKE_R+20)].min(),Y[(rl>LAKE_R+2)&(rl<LAKE_R+20)].max()))
