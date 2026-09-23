# 골드 시스템 설계

> 원본 `design-origin/GDD.md` 파트 4(경제)·파트 6(도파민)와 `GameScene.ts`의 골드 처리를 기준으로 한다.
> 구현: `Assets/_Project/Scripts/Core/Economy.cs` (순수 C#, Unity 비의존) · 테스트: `Assets/_Project/Tests/EditMode/EconomyTests.cs`.
> 수치 변경은 이 문서가 아니라 Core 코드의 상수를 고치고 시뮬레이터로 재검증한다.

> **2026-09-23 기획 변경 (이 줄이 아래 본문보다 우선):**
> - 골드는 스테이지마다 초기화된다.
> - 지출처는 **포탑 설치**([TOWER_PLACEMENT.md](TOWER_PLACEMENT.md))와 **포탑 업그레이드**([TOWER_UPGRADE.md](TOWER_UPGRADE.md)) 두 가지다.
> - 시작 골드와 골드 획득량은 [플레이어 스킬](PLAYER_SKILL_TREE.md)로 늘릴 수 있다.
> - 원본의 **뽑기·합성·주식은 없다.** 아래 본문의 해당 항목(소환권, 뽑기 할인, 합성, `StockMarket` 등)은 레거시 기획으로 보류한다. 코드(`Economy`, `StockMarket`)도 당장 지우지 않고 둔다.

## 1. 원칙

1. **골드는 판 안에서만 산다.** 시작 100G, 판이 끝나면 골드·주식 모두 소멸. 판 간 누적 자원은 소환권·도감·스토리 진행뿐이다.
2. **수입은 라운드에 비례해 커진다.** 후반 3기 합성(유닛 3소모)의 연료가 되도록 처치·클리어 골드 모두 라운드 항이 있다.
3. **골드에 닿는 모든 배율은 한 곳(`Economy`)을 지난다.** 스킬·스트릭·변이·카드가 각자 골드를 더하지 않고 `Economy`의 API를 호출한다. 이래야 원장(ledger)으로 수입원별 합계를 검증할 수 있다.
4. **정수 골드.** 배율 계산 후 `Math.Round(반올림, AwayFromZero)` — 원본 JS `Math.round`와 같은 결과.
5. **부족하면 거부, 빚 없음.** `TrySpend`가 false를 돌려주고 잔액은 변하지 않는다.

## 2. 수입 (Sources)

| 출처 | 공식 | `GoldSource` | 비고 |
| --- | --- | --- | --- |
| 몹 처치 | `round(몹골드 × 스킬 killGoldMul × 스트릭 배율 × 변이 배율)` | Kill | 몹골드 = `2 + ⌊r/5⌋` |
| 보스 처치 | 같은 공식, 몹골드 = `20 + r` | BossKill | |
| 황금 비둘기 | 같은 공식, 몹골드 = 일반 ×10 | GoldenKill | 놓쳐도 벌 없음 |
| 분열 자식 | 몹골드 = `max(1, ⌊부모/2⌋)` | Kill | 쓰레기 2·3단계 |
| 라운드 클리어 | `round((10 + 2r) × Π 필드 유닛 roundClearGoldMul)` | RoundClear | tf4 "예산 배정" 패시브 |
| 지원 카드 "도시 예산" | +150 | Card | 5/10/…/35R 선택지 |
| 속보 "붕어빵 트럭" | +60 | CityEvent | 5R+ 비보스 클리어 시 8% |
| 스토리 챕터 보상 | 1장 +100, 4장 +250 | Story | 판 간 진행 |
| 낮 시작 | Σ 필드 유닛 dayStartGold | DayStart | tf5 "지하철 출근" 패시브 |
| 주식 매도 | `현재가 × 수량` | StockSell | |

### 2.1 킬 스트릭 (`Events.StreakGoldMul`)

- 직전 처치로부터 **3초 이내** 처치하면 스트릭 +1, 아니면 1로 리셋.
- **5킬부터** 표시·보너스 시작: 처치 골드 `+1%/킬`, **최대 +10%** (15킬에서 캡).
- 스트릭은 골드 배율에만 영향. 시뮬 기대값 ×1.04.

### 2.2 변이 라운드 (`Economy.RoundGoldMul`)

- 비보스 라운드 시작 시 5%: 황금 라운드(골드 ×2) / 러시 아워(속도 ×1.5 + 골드 ×2).
- 라운드 시작마다 1로 초기화. 처치 골드에만 곱하고 클리어 골드에는 곱하지 않는다 (원본과 동일).

## 3. 지출 (Sinks)

| 항목 | 공식 | `GoldSource` |
| --- | --- | --- |
| 유닛 뽑기 | `max(5, 20 − gachaDiscount)` | Gacha |
| 주식 매수 | `현재가 × 수량` | StockBuy |

`gachaDiscount`(`Mods.GachaDiscount`)는 판 안에서 누적된다: 카드 "뽑기 보조금" +5(중복 선택 가능), 속보 "야시장" +2, 스토리 2장 보상 +2. 바닥 5G.

뽑기 순서(원본): 잔액 검사 → 유닛 롤 → 배치 성공 → **그 뒤에 차감**. 부대가 가득 차면 골드를 쓰지 않는다. 잭팟 무료 뽑기는 차감 없음.

## 4. 주식 (`StockMarket`) — 두 번째 골드 사용처

- 3종목, 시작가 100, 라운드 클리어마다 시세 갱신: `가격 × (1 + 0.5% + 균등(±vol) [+ 급등/급락 ±30%] [+ 연동 +18%] [소프트 평균회귀 ∓5%])`, 바닥 20 / 상한 400.
- 매수는 골드 차감 + 평단 갱신, 매도는 골드 가산. 판 종료 시 잔여 주식 소멸.
- 밸런스: 드리프트 +0.5%/R라 존버 수익은 미미, 유저 엣지 상한 ≈ 판당 수입의 3%. 봇은 미투자.

## 5. 원장 (Ledger)

`Economy`는 `GoldSource`별 누적 수입/지출을 기록하고 `Changed` 이벤트로 변화(출처·증감·잔액)를 알린다.

- HUD: 잔액, 스트릭, 골드 증감 팝업(출처별 색).
- 결과 화면: 수입원 비율(처치/클리어/이벤트/주식), 최대 스트릭.
- 밸런스 시뮬: 원장 합계로 "도파민 경제 ≈ 수입 +10%" 같은 가정을 직접 측정.

## 6. 연동 지점

| 호출자 | API |
| --- | --- |
| 몹 처치 (`EnemySpawner.OnKilled` → 이후 `GameSession`) | `Economy.RewardKill(mob, nowMs, killGoldMul)` |
| 라운드 클리어 (`IWaveCallbacks.RoundClear`) | `Economy.RewardRoundClear(round, roundClearGoldMul)` |
| 라운드 시작 | `Economy.RoundGoldMul = 변이 배율 (기본 1)` |
| 뽑기 | `Economy.GachaCost()` → 배치 성공 후 `Economy.TrySpend(cost, Gacha)` |
| 카드·속보·스토리 | `Economy.Earn(amount, Card/CityEvent/Story)`, 할인은 `Mods.GachaDiscount += n` |
| 낮 시작 | `Economy.RewardDayStart(sum)` |
| 주식 | `StockMarket.Buy/Sell` (내부에서 `Economy` 호출), 클리어마다 `StockMarket.Tick(boostId, rng)` |

## 7. 범위 밖 (후속)

- 뽑기 확률·천장·잭팟은 골드가 아니라 **가챠 시스템**(M3)에서 다룬다. `Events.cs`에 상수만 먼저 둔다.
- 스킬 `killGoldMul`·`roundClearGoldMul`·`dayStartGold` 값은 스킬 이식(M4) 때 채워지고, `Economy`는 배율 인자만 받는다.
- 원본에 없는 "골드 이자" 등 신규 계열(unit-ideas.md 3장)은 도입 시 `GoldSource`를 추가한다.
