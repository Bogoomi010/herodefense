# 이식 계획 — repo_RTD_in_city → TowerDefense (Unity 3D 폴리곤)

> 작성일 2026-09-17. 원본: `D:\Workspace\repo_RTD_in_city` (TypeScript + Phaser 3 + Tauri, 2D 랜덤 캐릭터 디펜스).
> 목표: 원본의 **게임 시스템과 기획을 그대로 계승**하되, **로우폴리 3D 타워 디펜스**로 재구현한다.
> 원본 기획 문서 사본은 [design-origin/](design-origin/) 에 있다 (GDD, 캐릭터 설정집, 아이디어 문서).

---

## 1. 핵심 결정

| 항목 | 원본 | 새 프로젝트 | 비고 |
| --- | --- | --- | --- |
| 엔진 | Phaser 3 (2D 캔버스) | Unity 6000.3.24f1, URP | 환경 구축 완료 |
| 아트 | AI 생성 픽셀 스프라이트 (256px 시트) | **로우폴리 3D**, 플랫 컬러, Blender MCP 생성 | 원본 아트 규격은 폐기, 톤 바이블(코믹 도심)은 유지 |
| 유닛 조작 | RTS 이동 (클릭 선택 · 드래그 박스 · 우클릭 이동) | **타워 배치** — 그리드 칸에 고정, 드래그로 재배치 | 사용자 요청: 랜덤 캐릭터 디펜스 → 타워 디펜스 |
| 카메라 | 고정 2D 1280×720 | 고정 3/4 시점 원근 카메라 (약 50° 하향), 회전 없음 | 트랙 전체가 한 화면에 |
| 밸런스 수치 | `src/data/*.ts` (px 단위) | **수치 그대로 이식**, px → 월드 단위 변환 상수 하나로 처리 | 봇 시뮬레이터 검증 결과(승률 밴드 40~60%)를 그대로 물려받기 위함 |
| 세이브 | Tauri JSON / localStorage | `Application.persistentDataPath` JSON | 구조 동일 |
| 플랫폼 | Windows 데스크톱 | Windows 데스크톱 우선 | 모바일은 범위 밖 |

### 1.1 RTS 이동 제거에 따른 조정

원본은 근접 계열(배달·경찰·철거·교통, 사거리 210~250)을 **걸어서 도로 옆 전열로 옮기는** 포지셔닝이 핵심이었다. 타워 디펜스로 바꾸면 이동이 없으므로 아래처럼 대체한다.

- **배치 = 포지셔닝**: 뽑기 결과는 빈 칸에 자동 배치되지만, 유닛을 드래그해 다른 빈 칸으로 **무료 재배치**할 수 있다 (빈 칸으로 스왑도 허용). 근접 유닛은 외곽 칸, 원거리는 중앙 칸에 두는 퍼즐은 그대로 남는다.
- **이동 중 공격 불가 페널티**는 사라진다. 대신 재배치 직후 짧은 셋업 시간(1초, 공격 불가)을 둔다.
- **스토리 존 파견**: "오두막까지 걸어가기" 대신 유닛 선택 → 패널의 파견 버튼으로 즉시 파견. 파견 유닛은 필드 칸에서 제거되고 오두막 위에 3D로 표시된다.
- **부대 포메이션, 박스 다중 선택, 이동 속도 패시브(`moveSpeedMul`)**는 삭제. 해당 패시브(dv5 등)는 다른 효과로 치환한다 (스킬 이식 단계에서 결정).
- 지상/공중 유닛 타입(README "전투 축 기획", 미구현)은 이동이 없으므로 **배치 가능 칸의 차이**로만 표현한다. 공중 유닛은 도로 위 칸에도 배치 가능. 1차 범위에서는 제외하고 훅만 남긴다.

### 1.2 단위계

- 원본 트랙: 사각 루프 790×530 px, 유닛 그리드 6×4 칸(칸 74 px), 사거리 210~480 px.
- **1 Unity 단위(m) = 50 px** → 트랙 15.8×10.6 m, 칸 1.48 m, 사거리 4.2~9.6 m.
- 데이터는 px 값을 그대로 보관하고 `WorldScale.PxToWorld = 0.02f` 한 곳에서만 변환한다. 몹 속도(px/s)·스플래시 반경·사거리 모두 동일 규칙.
- 캐릭터 높이 기준 1.2 m (세미치비, 칸 안에 들어가는 크기). 몹 기본 크기 0.8 m, 단계별 ×1.2/1.4, 보스 2.5 m.

---

## 2. 원본 시스템 인벤토리와 이식 판정

원본 코드 6,500줄 중 렌더링·입력(Phaser 의존)은 약 2,500줄, 나머지는 순수 로직·데이터다. 순수 로직은 C#으로 1:1 이식한다.

| 시스템 | 원본 파일 | 판정 | 새 위치 (C#) |
| --- | --- | --- | --- |
| 게임 상수 (라운드 40, 몹 20/라운드, 골드 등) | `data/config.ts` | 그대로 | `Core/GameConfig.cs` |
| 유닛 40종 정의 (등급·계열·수치·태그) | `data/units.ts` | 그대로 | `Core/Data/UnitDefs.cs` |
| 라운드별 뽑기 확률·등급 롤 | `data/units.ts` | 그대로 | `Core/Gacha.cs` |
| 몹 3계열×3단계, 보스 4종, 무한 보스, 난이도 | `data/waves.ts` | 그대로 | `Core/Data/MobDefs.cs`, `Core/MobStats.cs` |
| 3기 조합 220 (규칙 생성·작명·도감 키) | `data/combos.ts` | 그대로 | `Core/Combos.cs` |
| 스킬 148개 (패시브/공격/액티브) | `data/skills.ts` | 그대로, 이동 관련 1~2개 치환 | `Core/Data/SkillDefs.cs` |
| 지원 카드 10종 + `Mods` | `data/cards.ts` | 그대로 | `Core/Cards.cs` |
| 도파민 상수 (스트릭·잭팟·천장·속보·변이·부스터) | `data/events.ts` | 그대로 | `Core/Events.cs` |
| 스토리 존 10장·보상·HP 곡선 | `data/story.ts` | 그대로 | `Core/Story.cs` |
| 미니 주식 3종목 | `data/stocks.ts` | 그대로 (후순위) | `Core/Stocks.cs` |
| 업적 12종 | `data/achievements.ts` | 그대로 | `Core/Achievements.cs` |
| 웨이브 상태 기계 (휴식/진행, 보스 제한시간, 무한) | `systems/WaveSystem.ts` | 그대로 | `Core/WaveSystem.cs` |
| 닫힌 경로 거리→좌표 | `core/path.ts` | 그대로 (3D 좌표) | `Core/PathLoop.cs` |
| 유닛 전투 (타겟팅·쿨다운·패시브 판정·스킬 발동) | `entities/Unit.ts` 전투부 | 그대로, 렌더 분리 | `Core/UnitCombat.cs` |
| 몹 상태 (HP·감속·기절·방깎·마킹·도트) | `entities/Mob.ts` | 그대로, 렌더 분리 | `Core/MobState.cs` |
| 게임 루프 오케스트레이션 (골드·데스·합성·조합·소환권·카드·이벤트) | `scenes/GameScene.ts` 로직부 | 분해해서 이식 | `Core/GameSession.cs` + 하위 서비스 |
| 세이브 (기록·도감·스토리·업적·부스터·설정) | `core/save.ts` | 구조 유지, 저장소 교체 | `Core/SaveData.cs`, `Game/SaveStore.cs` |
| 밸런스 시뮬레이터 (봇 400판) | `scripts/balance-sim.ts` | **이식** — Core가 Unity 비의존이면 EditMode 테스트/에디터 메뉴로 실행 가능 | `Tests/EditMode/BalanceSim.cs` |
| 스킬 정합성 검사 | `scripts/skills-check.ts` | 이식 → EditMode 테스트 | `Tests/EditMode/SkillsCheckTests.cs` |
| RTS 이동·박스 선택·포메이션 | `Unit.ts`, `GameScene.commandMove` | **삭제** (1.1) | — |
| 스프라이트 시트 레지스트리·애니 키 | `data/art.ts`, `BootScene.ts` | **삭제**, 3D 프리팹 레지스트리로 대체 | `Game/UnitVisualRegistry` (SO) |
| DOM HUD·덱 선택·도감·설정 UI | `ui/*.ts` | 재작성 | UI Toolkit |
| 효과음 (WebAudio 합성) | `core/sfx.ts` | 재작성 | AudioSource + 클립 |

---

## 3. Unity 아키텍처

원본이 이미 "데이터/로직 ↔ Phaser 렌더링"으로 나뉘어 있어, 같은 경계를 어셈블리로 고정한다.

```
Assets/_Project/Scripts/
  Core/            TowerDefense.Core.asmdef — UnityEngine 비의존 (순수 C#)
    Data/          UnitDefs, MobDefs, SkillDefs, CardDefs, StoryDefs, EventDefs
    Combos.cs      220 조합 규칙 생성
    Gacha.cs       라운드별 확률·히든 슬롯·잭팟·천장
    WaveSystem.cs  라운드 상태 기계
    UnitCombat.cs  타겟팅·패시브·공격 스킬 (원본 Unit.update)
    MobState.cs    HP·디버프 (원본 Mob)
    GameSession.cs 골드·데스·합성·조합·소환권·카드·이벤트·스토리
    PathLoop.cs
    IRandom.cs     시드 가능한 난수 — 시뮬·테스트 재현용
  Game/            TowerDefense.Game.asmdef — MonoBehaviour
    GameBootstrap.cs   씬 진입, Session 생성, 프레임마다 Session.Tick(dt)
    Field/             TrackBuilder(경로·도로 메시), GridCells(6×4 칸)
    Views/             UnitView, MobView, ProjectileView — Core 상태를 읽어 표시만
    Input/             칸 클릭·드래그 재배치·유닛 선택
    Fx/                피격 플래시, 킬 팝, 등급 컷인
    SaveStore.cs       persistentDataPath JSON
  UI/              TowerDefense.UI.asmdef — UI Toolkit
    Hud, UnitPanel, GachaButton, RecipePanel, CardPick, Dex, Title, DeckSelect, Settings, Stocks
Assets/_Project/Tests/EditMode/  Core 단위 테스트 + BalanceSim + SkillsCheck
```

원칙:

- **Core는 Unity를 모른다.** `Vector`는 자체 `Vec2`(트랙 평면 px 좌표)로 두고 View에서 `Vector3(x, 0, -y) * PxToWorld`로 변환한다. 이 덕분에 봇 시뮬레이터가 Core만 링크해서 초당 수천 판을 돌릴 수 있다.
- **게임 시계는 Session이 소유** (배속·일시정지·낮/밤 3분 주기 전부 원본과 동일하게 `gameTime` 기준).
- **데이터는 C# 정적 테이블로 1:1 이식** (ScriptableObject 아님). 148 스킬·220 조합 같은 규칙형 데이터는 SO로 옮기면 오히려 관리가 어렵고, 원본과 diff 검증도 불가능해진다. 아트 매핑(유닛 id → 프리팹)만 SO로 둔다.
- **View는 Core를 구독만 한다.** Core가 이벤트(`UnitAttacked`, `MobKilled`, `GachaRolled`, `RoundStarted` …)를 발행하고 Game/UI가 연출을 붙인다.

---

## 4. 필드 구성 (3D)

- **트랙**: 원본과 같은 사각 루프 (스폰 = 좌상단 모서리, 시계 방향). 도로 메시 + 모서리 원형 마감. 트랙 바깥은 보도·건물 로우폴리 프롭 (Blender).
- **그리드 6×4**: 트랙 안쪽 광장. 칸마다 얇은 타일 메시, 호버 시 하이라이트, 배치 가능/불가 색.
- **스토리 존 오두막**: 좌하단 안쪽(원본 `STORY_POS` 170,566 px 위치)에 작은 건물. 파견 유닛이 그 위에 서고, 보스 HP 바가 뜬다.
- **낮/밤**: Directional Light 회전 + 스카이 색 보간 + 가로등 점등. 유닛 태그 버프 아이콘은 UI.
- **몹 계열 색**: 돌 회색 / 비둘기 하늘색 / 쓰레기 녹색 / 황금 노랑 — 머티리얼 색으로 계승.

---

## 5. Blender MCP 에셋 파이프라인

### 5.1 전제 조건

- Blender 4.x 실행 + blender-mcp 애드온 켜기 (현재 세션에서는 **연결 안 됨** — 에셋 단계 시작 전 실행 필요).
- Blender 측 단위 = 미터, Unity 임포트 스케일 1. 모델은 원점을 발밑 중앙에 두고 -Y 정면(Blender) → Unity에서 +Z 정면이 되도록 export 옵션 고정.
- 포맷: **glTF(.glb)** 우선 (URP 머티리얼·버텍스 컬러가 깔끔하게 옮겨짐). FBX는 리깅 애니메이션이 필요할 때만.
- 저장 위치: `Assets/_Project/Art/Models/{Units|Mobs|Bosses|Env|Props}/`. 파일명 = 유닛 id (`dv1.glb`, `mob_stone_t1.glb`, `boss_r10.glb`).

### 5.2 스타일 가이드 (로우폴리)

- 캐릭터 300~800 tri, 프롭 50~300 tri, 건물 100~500 tri. 스무스 셰이딩 없이 플랫.
- 텍스처 없이 **단일 팔레트 텍스처(16×16 컬러 아틀라스) 또는 버텍스 컬러**. 등급 색은 원본 HEX 그대로 (흔함 `#9aa0a6` … 초월 `#26c6da`) — 받침대(베이스 링)와 오라 파티클에만 사용, 몸체 색은 캐릭터 고유.
- 세미치비 비율 (머리 1 : 몸 2). 원본 톤 바이블 "우리 동네에 있는 그 사람"을 소품으로 즉시 식별: 배달통, 경찰모, 새총, 드론, 안전모+망치, 집게+빗자루, 전단지, 붕어빵 틀, 뚱뚱한 비둘기, 고등어 냥이.
- 등급이 오를수록 소품 스케일업·장식 추가 (같은 계열은 같은 베이스 바디 재사용).

### 5.3 모듈러 전략 (물량 문제 해결)

유닛 40종 + 조합 결과 220종을 전부 모델링하지 않는다.

- **베이스 바디 3종** (인간형 남/여/동물형) + **소품 세트**를 Blender에서 만들고, Unity에서 `UnitVisualRegistry`(SO)가 유닛 id → {바디, 소품, 팔레트 색}을 조립한다.
- 40종 정식 유닛은 각자 소품 조합을 지정. **조합 결과 220종은 지배 재료의 비주얼 + 등급 받침대 색 + 부재료 소품 1개**로 규칙 생성 (원본 작명 규칙과 같은 사고방식).
- 애니메이션은 1차로 **프로시저럴** (공격 시 소품 스윙/투척 트윈, 대기 시 바운스, 피격 시 스케일 펀치). 리깅 애니는 전설·초월 스킬 연출이 필요해질 때 Blender에서 추가.

### 5.4 제작 순서

1. 환경 키트: 도로 타일 직선/코너, 보도, 건물 5종, 가로등, 오두막, 그리드 타일.
2. 몹 10종: 돌 3단계, 비둘기 3단계, 쓰레기 3단계, 황금 비둘기 (같은 메시에 스케일·색 변주 → 실제 메시는 4개).
3. 보스 4종 + 무한 보스: 덤프트럭, 장갑 수송차, 스텔스 헬기, 시티 브레이커, 야근의 화신.
4. 흔함 10종 (덱 선택 화면에 바로 필요).
5. 안흔함~전설 30종, 초월 6종.
6. 투사체·이펙트 메시 (전단지, 붕어빵, 테이저 궤적, 드론 레이저 등).

각 단계마다 MCP로 생성 → `.glb` export → Unity 임포트 → 프리팹화 → 레지스트리 등록.

---

## 6. 마일스톤

| # | 목표 | 완료 기준 |
| --- | --- | --- |
| M0 | 환경 구축 | ✅ Unity 프로젝트 + git (2026-09-17) |
| M1 | **Core 이식** — 데이터·조합·가챠·웨이브·전투·세션·세이브 구조를 순수 C#으로 | EditMode 테스트 통과, 봇 시뮬 400판이 원본과 같은 승률 밴드(보통 45~57%) 재현 |
| M2 | **3D 필드 프로토타입** — 트랙·그리드·몹 이동·프리미티브(캡슐/큐브) 유닛·뽑기·라운드 진행·골드/데스 | 40라운드 플레이 가능, 보스 4종 등장, 승/패 판정 |
| M3 | **성장 시스템** — 3기 조합 220·동급 3기 합성·전설 2기 초월·소환권·지원 카드·도감·덱 선택·난이도 | 원본 README "조작" 항목 중 이동 관련 제외 전부 동작 |
| M4 | **스킬 148** — 패시브/공격/액티브, 오라, 마킹, 도트, 처형, 팀 버프, 낮/밤 | 스킬 정합성 테스트 통과, 시뮬 밴드 유지 |
| M5 | **Blender 에셋 교체** — 5.4 순서대로 프리미티브를 로우폴리 모델로 | 전 유닛·몹·보스·환경 교체, 프로시저럴 애니 적용 |
| M6 | **메타 시스템** — 스토리 존 10장·도파민(스트릭/잭팟/천장/속보/변이/업적/부스터)·미니 주식·무한 모드 | 판 간 세이브 유지, 결과 화면 하이라이트 |
| M7 | **폴리시** — UI Toolkit 마감, 등급 컷인·킬 팝·보스 연출, 사운드, 설정, Windows 빌드 | `unity build` 로 실행 파일 생성 |

M1과 M2는 병행 가능하다 (Core는 Unity 없이, 필드는 Core 인터페이스만 보고). 에셋(M5)은 Blender가 준비되는 대로 M2 이후 언제든 끼워 넣을 수 있다.

---

## 7. 가정과 미결 사항

- **타워 디펜스 = 유닛 고정 배치 + 드래그 재배치**로 해석했다 (1.1). 이동을 일부 남기고 싶다면 M2 전에 결정 필요.
- **한국어 텍스트 그대로** 이식한다 (원본 결정: 로컬라이징은 글로벌 출시 시점).
- **미니 주식·업적**은 원본에서도 밸런스 영향이 없어 M6 후순위로 두었다.
- 미구현 기획(원본 README "전투 축 기획": 근거리/원거리·지상/공중 타입)은 데이터 필드만 예약하고 1차 범위에서 제외.
- 조합 220종의 비주얼은 규칙 생성(5.3)으로 시작하고, 큐레이션 14종(전단지 폭풍, 붕어빵 푸드트럭 제국 등)만 전용 모델을 나중에 추가.
- Unity 라이브 편집을 위해 `unity pipeline install` (com.unity.pipeline)을 M2 시작 시 추가한다.
