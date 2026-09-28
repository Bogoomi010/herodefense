# 크립 HP 조절 방식 조사

2026-09-27. 스테이지가 올라갈수록 크립 HP를 어떻게 키우는지 다른 타워 디펜스를 조사한 자료. **결정:** 킹덤러쉬식 스테이지별 크립 풀 + 스테이지 배율 1.15^(n−1) + 새 크립 소개 카드 → [STAGE.md](STAGE.md#크립-구성과-강도).

> 출처 링크가 없는 설명(Kingdom Rush가 스테이지마다 새 적을 내는 방식, PvZ 등)은 게임을 해 본 일반 지식이다.

## 1. 두 가지 큰 방식

| 방식 | 하는 일 | 대표 게임 |
|---|---|---|
| **A. 공식으로 키우기** | 같은 적의 HP를 웨이브·스테이지 번호에 따라 식으로 늘린다 | Bloons TD 6(프리플레이), 무한·로그라이크 TD, 인디 TD 대부분 |
| **B. 적을 바꾸기** | 스테이지마다 HP가 더 높은 **새 종류**, **강화 버전**, **정예·보스**를 넣는다. 같은 적의 HP는 거의 그대로 | Kingdom Rush, Arknights, Plants vs. Zombies, Bloons TD 6(1~80라운드) |

대부분의 캠페인형 게임은 **B를 기본으로 하고, 난이도 선택에만 A(고정 배율)를 쓴다.** 무한 모드나 후반부는 A로 넘어간다.

## 2. 게임별

| 게임 | 스테이지·웨이브에 따라 | 난이도에 따라 |
|---|---|---|
| **Kingdom Rush** | 스테이지마다 정해진 적 구성. 뒤 스테이지일수록 HP·방어력이 높은 **새 종류**가 나온다 | 원작: 캐주얼 **80%**, 노멀 **100%**, 베테랑 **120%** HP. 후속작 불가능 난이도는 적마다 따로 정한 값(예: 40 / 50 / 60 / **80**). 피해·마법 저항·속도가 오르기도 한다 |
| **Bloons TD 6** | 1~80라운드: 라운드마다 **손으로 짠 구성**(더 센 풍선 종류). 81라운드부터 **구간별 증가율**: MOAB급 HP가 라운드당 +2%(81~100) → +5%(101~125) → +15%(126~150) → +35%(151~250) → +100%(251~300)… 모든 풍선 속도도 라운드당 +2% | 난이도별 시작 자원·라운드 수가 다르다 |
| **Arknights** | 스테이지 번호로 스텟을 늘리지 않는다. 적마다 **Level 0(기본) / 1(강화) / 2(고강화)** 버전을 두고, 어려운 스테이지에 강화 버전과 **정예·보스**를 섞는다 | 챌린지 모드: 스테이지마다 적 스텟 **+5~10%** 버프 + 목숨 1 |
| **Rogue Tower** | 웨이브마다 강해진다. 15웨이브 뒤로 홀수 웨이브마다 **HP 2배 미니보스**, 15·25·35·45웨이브에 보스 | — |
| **Plants vs. Zombies** | 좀비 HP는 종류마다 고정. 레벨이 오를수록 **종류와 수**가 늘어난다 | — |
| **인디 TD (YYZ 개발기)** | 웨이브마다 기본 HP **×1.2**(+20%), 10웨이브마다 스폰 수 +1. 처치 골드 = HP^0.9 + 1 | 맵마다 골드 지수(0.8~0.9)를 따로 조정 |

## 3. 공식으로 키울 때의 규칙 (방식 A)

1. **지수로 키운다.** 웨이브당 HP **+8~12%**가 흔하고, 짧은 게임은 +15~20%까지 쓴다. 식은 `HP = 기본 HP × 배율^(웨이브 − 1)`.
2. **후반엔 완만하게.** 지수가 너무 가팔라지면 뒤쪽에서 선형으로 바꾸거나 배율을 낮춘다(예: 25웨이브부터 선형). BTD6은 반대로 후반 구간마다 증가율을 **올려서** 끝없이 버틸 수 없게 만든다.
3. **보상은 HP보다 느리게 늘린다.** 처치 골드를 HP와 같은 비율로 늘리면 플레이어가 곡선을 앞질러 버린다. 제곱근이나 HP^0.8~0.9로 늘린다.
4. **방어력은 HP보다 느리게.** HP 증가율의 약 0.6배. 초반 공격 수단이 후반에 쓸모없어지지 않게.
5. **스폰 수는 HP보다 천천히** 늘린다.
6. **HP 곡선은 포탑 DPS 곡선과 같이 설계한다.** 포탑이 스테이지마다 얼마나 강해지는지(업그레이드·스킬)를 먼저 정하고 HP 곡선을 맞춘다.
7. **수치는 코드 밖에** 둔다(ScriptableObject·표). 시뮬레이터로 검증한다.

## 4. 우리 게임의 지금 방식 (2026-09-27)

| 요소 | 값 | 코드 |
|---|---|---|
| 웨이브 기본 HP | `18 × 1.18^(웨이브 − 1)` — 웨이브 1: 18, 5: 35, 10: 80, 15: **183** (2026-09-28 1.21 → 1.18) | `MobDefs.BaseHp` |
| 크립 종류 배율 | 비둘기 0.7~0.75, 돌멩이 1.0, 쓰레기 1.4~1.6 (웨이브 번호로 계열·단계 결정) | `MobDefs.Families` |
| 스테이지 배율 | 1스테이지 1.0, 2스테이지 1.15, 3스테이지 1.3, 4스테이지(보스) 1.5 | `Resources/Stages.asset` `hpMul` |
| 난이도 배율 | 쉬움 0.85, 보통 1.0, 어려움 1.25 (+ 스폰 수 0.8 / 1.0 / 1.3) | `MobDefs.DifficultyHpMul`, `DifficultyCountMul` |
| 보스 | 웨이브 기본 HP × 7 (2026-09-27 20 → 7, 약 1/3) | `MobDefs.BossDefFor(15)` |
| 처치 골드 | `2 + ⌊웨이브/5⌋` (HP와 무관하게 선형) | `MobDefs.MobStatsFor` |

**조사와 비교한 특징**
- **웨이브 안에서의 곡선은 가파르다.** 웨이브당 +21%로, 조사한 범위(+8~20%)의 위쪽 끝이다. 15웨이브 동안 HP가 약 14배가 된다.
- **스테이지가 바뀌면 웨이브 1(HP 18)부터 다시 시작한다.** 스테이지 사이 차이는 배율 1.0 → 1.5뿐이라, 4스테이지 첫 웨이브(27)는 1스테이지 5웨이브(39)보다 약하다.
- **스테이지 배율은 선형**(+0.15씩)이다. 스테이지가 늘면 플레이어는 스킬트리·해금 포탑으로 계속 강해지므로, 선형 배율은 뒤로 갈수록 상대적으로 쉬워질 수 있다.
- **처치 골드는 선형**이라 HP(지수)보다 훨씬 느리게 늘어난다. 조사의 "보상은 HP보다 느리게" 규칙과 방향은 같지만, 차이가 매우 크다(웨이브 15: HP ×14, 골드 ×2.5).
- 크립 종류는 웨이브 번호로 정해지고 **스테이지와 무관하다.** 모든 스테이지에 같은 순서로 같은 종류가 나온다.

## 5. 우리 게임에 쓸 수 있는 선택지 (결정 아님)

| 선택지 | 방식 | 장점 | 단점 |
|---|---|---|---|
| ① 스테이지 배율을 지수로 | `hpMul = 1.15^(스테이지 − 1)` 같은 식 | 표 한 줄이 식이 되어 스테이지 수가 늘어도 자동. 플레이어 성장을 따라간다 | 같은 크립이 숫자만 커져 새로움이 없다 |
| ② 스테이지마다 시작 웨이브를 밀기 | 스테이지 n의 웨이브 1 = 전체 곡선의 웨이브 `1 + (n − 1) × k` | 원본 40웨이브 곡선을 스테이지들이 나눠 쓴다. 뒤 스테이지 첫 웨이브가 앞 스테이지보다 약해지는 문제가 없다 | k를 잘못 잡으면 뒤 스테이지가 급격히 어려워진다 |
| ③ 스테이지마다 크립 구성을 다르게 (방식 B) | Kingdom Rush처럼 스테이지별로 나오는 종류·정예를 정한다. HP 배율은 작게 | 스테이지마다 새로운 적, 포탑 해금과 짝지을 수 있다(예: 냉기가 풀리는 스테이지에 빠른 크립) | 스테이지마다 구성 데이터를 만들어야 한다 |
| ④ 강화 버전 (Arknights) | 같은 크립의 "강화" 버전(HP·속도↑)을 뒤 스테이지에 섞는다 | 모델 재사용, 단계적 난이도 | ③보다 새로움이 적다 |

- 어느 쪽이든 **플레이어가 스테이지 사이에 얼마나 강해지는가**(스킬트리 노드 수, 포탑 해금, 영웅 성장)를 먼저 정해야 곡선을 맞출 수 있다.
- 처치 골드를 HP와 어떻게 묶을지(선형 유지, HP^0.5~0.9 등)도 함께 정한다.

## 출처

- [Engineering tower defense games: core systems guide – Game-Ace](https://game-ace.com/blog/engineering-of-tower-defense-games/) — 웨이브당 HP +8~12%, 지수 + 선형 바닥, 방어 타입은 정해진 시점에 도입
- [Making a Tower Defense Game Part 3 – YYZ Productions](https://yyz-productions.com/2015/12/01/making-a-tower-defense-game-part-3/) — 웨이브당 ×1.2, 골드 = HP^0.9 + 1, 골드 지수로 난이도 곡선 조절
- [enemy behaviours, wave scaling – satautiv/tower-defence PR #65](https://github.com/satautiv/tower-defence/pull/65) — 방어력 = HP 증가율 × 0.6, 보상은 제곱근
- [Scaling Enemy Difficulty per wave – Construct 포럼](https://www.construct.net/en/forum/construct-2/how-do-i-18/scaling-enemy-difficulty-per-67572) — 웨이브당 배율 예시
- [Dynamic Difficulty Adjustment in Tower Defence (ResearchGate)](https://www.researchgate.net/publication/283161874_Dynamic_Difficulty_Adjustment_in_Tower_Defence) — 적 스텟·골드·스폰 수 3개 배율로 난이도 조절, 이상적인 곡선은 지수
- [Freeplay – Blooncyclopedia](https://www.bloonswiki.com/Freeplay) — BTD6 81라운드 이후 HP·속도 증가 구간표
- [Health Ramping – Bloons Wiki](https://bloons.fandom.com/wiki/Health_Ramping) — MOAB급 HP 증가(검색 결과 요약)
- [Difficulty – Kingdom Rush Wiki](https://kingdomrushtd.fandom.com/wiki/Difficulty) — 원작 난이도별 HP 80 / 100 / 120%(검색 결과 요약)
- [Can we get specifics on the Impossible difficulty? – Kingdom Rush Origins Steam 토론](https://steamcommunity.com/app/816340/discussions/0/1837937637884038634/) — 불가능 난이도 적별 HP·피해·저항 예시
- [Enemy – Arknights Terra Wiki](https://arknights.wiki.gg/wiki/Enemy) — Level 0/1/2 버전, 일반·정예·보스
- [Challenge Mode – Arknights Terra Wiki](https://arknights.wiki.gg/wiki/Challenge_Mode) — 챌린지 모드 스텟 +5~10%(검색 결과 요약)
- [Monsters – Rogue Tower Wiki](https://rogue-tower.fandom.com/wiki/Monsters) — 15웨이브 뒤 HP 2배 미니보스, 보스 웨이브(검색 결과 요약)
