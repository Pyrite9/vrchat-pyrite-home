# 실행 위치: 프로젝트 루트 (E:\\VRC\\Pyrite\\World). numpy, pillow, scipy 필요
# 부두 판자 노멀맵 + AO — T_DockWood.png(512², 판자 띠 8개, 띠 높이 0.125) 기준
#  메시: 판자 하나 윗면 = u 0..0.835 (2.0 m) × v 띠 하나 안쪽 [k/8+0.004, (k+1)/8-0.004] (0.22 m)
#  → u 1 = 2.40 m, v 띠 안 1 = 0.235 m.  높이장(m) → 탄젠트 공간 노멀 (R=+u, G=+v)
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
S = 1024
alb = Image.open('Assets/TerrainAssets/T_DockWood.png').convert('L').resize((S, S), Image.BICUBIC)
L = np.asarray(alb).astype(np.float64) / 255.0
L = L[::-1]                                   # 행 0 = v 0 (Unity 아래)
U_M, BAND_M = 2.40, 0.235                     # m per u, m per band-t
v = (np.arange(S) + 0.5) / S
t = (v * 8.0) % 1.0                           # 띠 안 위치 0..1
T0, T1 = 0.032, 0.968                         # 판자가 쓰는 범위(가장자리 = 실제 판자 모서리)
tt = np.clip((t - T0) / (T1 - T0), 0, 1)      # 판자 폭 0..1
w_m = tt * (T1 - T0) * BAND_M                 # 판자 폭 방향 위치 (m), 0..0.22
PW = (T1 - T0) * BAND_M
d = np.minimum(w_m, PW - w_m)                 # 가장 가까운 모서리까지 (m)

# 1) 둥근 모서리 반경 12 mm
R = 0.012
x = np.clip(1 - d / R, 0, 1)
h_bevel = -(R - np.sqrt(np.maximum(R * R - (x * R) ** 2, 0)))
# 2) 판자 휨(cupping) — 가운데가 1.2 mm 낮다
c = (w_m - PW / 2) / (PW / 2)
h_cup = 0.0012 * (c * c - 1)
h_row = (h_bevel + h_cup)[:, None] * np.ones((1, S))

# 3) 나뭇결 — 알베도 밝기의 고주파(결은 u 방향으로 길다) → 어두운 결 = 오목
hp = L - gaussian_filter(L, sigma=(4.0, 16.0))
hp = gaussian_filter(hp, sigma=(0.7, 3.0))
h_grain = hp / (np.abs(hp).max() + 1e-9) * 0.0006
# 4) 판자마다 아주 약한 비틀림 (띠 번호로 고정 난수)
rng = np.random.default_rng(7)
band = np.floor(v * 8).astype(int)
tw = rng.uniform(-1, 1, 8)[band]
u = (np.arange(S) + 0.5) / S
h_twist = (tw[:, None] * 0.0015) * np.sin(np.pi * u)[None, :] * (c[:, None])

H = h_row + h_grain + h_twist
du = U_M / S                                  # m per px (u)
dv = BAND_M * 8 / S                           # m per px (v)
gy, gx = np.gradient(H, dv, du)               # axis0 = v, axis1 = u
n = np.stack([-gx, -gy, np.ones_like(H)], -1)
n /= np.linalg.norm(n, axis=-1, keepdims=True)
nrm = ((n * 0.5 + 0.5) * 255 + 0.5).astype(np.uint8)[::-1]
Image.fromarray(nrm, 'RGB').save('Assets/TerrainAssets/T_DockWood_N.png')

# AO — 판자 틈 쪽 어둡게(모서리 25 mm 안에서 0.55 까지) + 결 골 살짝
ao_edge = 0.55 + 0.45 * np.clip(d / 0.025, 0, 1) ** 0.6
ao = ao_edge[:, None] * (1 - 0.35 * np.clip(-hp / (np.abs(hp).max() + 1e-9), 0, 1))
ao = np.clip(ao, 0, 1)
Image.fromarray((ao[::-1] * 255 + 0.5).astype(np.uint8), 'L').save('Assets/TerrainAssets/T_DockWood_AO.png')

# 확인용: 그레이징 조명으로 음영만 (알베도 × AO × N·L)
ld = np.array([0.55, 0.35, 0.45]); ld /= np.linalg.norm(ld)
sh = np.clip((n * ld).sum(-1), 0, 1)
prev = (np.asarray(alb.convert('L')).astype(float)[::-1] / 255) * ao * (0.35 + 0.9 * sh)
Image.fromarray((np.clip(prev, 0, 1)[::-1] * 255).astype(np.uint8)).save('Assets/_preview/dock_shade.png')
print('H range mm', H.min() * 1000, H.max() * 1000, 'ao', ao.min(), ao.mean(), 'tilt max deg', np.degrees(np.arccos(n[..., 2].min())))
