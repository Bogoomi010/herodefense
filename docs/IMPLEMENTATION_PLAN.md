# 기초 시스템 구현 계획

2026-09-23 작성. 대상 기획: [TOWER_PLACEMENT](TOWER_PLACEMENT.md) · [TOWER_UPGRADE](TOWER_UPGRADE.md) · [TREE](TREE.md) · [HERO_INTERACTION](HERO_INTERACTION.md) · [HERO_DESCENT](HERO_DESCENT.md) · [PLAYER_SKILL_TREE](PLAYER_SKILL_TREE.md) · [GOLD_SYSTEM](GOLD_SYSTEM.md) 상단 변경분.

## 진행 상황 (2026-09-23)

1~7단계 모두 1차 구현 완료. EditMode 테스트 29개 통과. 플레이 확인은 아직.

계획과 달라진 점:
- `PlayerBonuses`는 `Core/SkillTree.cs` 안에 함께 둔다. 스킬 효과 합산이 트리와 붙어 있어서다.
- 포탑 수치는 ScriptableObject 대신 `TowerPlacer` 인스펙터의 `TowerSpec`에 둔다. 포탑이 1종이라서다. 2종 이상이 되면 에셋으로 뺀다.
- 업그레이드 골드는 시작할 때가 아니라 완료할 때 낸다(시작할 때 잔액 확인). 취소해도 골드를 잃지 않는다.
- 베타 테스트용으로 HUD에 "스테이지 종료" 버튼을 둔다. 40라운드를 다 돌지 않고도 정산과 스킬트리를 확인할 수 있다.

## 원칙

- **판정 로직은 Core(순수 C#)에 둔다.** 설치 판정, 레벨업, 스킬트리 개방·초기화, 보너스 합산이 해당된다. 모두 EditMode 테스트로 검증한다. Core는 `noEngineReferences`라 좌표는 `float x, z`로 받는다.
- **Unity 쪽은 입력·표시·연결만 맡는다.** 대상 폴더는 Game / Hero / UI다.
- **기존 코드를 재사용한다.**
  - `EnemySpawner`가 스테이지 세션 역할을 계속 맡는다.
  - 골드는 `Economy.TrySpend`를 쓴다.
  - 독은 `MobState.ApplyDot`을 쓴다.
  - 경로 위 판정은 `TileMap.WorldToTile`을 쓴다.
- **뽑기·주식 코드는 건드리지 않는다(레거시).** 새 지출처만 `GoldSource`에 추가한다.

## 맵 전제

나무를 "가장 좋은 자리"에 직접 두려면 스테이지마다 맵이 같아야 한다. 맵 생성은 시드 결정적이다. 따라서 **스테이지 = 고정 시드의 `TileMap` + 손으로 배치한 나무**로 충분하고, 새 맵 데이터 형식은 만들지 않는다.

- 위험: `PathGenerator`나 `TerrainMeshBuilder`를 고치면 같은 시드라도 맵이 바뀌어, 나무 배치가 어긋난다. 생성기를 고칠 때는 나무를 다시 배치해야 한다.
- 이 위험이 커지면 맵을 에셋으로 굽는 방식으로 바꾼다(후속).

## 단계

의존 순서대로 진행한다. 각 단계가 끝날 때마다 플레이해서 확인할 수 있다.

### 1단계: 공통 기반

| 할 일 | 위치 |
|---|---|
| `PlayerProfile`: 레벨, 경험치, 스킬 포인트, 찍은 노드 id 목록, 튜토리얼 완료 여부. 경험치 추가 → 레벨업 → 포인트 지급 | `Core/PlayerProfile.cs` |
| `PlayerBonuses`: 찍은 노드들의 효과를 합산한다. 항목은 구역 축소 %, 업그레이드 시간 배율, 강림 쿨타임 배율, 경험치 배율, 시작 골드, 골드 배율 | `Core/PlayerBonuses.cs` |
| 프로필 저장·불러오기: `persistentDataPath/profile.json`, `JsonUtility` 사용 | `Game/ProfileStore.cs` |
| `GoldSource`에 `TowerBuild`, `TowerUpgrade` 추가 | `Core/Economy.cs` |
| 스테이지 시작 시 보너스 적용: 시작 골드, 골드 배율 | `EnemySpawner` |

검증: `PlayerProfileTests` (경험치 → 레벨업 → 포인트 지급).

### 2단계: 포탑 설치

| 할 일 | 위치 |
|---|---|
| `TowerDef` (ScriptableObject): 바닥 반지름, 구역 기준 반지름, 상호작용 반지름, 설치 비용, 공격력·쿨다운·사거리, 방향별 업그레이드 단계표(비용·시간·효과). 베타는 에셋 1개 | `Game/TowerDef.cs`, `ScriptableObjects/Towers/` |
| `PlacementRules.Check`: 골드 → 바닥 전체가 녹색 → 나무와 겹침 → 다른 포탑과 거리 ≥ 2×구역 반지름. 실패 이유를 enum으로 돌려준다. 녹색 판정은 델리게이트로 받는다 | `Core/PlacementRules.cs` |
| 녹색 판정: 바닥 중심과 둘레 12개 샘플점이 모두 `TileType.Ground`인지 확인 | `TileMap.IsGround(Vector3)` |
| `Tower` (MonoBehaviour): `TestTower`를 대체한다. 공격 + 방향별 업그레이드 레벨 보유 | `Game/Tower.cs` |
| `TowerPlacer`: 설치 모드, 커서를 따라가는 미리보기 포탑 + 구역 원(가능 초록 / 불가 빨강), 클릭하면 설치 + 골드 차감 | `Game/TowerPlacer.cs` |
| `TestTower`와 `testTowers` 자동 배치 삭제 | |

검증: `PlacementRulesTests` (규칙마다 통과 1개, 실패 1개. 원끼리 맞닿는 경우는 통과).

### 3단계: 나무

| 할 일 | 위치 |
|---|---|
| `FieldTree` (MonoBehaviour, 나무 프리팹 5종에 붙임): 바닥 반지름, 경험치 보상, 상호작용 반지름, 베기 30초 | `Game/FieldTree.cs` |
| 나무 목록: 스테이지 시작 시 씬에서 수집하고, 벤 나무는 목록에서 뺀다 | `EnemySpawner` |
| `TreeScatter` 삭제. 맵 씬에 나무를 직접 배치하고 저장한다 | `Map/TreeScatter.cs` 삭제 |

### 4단계: 영웅 상호작용과 강림 쿨타임

| 할 일 | 위치 |
|---|---|
| `IHeroInteractable`: 위치, 상호작용 반지름, 작업 시간, 시작 가능 여부, 완료 처리. 구현체는 `Tower`와 `FieldTree` 둘 | `Game/IHeroInteractable.cs` |
| `HeroInteractor`: 범위 안에서 가장 가까운 대상 → 안내 표시 → E 키로 시작 → 진행 막대 → 범위를 벗어나면 취소, 진행도 0 | `Hero/HeroInteractor.cs` |
| 포탑 대상이면 E 키로 3방향 선택 창을 연다. 선택하면 골드를 내고 작업 시작 | `Hero/HeroInteractor.cs` |
| 강림 쿨타임 20초 (귀환 시점부터 계산, `PlayerBonuses` 배율 적용) | `Hero/Hero.cs` |

### 5단계: 포탑 업그레이드 효과

| 방향 | 적용 |
|---|---|
| 공격속도 | 쿨다운 배율 |
| 파워 | 공격력 배율 |
| 독 | 명중 시 `MobState.ApplyDot(dps, 지속시간)` |

- 업그레이드 시간 = 단계표 시간 × `PlayerBonuses` 업그레이드 시간 배율.

### 6단계: 정산과 스킬트리

| 할 일 | 위치 |
|---|---|
| 스테이지 중 얻은 경험치 누적 (나무, × 경험치 배율) | `EnemySpawner` |
| 스테이지 종료 → 정산 화면(얻은 경험치, 레벨업) → 프로필 저장 → 메뉴로 복귀 | `UI/StageResult` |
| `SkillTreeDef` (ScriptableObject): 노드 id, 화면 위치, 효과 종류·값, 이웃 노드, 핵심 노드 여부. 시작 노드 1개. 개발자가 인스펙터에서 편집 | `Game/SkillTreeDef.cs` |
| `SkillTree` 로직: 찍을 수 있는지(열림 = 찍은 이웃 있음, 포인트 1 이상), 찍기, 초기화(무료, 시작 노드만 남음) | `Core/SkillTree.cs` |
| 스킬트리 화면: 메인 메뉴 탭, 튜토리얼 완료 후에만 표시, 끌어서 이동·확대, 노드 3상태 표시, 초기화 버튼 | `UI/SkillTreeView` (UI Toolkit) |

검증: `SkillTreeTests` (잠긴 노드 거부, 개방 연쇄, 초기화 후 포인트 환급).

### 7단계: 인게임 HUD

- 기존 `EnemySpawner.OnGUI`에 추가: 골드, 강림 쿨타임, 설치 버튼, 상호작용 진행 막대.
- ponytail: 베타는 IMGUI로 충분하다. 정식 UI(UI Toolkit)는 레이아웃이 확정되면 만든다.

## 하지 않는 것 (베타 범위 밖)

- 포탑 판매·철거, 포탑 2종 이상, 영웅 스킬트리
- 맵을 에셋으로 굽기, 맵 에디터
- 튜토리얼 내용 (1단계에서는 완료 여부 플래그만 둔다)

## 결정 (2026-09-23)

1. **포탑 설치는 절벽 시점에서만.** 설치 모드에 들어가서 클릭한다. 강림 입력인 Ctrl+클릭과 겹치지 않는다.
2. **튜토리얼 완료는 임시로 "첫 스테이지를 한 번 끝내면"**으로 한다.
3. **업그레이드는 방향당 3단계, 시간은 최소 10초부터 시작**한다 (현재 안: 10 → 15 → 20초).
