# 포탑 디자인 조사

2026-09-27. 포탑 외형(모델·연출) 디자인을 위한 자료 조사. 포탑 **종류·역할** 조사는 [TOWER_TYPES_RESEARCH.md](TOWER_TYPES_RESEARCH.md), 결정은 별도 문서에서 한다.

> 출처 링크가 없는 설명(Kingdom Rush·Defense Grid·Dungeon Defenders의 생김새 등)은 게임을 해 본 일반 지식이다.

## 1. 잘 만든 포탑 디자인의 원칙

| 원칙 | 내용 | 출처·예 |
|---|---|---|
| **실루엣만으로 구분** | 색·디테일을 빼고 윤곽만 봐도 무슨 포탑인지 알아야 한다. 세밀한 표현보다 **굵은 형태**가 유리하다 | "작은 화면에서 실루엣만으로 알아볼 것"(satautiv 아트 방향) |
| **역할이 모양에 드러남** | 사거리 긴 포탑은 높고 가늘게, 광역은 넓고 무겁게, 지원·감속은 낮고 둥글게 | 기존 [TOWER_ART_PROMPTS.md](TOWER_ART_PROMPTS.md) 규칙과 같음 |
| **포탑마다 강조색 하나** | 포탑 종류마다 색을 정해 두고 다른 곳에 쓰지 않는다. 피해 종류(불·얼음·독)에 색을 **예약**한다 | satautiv: 색상 9개를 40° 간격으로 예약하고 테스트로 지킨다 |
| **배경·적과 명도 대비** | 배경은 중간 명도로 차분하게, 적과 포탑은 가장 밝거나 어두운 명도로 | Kingdom Rush: 적에 명도 양 끝을 쓰고 배경은 누른다 (wesplays) |
| **강화가 눈에 보임** | 업그레이드하면 모양이 바뀌어 필드만 봐도 단계를 안다. 너무 미묘하면 플레이어가 구분을 못 한다 | BTD6: 업그레이드마다 모델이 바뀐다(조합 64개). Tower Dominion: 변화가 미묘하다는 불만 |
| **1단계와 최종 단계 모두 알아볼 것** | 강화해도 원래 종류의 정체성(실루엣 핵심)을 유지한다 | satautiv: "T1과 T5 모두에서 읽혀야" |
| **쏘는 게 보여야** | 조준(머리 회전), 발사 반동, 투사체, 명중 효과로 "지금 이 포탑이 저 적을 쏜다"가 보여야 한다 | wesplays: 폭발·효과가 즉시 피드백. Kingdom Rush: 탑 위에 궁수가 서서 쏜다 |
| **세계관과 통일** | 스타일은 배경(로우폴리 초원)과 맞추되 명확성을 해치지 않는다 | wesplays |

## 2. 게임별 포탑 생김새

| 게임 | 시점 | 생김새 | 강화 표현 |
|---|---|---|---|
| **Kingdom Rush** | 탑뷰 2D | 건물(탑) + **위에 사람이 보인다**(궁수 2명, 마법사, 포병). 병영은 문에서 병사가 나온다 | 1→3단계에 건물이 커지고 재질이 좋아진다(나무 → 돌 → 장식). 4단계는 두 갈래로 **완전히 다른 건물** |
| **Bloons TD 6** | 탑뷰 2D | 포탑이 원숭이 캐릭터 | 업그레이드마다 장비가 바뀐다(머리띠 색, 화살통, 날개). 갈래별로 모양이 확실히 다르다 |
| **Defense Grid** | 3D 탑뷰 | 공통 받침 위에 무기 머리. 머리가 적을 향해 돈다 | 단계마다 받침과 머리가 커지고 부품이 늘어난다 |
| **Dungeon Defenders** | 3인칭 | 영웅 키 정도의 작은 포탑. 속성별 색(불 빨강, 번개 파랑, 독 초록) | 등급별 광택·크기 |
| **Rogue Tower** | 3D 로우폴리 | 각 포탑이 단순한 기하 형태. 머리가 회전 | 레벨이 숫자로 표시되고 모양 변화는 작다 |
| **3D 에셋 팩(로우폴리)** | — | **받침(Base) · 몸통(Mount) · 머리(Head)** 3부품 조립, 포신은 반동용으로 따로 | 부품 교체로 변형. 폴리곤 180~560개 수준 |

## 3. 3D로 만들 때의 구조

1. **부품 나누기:** 받침(땅에 붙는 부분) / 몸통(탑) / 머리(회전하는 무기) / 포신·팔(발사 동작). 머리와 포신은 **따로 움직일 수 있게** 나눈다.
2. **조준:** 머리를 표적 방향으로 돌린다(`Quaternion.LookRotation` + 보간). 몸통은 고정.
3. **발사 동작:** 포신 반동(뒤로 밀렸다 돌아옴), 투석기 팔 휘두르기, 발리스타 시위 당기기. 짧은 클립이나 코드로.
4. **강화 부품:** 강화 단계·갈래마다 부품을 **덧붙이는** 방식이 만들기 쉽다(깃발, 쇠 띠, 두 번째 무기, 독 통).
5. **로우폴리:** 플랫 셰이딩·단색, 포탑 하나에 수백 폴리곤이면 충분하다.

## 4. 우리 게임과 연결되는 점

### 지금 상태 (2026-09-27)

| 항목 | 지금 |
|---|---|
| 포탑 종류 | 기본(연사, 업그레이드 3갈래: 공격속도·파워·독) · 투석기(광역 폭발 + 넉백) · 냉기(감속) · 발리스타(저격) — [TOWER_TYPES.md](TOWER_TYPES.md) |
| 모델 | 없음. 종류별 색이 다른 상자 **3 × 11 × 3m**로 대신한다 |
| 크기 | 풍차 크기: 높이 10~12m, 바닥 반지름 1.5m ([SCALE.md](SCALE.md)) |
| 아트 방향 | 중세 판타지 로우폴리, 플랫 셰이딩, 텍스처 없음 (크립·나무와 같은 스타일) |
| 보는 시점 | ① 절벽 위에서 멀리·높이 내려다봄 ② 영웅 3인칭으로 **바로 옆**(영웅 1.8m 대 포탑 11m) |
| 기존 프롬프트 문서 | [TOWER_ART_PROMPTS.md](TOWER_ART_PROMPTS.md)는 원본의 6계열·5등급·높이 3m 기준이라 지금 4종·11m와 맞지 않는다 |

### 조사에서 나온 제안 (결정 아님)

| 포탑 | 실루엣 (역할이 드러나게) | 움직이는 부분 | 강조색 |
|---|---|---|---|
| **기본 (연사)** | 돌·나무 **궁수탑**. 꼭대기 망루에 쇠뇌(또는 궁수) | 망루 위 쇠뇌가 표적을 향해 돌고, 쏠 때 반동 | 빨강 깃발 |
| **투석기 (광역)** | 넓고 묵직한 돌 받침 위 **투석기 팔**. 옆에 돌무더기 | 팔이 뒤로 젖혔다 휘둘러 던짐 | 주황·갈색 |
| **냉기 (감속)** | 둥근 **마법탑**, 꼭대기에 푸른 얼음 수정 | 수정이 천천히 돌고, 쏠 때 번쩍임 | 하늘색(얼음 전용) |
| **발리스타 (저격)** | 가장 **높고 가는** 탑, 꼭대기에 긴 대형 쇠뇌 | 쇠뇌가 돌고, 쏠 때 시위가 튕김 | 짙은 쇠 + 금색 |

- **색 예약:** 초원(라임), 길(황토), 크립(흰·분홍·갈색)과 겹치지 않게 포탑 강조색을 정한다. 얼음 = 하늘색, 독 = 초록처럼 효과색은 그 효과에만 쓴다.
- **기본 포탑 강화 표현:** 갈래마다 덧붙이는 부품을 정한다(예: 공격속도 → 쇠뇌 추가, 파워 → 쇠 띠·큰 활, 독 → 초록 독 통). 단계가 오를수록 부품이 늘어난다.
- **두 시점 모두 고려:** 멀리서는 꼭대기 실루엣과 강조색으로, 가까이(3인칭)서는 받침·문·계단 같은 디테일로 읽히게 한다. 11m 포탑은 3인칭 카메라를 가릴 수 있다([SCALE.md](SCALE.md) 알려진 문제).
- **제작 방식:** 크립처럼 Blender 스크립트로 부품을 만들고(받침 공통 + 종류별 머리), 머리·팔을 따로 움직이게 한다. 발사 동작은 크립 걷기처럼 짧은 클립으로.

## 기본 포탑 모델 (2026-09-27 적용)

- 사용자가 Hitem3D(이미지 → 3D AI)로 만든 석탑 모델을 쓴다: 흰 돌 석탑, 파란 기와 지붕, 나무 기둥 망루, 양옆 파란 깃발, 아치 나무문, 망대 쇠뇌.
- 원본(면 약 199만 개, 4K 텍스처 3장)은 너무 무거워 `Tools/Blender/import_hitem_tower.py`로 가볍게 만든다:
  세우기 → 높이 11m·발밑 원점 → 면 1.2%로 줄이기(약 2.4만 개) → 새 UV → 원본 색을 2048 텍스처에 굽기 → FBX.
- Unity: `TowerDefense/Towers/Build Tower Prefabs`(TowerPrefabBuilder) → `Resources/Towers/TowerBasic`. `Tower.Create`가 종류 이름(`Tower<종류>`)으로 찾고, 없으면 색 상자.
- **쇠뇌 조준:** 원본에서 쇠뇌는 몸통과 떨어진 조각이라 "Head"로 떼어 낸다(`HEAD_Z=8.12`). 흉벽 톱니(위 끝 약 8.0m)와 지붕 처마(9.0m) 사이로 올려 탑 중심에서 돌게 했다. 게임에서는 사거리 안 가장 가까운 크립을 향해 부드럽게 돌고(초당 감쇠 10), 쏠 때 0.35m 뒤로 밀렸다 돌아온다. 화살(빔)은 쇠뇌 끝에서 나간다. 모서리 기둥과는 대각선 방향에서 잠깐 겹치지만 신경 쓰지 않기로 했다(2026-09-27).
- **설치 방향:** 포탑 정면(문·쇠뇌 쪽, Unity +Z)이 가장 가까운 길 지점을 보게 세운다(`Tower.FacePath`).
- 알려진 점: 받침 돌판이 약 5.6 × 6.5m로 바닥 반지름(1.5m)보다 넓어, 길 가까이 세우면 돌판이 길에 걸쳐 보일 수 있다.
- 원본 파일은 저장소에 넣지 않는다(212MB). 다시 만들 때는 원본 obj 경로를 스크립트에 넘긴다.

## 5. 미정

1. 포탑 위에 사람(궁수·포병)을 둘 것인가, 무기만 둘 것인가?
2. 기본 포탑 강화 단계마다 모양을 바꿀 것인가? 새 3종(강화 없음)은 한 모양이면 되는가?
3. 풍차 크기(11m) 그대로인가? 높이를 줄이면 3인칭 가림 문제가 줄어든다.
4. 농장 구역에 맞춰 포탑에 농장 느낌(풍차·곳간 재료)을 섞을 것인가, 순수 중세 성탑으로 갈 것인가?
5. [TOWER_ART_PROMPTS.md](TOWER_ART_PROMPTS.md)를 지금 4종 기준으로 다시 쓸 것인가, 이 문서로 대신할 것인가?

## 출처

- [From Chaos to Clarity: Visual Hierarchy in Tower Defense Design – Wes Plays](https://www.wesplays.com/wes-plays/from-chaos-to-clarity-visual-hierarchy-in-tower-defense-design) — 실루엣·색·명도 대비·효과 피드백, Kingdom Rush 명도 배분
- [art direction v1.0 – satautiv/tower-defence PR #79](https://github.com/satautiv/tower-defence/pull/79) — 실루엣 기준, 색 예약, T1·T5 모두 읽히기, 갈래 구분
- [Tower Defense Design Guide – Design the Game](https://www.designthegame.com/learning/tutorial/tower-defense-design-guide) — 효과·소리 피드백
- [Tower Dominion – Steam 토론](https://steamcommunity.com/app/3226530/discussions/0/732500893149231248) — 강화 외형 변화가 미묘하다는 불만(검색 결과 요약)
- [Upgrade – Blooncyclopedia](https://www.bloonswiki.com/Upgrade), [Dart Monkey (BTD6) – Bloons Wiki](https://bloons.fandom.com/wiki/Dart_Monkey_(BTD6)) — 업그레이드마다 모델 변화(검색 결과 요약)
- [Archer Tower – Kingdom Rush Wiki](https://kingdomrushtd.fandom.com/wiki/Archer_Tower), [Kingdom Rush – Wikipedia](https://en.wikipedia.org/wiki/Kingdom_Rush) — 탑 위 궁수 2명, 3단계 + 4단계 두 갈래(검색 결과 요약)
- [polygon TD turret collection – itch.io](https://trockk.itch.io/polygon-td-turret-collection), [Tower Defense Turrets (low-poly, animated) – CGTrader](https://www.cgtrader.com/3d-models/military/gun/tower-defense-turrets) — 받침·몸통·머리 3부품, 폴리곤 수(검색 결과 요약)
- [Building 3D Tower Defense Game — Turret – Simon Pham (Medium)](https://simonpham.medium.com/building-3d-tower-defense-game-turret-e92960c5642e) — 머리 조준 회전(LookRotation + 보간)(검색 결과 요약)
