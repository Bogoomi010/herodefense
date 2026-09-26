# 인게임 UI 조사

2026-09-26. 인게임 UI(HUD) 기획을 위한 자료 조사. 기획 결정은 별도 문서에서 한다.

> 화면 배치(어디에 무엇이 있는가) 중 출처 링크가 없는 것은 게임을 직접 해 본 일반 지식이다. 기획에 쓰기 전에 스크린샷으로 한 번 더 확인한다.

## 1. 게임별 HUD 구성

| 게임 | 시점 | 화면 배치 | 특징 |
|---|---|---|---|
| **Kingdom Rush** | 탑뷰 2D | 왼쪽 위: 목숨(하트)·골드·웨이브 수. 왼쪽 아래: 영웅 초상화와 스펠 2개. 오른쪽 위: 일시정지·도감 | **UI가 최소한이다.** 필드 밖에 두거나, 고른 대상 옆에 붙인다. 포탑 자리를 누르면 **원형(링) 메뉴**가 자라나듯 펼쳐진다. 업그레이드·판매도 같은 링. 스펠에 단축키가 없어 마우스를 화면 아래까지 옮겨야 하는 게 약점으로 꼽힌다 |
| **Kingdom Rush (웨이브 호출)** | | 적이 나올 입구 가장자리에 **해골 아이콘** | 해골을 누르면 다음 웨이브의 적 종류·수·경로를 보여 준다. 일찍 부르면 남은 초만큼 **골드 보너스와 스펠 쿨타임 단축**. 날개 달린 해골은 비행 적 |
| **Bloons TD 6** | 탑뷰 2D | 위: 목숨·돈·라운드. 오른쪽: 타워 상점 패널(세로 목록). 오른쪽 아래: 시작/배속 버튼. 타워를 고르면 한쪽 패널에 **업그레이드 3갈래** | 정보가 많은 대신 단축키가 촘촘하다. Space = 라운드 시작/배속, Tab = 표적 우선순위 변경, `,` `.` `/` = 업그레이드 1·2·3번 갈래. 키는 전부 바꿀 수 있다 |
| **Defense Grid 2** | 3D 탑뷰 | 포탑 설치 창이 화면의 약 25% | 설치 창에 같은 수치를 그래프와 글로 두 번 보여 줘서 **정보 과다**로 비판받는다. 반대로 **필드 위 UI**(적 경로선, 사거리 원)와 포탑 모양이 바뀌는 업그레이드는 좋은 예로 꼽힌다 |
| **Arknights** | 쿼터뷰 모바일 | 위: 적 수(처치·통과/전체), 목숨, 배속(1×/2×), 일시정지. 오른쪽 아래: 배치 포인트(DP). 아래: **배치 대기 줄**(유닛 카드 + 비용) | 카드를 끌어 칸에 놓고 방향을 정한다. 유닛을 고르면 시간이 느려져 차분히 고를 수 있다. DP는 초당 1씩 찬다 |
| **Dungeon Defenders 2** | 3인칭 + 건설 모드 | 왼쪽 위: 영웅 초상화·레벨·체력, 파티원. 오른쪽 위: 웨이브 수, 웨이브 적 수, 방어물 한도, 미니맵. 아래: 마나, 영웅 스킬, 설치할 방어물 단축 줄(단축키·쿨타임) | 아이콘 상태를 색으로 구분: **회색 = 못 씀, 빨강 = 마나 부족, 숫자 = 쿨타임 남은 초**. 건설 단계에는 **초록 배너**로 웨이브 번호·적 수·남은 시간을 띄운다. 건설 모드에서 T로 탑뷰 ↔ 어깨 너머 시점을 오간다 |
| **Orcs Must Die** | 3인칭 액션 | 가운데: 조준점. 아래: **함정·주문 단축 줄**(1인 최대 10칸). 체력·리프트 포인트(목숨) 표시 | 웨이브 시작 전 Tab으로 함정을 단축 줄에 넣고 뺀다. 함정은 전투 중에도 설치할 수 있다. 쓰러지면 바로 부활하지만 리프트 포인트를 잃는다 |

## 2. 공통 HUD 요소

거의 모든 타워 디펜스에 있는 것.

| 요소 | 보여 주는 것 | 흔한 위치 |
|---|---|---|
| **자원** | 골드(돈·DP·마나) | 위 왼쪽 또는 위 가운데 |
| **목숨** | 남은 목숨(하트·기지 체력) | 자원 옆 |
| **웨이브** | 현재 / 전체 웨이브, 남은 적 수 | 위 |
| **다음 웨이브** | 다음 웨이브의 적 종류·수·입구, 시작 버튼, 남은 시간 | 입구 쪽 필드 위(KR) 또는 오른쪽 아래(BTD) |
| **배속** | 1× / 2× (/ 3×) | HUD 위에 바로. 메뉴 안에 숨기지 않는다 |
| **건설 목록** | 포탑 종류, 비용, 잠김 여부 | 링 메뉴(KR) · 옆 패널(BTD) · 아래 줄(AK, DD, OMD) |
| **선택 패널** | 고른 포탑의 수치, 업그레이드·판매, 표적 우선순위 | 대상 옆 링(KR) 또는 옆 패널(BTD) |
| **영웅·스킬** | 초상화, 체력, 스킬 쿨타임 | 왼쪽 아래 또는 왼쪽 위 |
| **알림** | 새 적 등장 소개, 목숨 잃음, 골드 부족 | 가운데 위 잠깐 |
| **일시정지·설정** | | 오른쪽 위 |

## 3. 조작 패턴

### 3-1. 설치

- **사거리 미리보기.** 설치 전에 커서를 따라다니는 반투명 포탑과 사거리 원을 보여 준다. 놓을 수 있으면 **초록**, 없으면 **빨강**(또는 빨간 X).
- **링 메뉴가 바닥 패널보다 낫다**는 의견이 있다. 맵을 가리지 않기 때문이다. 대신 링은 "정해진 설치 칸"이 있는 게임(KR)에 잘 맞는다. 우리처럼 **자유 설치**면 "종류를 먼저 고르고 놓는" 줄·패널 방식이 흔하다(BTD, AK, OMD).
- 포탑 카드에는 **비용**과 **공격 성격**(광역/감속 등)을 함께 적는다.
- 모바일은 설치 터치 영역 44~48px 이상, 끄는 동안 사거리 원 유지.

### 3-2. 업그레이드·판매

- 수치를 **글과 그래프로 두 번** 보여 주지 않는다(DG2 비판점). 한 가지로.
- 업그레이드 전후 차이를 보여 준다(예: 피해 14 → 18).
- 업그레이드하면 **포탑 모양이 바뀌면** 필드만 봐도 상태를 안다(DG, KR).

### 3-3. 단축키

- 자주 누르는 것은 반드시 키를 준다: 웨이브 시작·배속(Space), 포탑 종류(숫자), 업그레이드 갈래, 스킬.
- KR의 약점(스펠에 키가 없음)이 대표 반례.

### 3-4. 웨이브 흐름

- 다음 웨이브 예고(적 종류·수·입구)를 준다.
- **일찍 부르기 보상**(KR): 위험을 감수하면 골드·쿨타임 이득. 빨리 치우는 플레이에 보상을 준다.
- 자동 다음 웨이브는 토글 하나로.

## 4. 3인칭 + 건설이 섞인 게임에서 배울 점

우리 게임은 **절벽 위 시점(설치)**과 **필드 3인칭(영웅 강림)**이 오간다([HERO_DESCENT.md](HERO_DESCENT.md)). 탑뷰 TD보다 Dungeon Defenders, Orcs Must Die와 가깝다.

| 배울 점 | 게임 | 이유 |
|---|---|---|
| **모드마다 HUD를 바꾼다** | DD2 | 건설 단계는 초록 배너·웨이브 정보, 전투는 체력·스킬. 지금 어느 모드인지 한눈에 안다 |
| **아래 단축 줄 하나** | DD2, OMD | 3인칭에서는 마우스가 조준에 묶이므로 버튼 클릭보다 **숫자 키 + 아래 줄**이 맞다. 아이콘 상태(회색·빨강·쿨타임 숫자) 규칙을 함께 쓴다 |
| **가운데는 비운다** | OMD | 조준점과 필드 위 안내(상호작용 키, 진행 막대)만 가운데에 둔다 |
| **시점 전환 키** | DD2 (T) | 우리는 T가 이미 귀환 키다 |
| **쓰러짐·부재 비용** | OMD | 영웅이 필드에 있는 동안 절벽 쪽 조작이 막히는 비용을 HUD로 보여 줘야 한다(아래 5절) |

## 5. 우리 게임과 연결되는 점

### 현재 HUD (2026-09-26)

전부 IMGUI(`OnGUI`)로 그린 임시 UI다.

| 위치 | 내용 | 코드 |
|---|---|---|
| 왼쪽 위 상자 | 스테이지·웨이브, 몹·처치·진입·데스·골드, 레벨·경과시간·별 기준·배속, 메시지 | `Game/EnemySpawner.cs` `OnGUI` |
| 같은 상자 안 | 설치 모드 버튼, 스테이지 종료, 포탑 종류 버튼(1~4) | 같은 곳 |
| 가운데 아래 | 영웅 상태, 상호작용 안내, 진행 막대, 업그레이드 방향 선택 창 | `Hero/HeroInteractor.cs` `OnGUI` |
| 가운데 | 정산 창 | `EnemySpawner.cs` |

글자 목록 위주이고, 모드 구분·아이콘·다음 웨이브 예고·강림 쿨타임 표시가 없다.

### 조사에서 나온 제안 (결정 아님)

| 제안 | 근거 | 우리 규칙과의 관계 |
|---|---|---|
| **위 줄에 자원 3개만**: 골드, 데스(목숨), 웨이브 n/15 | 공통 요소, KR의 최소 UI | 처치·진입·몹 수·경과시간은 정산 창이나 펼침 정보로 뺀다 |
| **별 상태 표시**: 별 3개 아이콘, 크립 통과·시간 초과 시 하나씩 꺼짐 | 목숨처럼 "잃고 있는 것"을 보여 준다 | [STAGE.md](STAGE.md) 별 규칙(3에서 깎임)과 그대로 맞는다 |
| **절벽 시점 = 건설 HUD**: 아래에 포탑 종류 줄(1~4, 비용, 잠김 "스테이지 N"), 설치 미리보기 초록/빨강 + 사거리 원 + 걸리는 포탑 구역 강조 | BTD·AK·OMD 줄 방식, 자유 설치 | [TOWER_PLACEMENT.md](TOWER_PLACEMENT.md) 설치 미리보기 규칙이 이미 같다 |
| **필드 시점 = 영웅 HUD**: 포탑 줄을 숨기고 영웅 체력·스킬 줄, 귀환(T) 가능까지 남은 초 | DD2의 모드별 HUD | 강림 중에는 설치를 못 하므로 설치 줄을 보일 이유가 없다 |
| **영웅 강림 버튼 + 쿨타임 원형 게이지**를 한 자리(왼쪽 아래)에 | 스킬 쿨타임 표시 관례(DD2 숫자, 원형 채움) | 강림 쿨타임 20초·귀환 대기 10초([HERO_DESCENT.md](HERO_DESCENT.md)) |
| **필드 위 UI**: 상호작용 안내와 진행 막대는 대상 포탑·나무 머리 위에 | DG의 필드 UI 호평 | [HERO_INTERACTION.md](HERO_INTERACTION.md) 진행 막대 |
| **업그레이드 선택 창**: 방향 3개에 **전후 수치**(예: 피해 14 → 18)와 비용, 숫자 키 1~3 | BTD 업그레이드 단축키, DG 정보 과다 반면교사 | [TOWER_UPGRADE.md](TOWER_UPGRADE.md) 3방향 |
| **다음 웨이브 예고**: 스폰 입구 위에 아이콘(보스 웨이브는 다른 모양) | KR 해골 | 15웨이브 보스([STAGE.md](STAGE.md)) |
| **배속 버튼을 HUD에 바로**, Space 단축키 | 공통 권장 | 지금은 배속 값만 글자로 보인다 |
| 새 포탑 해금·새 적 첫 등장 때 **짧은 소개 카드** | KR 새 적 팝업·도감 | 스테이지마다 포탑 하나씩 해금([TOWER_TYPES.md](TOWER_TYPES.md)) |

보류할 것: 미니맵(맵 80×48m로 작다), 일찍 부르기 보상(경제 수치가 먼저 정해져야 한다), 표적 우선순위 변경(포탑마다 표적이 고정돼 있다).

## 6. 미정

- UI 구현 방식: 지금의 IMGUI를 계속 쓸지, uGUI(Canvas)·UI Toolkit으로 옮길지. 메인 메뉴·스테이지 선택은 이미 Canvas다(`UI/MainMenuController.cs`, `UI/StageSelectController.cs`).
- 절벽 시점과 필드 시점에서 HUD를 얼마나 다르게 할지.
- 3인칭 조준점이 필요한지(영웅 공격 방식에 달림).

## 출처

- [User Interface Analysis Of Tower Defence Games – joshbauer94](https://joshbauer94.wordpress.com/2014/11/08/user-interface-analysis-of-tower-defence-games/) — BTD·KR·DG2 UI 비교
- [Engineering the next generation of tower defense games – Game-Ace](https://game-ace.com/blog/engineering-of-tower-defense-games/) — 링 메뉴, 터치 영역, 배속·자동 웨이브
- [Build / upgrade / sell interaction, range preview, undo – GitHub issue](https://github.com/satautiv/tower-defence/issues/17) — 설치·업그레이드 UX 체크리스트
- [Defense Grid: The Awakening – Wikipedia](https://en.wikipedia.org/wiki/Defense_Grid:_The_Awakening) — 설치 미리보기(초록 표시)
- [Kingdom Rush Beginner's Guide – Level Winner](https://www.levelwinner.com/kingdom-rush-beginners-guide-tips-tricks-strategies-to-vanquish-the-evil-forces/) — 해골 아이콘 웨이브 예고
- [Kingdom Rush 100% Achievements Guide – Steam](https://steamcommunity.com/sharedfiles/filedetails/?id=3581574813) — 일찍 부르기 골드·쿨타임 보너스
- [Bloons TD 6 Hotkeys – Magic Game World](https://www.magicgameworld.com/bloons-td-6-hotkeys/), [Hotkey – Bloons Wiki](https://www.bloonswiki.com/Hotkey) — BTD6 단축키
- [Operation – Arknights Terra Wiki](https://arknights.wiki.gg/wiki/Operation), [Deployment Point – Arknights Terra Wiki](https://arknights.wiki.gg/wiki/Deployment_Point) — 전투 화면 요소, DP
- [User Interface – Dungeon Defenders 2 Wiki](https://wiki.dungeondefenders2.com/wiki/User_Interface) — DD2 HUD 배치·아이콘 상태
- [Build phase – Dungeon Defenders Awakened Wiki](https://dungeon-defenders-awakened.fandom.com/wiki/Build_phase), [Controls and Keybindings – DD Wiki](https://dungeondefenders.fandom.com/wiki/Controls_and_Keybindings_for_PC) — 건설 배너, T 시점 전환
- [Orcs Must Die! 2 – Wikipedia](https://en.wikipedia.org/wiki/Orcs_Must_Die!_2), [Orcs Must Die! Deathtrap – Steam](https://steamcommunity.com/app/2273980) — 함정 단축 줄, 리프트 포인트
- [Game UI Database – Orcs Must Die! 3](https://www.gameuidatabase.com/gameData.php?id=1113) — OMD3 화면 모음(스크린샷 확인용)
