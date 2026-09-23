# 부두 판자 톤 변화 — T_DockWood.png → T_DockWood_V.png
#  판자 한 장 = 텍스처 띠 1개(8개). 띠마다 밝기·채도·색 온도를 다르게(바랜 회색 / 짙은 갈색 / 보통),
#  길이(u) 방향으로 저주파 얼룩 ±8%, 판자 끝(u 0 · 0.835 부근 10 cm)은 물 먹은 끝처럼 어둡게.
import numpy as np
from PIL import Image
from scipy.ndimage import gaussian_filter
src = Image.open('Assets/TerrainAssets/T_DockWood.png').convert('RGB')
S = src.size[0]
a = np.asarray(src).astype(np.float64)[::-1] / 255.0      # 행 0 = v 0
lin = a ** 2.2
v = (np.arange(S) + 0.5) / S
u = (np.arange(S) + 0.5) / S
band = np.floor(v * 8).astype(int)
#            밝기   채도   따뜻함(+R -B)
P = np.array([[1.00, 1.00, 0.00],
              [0.62, 1.15, 0.06],    # 짙은 갈색
              [1.18, 0.55, -0.03],   # 바랜 회색
              [0.85, 1.05, 0.03],
              [0.70, 0.90, 0.02],
              [1.10, 0.70, -0.02],
              [0.92, 1.10, 0.05],
              [0.78, 0.80, 0.00]])
b = P[band]
lum = lin.mean(-1, keepdims=True)
out = lum + (lin - lum) * b[:, None, 1:2]                  # 채도
out *= b[:, None, 0:1]                                      # 밝기
out[..., 0] *= 1 + b[:, None, 2]; out[..., 2] *= 1 - b[:, None, 2]
# 길이 방향 얼룩 — 띠마다 다른 난수, u 로 주기(타일링 되게 1 의 배수 주파수)
rng = np.random.default_rng(11)
st = np.zeros((S, S))
for k in range(8):
    ph = rng.uniform(0, 2 * np.pi, 3)
    row = (0.05 * np.sin(2 * np.pi * 2 * u + ph[0]) + 0.03 * np.sin(2 * np.pi * 5 * u + ph[1])
           + 0.02 * np.sin(2 * np.pi * 11 * u + ph[2]))
    st[band == k] = row
out *= (1 + st)[..., None]
# 판자 끝(윗면 u 0 · 0.835) 10 cm = u 0.042 안쪽 물 먹은 끝
du = np.minimum(np.abs(u - 0.0), np.abs(u - 0.835)); du = np.minimum(du, np.abs(u - 1.0))
end = 1 - 0.28 * np.clip(1 - du / 0.042, 0, 1) ** 1.5
out *= end[None, :, None]
out = np.clip(out, 0, 1) ** (1 / 2.2)
Image.fromarray((out[::-1] * 255 + 0.5).astype(np.uint8)).save('Assets/TerrainAssets/T_DockWood_V.png')
print('band mean before/after', [round(float(a[band == k].mean()), 3) for k in range(8)], [round(float(out[band == k].mean()), 3) for k in range(8)])
