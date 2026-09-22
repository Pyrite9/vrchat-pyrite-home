import numpy as np, math, matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib.collections import PolyCollection

RES=513
h = np.fromfile('/home/claude/out/PyriteHome_Terrain.raw', dtype='<u2').reshape(RES,RES)
Y = -10 + h.astype(float)/65535.0*60.0

# parse columns from obj (top-face y + centers)
cols=[]
cx=cz=None; vs=[]
import re
V=[]
for line in open('/home/claude/out/PyriteCliffs_Visual.obj'):
    if line.startswith('v '):
        p=line.split(); V.append((float(p[1]),float(p[2]),float(p[3])))
for k in range(0,len(V),12):
    grp=V[k:k+12]
    if len(grp)<12: break
    mx=sum(p[0] for p in grp)/12; mz=sum(p[2] for p in grp)/12
    top=sum(p[1] for p in grp[6:])/6
    cols.append((mx,mz,top))

fig,axes=plt.subplots(1,2,figsize=(17,8.2),facecolor='#14161a')

ax=axes[0]
im=ax.imshow(Y, origin='lower', extent=[-100,100,-100,100], cmap='terrain', vmin=-7, vmax=13)
ax.contour(np.linspace(-100,100,RES), np.linspace(-100,100,RES), Y, levels=[0.0], colors='#3fd8e0', linewidths=1.6)
ax.set_title('Terrain heightmap  (cyan = y0 waterline)', color='#e6e6e6')
ax.set_facecolor('#14161a')
for s in ax.spines.values(): s.set_color('#555')
ax.tick_params(colors='#999')
cb=fig.colorbar(im,ax=ax,fraction=0.046); cb.ax.tick_params(colors='#999')
ax.plot(0,58,'o',color='#ffd45e',ms=9); ax.annotate('spawn',(0,58),(6,62),color='#ffd45e')
ax.annotate('low section\n(sunset)',(0,-86),(0,-92),color='#ff9d5c',ha='center')

ax=axes[1]
polys=[]; heights=[]
for mx,mz,top in cols:
    ang=[math.radians(90+60*k) for k in range(6)]
    polys.append([(mx+1.1*math.cos(a), mz+1.1*math.sin(a)) for a in ang]); heights.append(top)
pc=PolyCollection(polys, array=np.array(heights), cmap='copper', edgecolors='none')
ax.add_collection(pc)
ax.imshow(Y, origin='lower', extent=[-100,100,-100,100], cmap='bone', vmin=-8, vmax=16, alpha=0.55)
ax.set_xlim(-100,100); ax.set_ylim(-100,100); ax.set_aspect('equal')
ax.set_title('Columnar basalt ring — %d hex columns, colour = height' % len(cols), color='#e6e6e6')
ax.set_facecolor('#14161a')
for s in ax.spines.values(): s.set_color('#555')
ax.tick_params(colors='#999')
cb=fig.colorbar(pc,ax=ax,fraction=0.046); cb.ax.tick_params(colors='#999')

plt.tight_layout()
plt.savefig('/home/claude/out/layout_preview.png', dpi=110, facecolor='#14161a')
print('columns parsed:', len(cols), ' height range %.1f - %.1f' % (min(heights),max(heights)))
