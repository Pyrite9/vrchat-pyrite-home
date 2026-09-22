# PyriteHome

VRChat 개인 홈 월드 — 주상절리 절벽 · 호수 · 황철석 · 캠프 · 황혼.
Unity 2022.3.22f1 / VRChat SDK Worlds 3.10.5 / UdonSharp.

## 이 저장소에 없는 것

**유료·서드파티 에셋은 재배포 금지라 포함하지 않는다.** 클론한 뒤 Booth 에서 받은 패키지를 다시 임포트해야 한다.
패키지의 GUID 가 같으므로 씬·머티리얼 참조는 임포트만 하면 그대로 이어진다.

| 폴더 | 에셋 |
|---|---|
| `Assets/つきのすとあ/` | FlowersGrassland (つきのすとあ) |
| `Assets/Noagami/` | のあがみ キャンプ＆焚火アセット |
| `Assets/Sakana-Water/` | サカナ VRC向け水面シェーダー |
| `Assets/Sorafield Atmosphere Sky/` · `Sorafield Procedural Skies - VRChat Addon/` | Sorafield Atmosphere Sky |
| `Assets/Flora/Generated/` | 위 꽃 에셋에서 합쳐 만든 파생 메시 |
| `Assets/TerrainAssets/T_grass_dusk.png` · `T_Ground_dusk.png` | 꽃 팩 지면 텍스처를 색만 누른 파생본 |
| `M_LakeWater.mat` · `M_Sky_PyriteDusk.mat` | 위 패키지 폴더 안에서 조정한 머티리얼 — 값은 `PyriteTunedMaterials.cs` 에 있음 |

## 복구 순서

1. VCC 에 저장소 추가: `https://vpm.techanon.dev/index.json` (ProTV), Settings ▸ Packages ▸ **Show Pre-Release Packages** 켜기
2. VCC ▸ Add Existing Project 로 이 폴더를 열면 `Packages/vpm-manifest.json` 기준으로 SDK·ProTV 가 복원된다
3. Unity 를 열고 위 Booth 패키지 4개 임포트
4. `Tools ▸ Pyrite ▸ A2. Rebuild Dusk Ground Textures` — 지면 텍스처 재생성 + 지형 레이어 재연결
   `Tools ▸ Pyrite ▸ A3. Recreate Tuned Materials` — 호수 물·황혼 하늘 머티리얼을 원래 GUID 로 재생성
5. `Tools ▸ Pyrite ▸ I. Build Flower Field Meshes` — 꽃 파생 메시 재생성
6. `Tools ▸ Pyrite ▸ F. Bake Lighting Now` → `T. Bake Reflection Sets`

## 생성 스크립트

`Tools/python/` — 지형 높이맵·스플랫맵·절벽·결정·부두·꽃 밀도·환경음을 만든 파이썬 스크립트. (황철석 노멀맵/MatCap 은 결과 PNG만 `Assets/TerrainAssets`에 보관)
Unity 밖에서 돌려 만든 결과물(`Terrain.raw`, `Splat4.bin`, `Assets/Meshes/*.obj`, `Assets/Audio/*.wav` 등)의 원본이다.
경로가 제작 당시 작업 환경 기준이라 그대로 돌리려면 입출력 경로를 맞춰야 한다.

## 규약

- VRChat 업로드는 항상 **Private**
- 제작 기록은 Claude 프로젝트 문서(`world-pyritehome-*.md`)에 있다
