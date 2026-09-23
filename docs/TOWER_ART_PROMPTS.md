# 포탑 디자인 프롬프트 — 로우폴리 중세

> 아트 방향: **중세 판타지 로우폴리 포탑**. 원본(`design-origin/`)의 코믹 도심 캐릭터 기획은 따르지 않는다.
> 유지하는 것: 6계열의 **역할**과 등급 5단계(+초월). 등급색은 코드(`GDD 1.2`)와 같다.
> 현재 3D 규격(`Tools/Blender/make_trees.py`, `make_sheep.py`): 플랫 셰이딩, 텍스처 없음, 라임색 나무·흰 양·주황빛 갈색 줄기.
> 프롬프트 본문은 이미지 생성기 호환을 위해 영어, 설명은 한국어.

## 1. 공통 블록 (모든 프롬프트 맨 앞)

```
Low-poly 3D game asset, flat-shaded faceted polygons, visible polygon edges, no textures, solid flat colors.
Medieval fantasy defense tower, stylized and chunky, bright friendly palette that matches a lime-green (#9ED629) low-poly meadow with orange-brown (#CC7833) tree trunks and white sheep.
Small footprint: fits a 1.5 m square stone tile base, no taller than 3 m, silhouette readable from a high 3/4 top-down camera and from a distant cliff.
Single tower only, 3/4 front view, centered, neutral studio light, plain white background, no ground shadow, no floating parts, no text, no logos.
```

네거티브(지원하는 툴에서): `realistic, photo, textures, gritty, dark, ruined, blood, modern, sci-fi, gun, text, watermark, multiple objects, humans`

## 2. 등급 규칙 (공통 블록 뒤에 한 줄)

| 등급 | 색 | 재질·규모 | 추가 문장 |
| --- | --- | --- | --- |
| 흔함 | 회색 `#9aa0a6` | 통나무·거친 목재, 1층 | `Rank 1: rough log and plank construction, rope bindings, gray cloth, small and simple.` |
| 안흔함 | 초록 `#4caf50` | 목재+철 띠, 초록 깃발 | `Rank 2: timber reinforced with iron bands, one green (#4caf50) banner, slightly taller.` |
| 특별함 | 파랑 `#42a5f5` | 다듬은 석재, 파랑 지붕 | `Rank 3: cut stone blocks, blue (#42a5f5) tiled roof and banner, proper fortification.` |
| 희귀함 | 보라 `#ab47bc` | 석재+보라 룬 문양 | `Rank 4: dark stone with purple (#ab47bc) rune carvings, taller spire, ornate details.` |
| 전설 | 주황 `#ffa726` | 금장 장식, 주황 불꽃 | `Rank 5: gold trim, orange (#ffa726) flame braziers, grand castle-tower scale within the tile.` |
| 초월 | 청록 `#26c6da` | 청록 수정 | `Rank 6: floating cyan (#26c6da) crystal fused into the structure, tallest, myth-like.` |

이미지→3D로 뽑을 때는 룬 발광·불꽃·떠 있는 수정을 **조각(기하)으로만** 표현하게 `carved, not glowing; flame as a solid orange shape` 를 덧붙이고, 발광은 Unity 이미시브로 준다.

## 3. 계열별 프롬프트 (공통 블록 + 등급 문장 + 아래 한 줄)

역할이 실루엣에 바로 드러나야 한다. 사거리가 긴 계열일수록 **높고 가늘게**, 근접·지원 계열은 **낮고 넓게**.

### 🏹 궁수탑 — 속사 (원본 배달 계열 역할: 빠른 연타 물리)
- 1 `Wooden archer post: a short log platform with a thatched roof and a single longbow rack.`
- 2 `Timber archer tower with two bow slits and an iron-banded ladder.`
- 3 `Stone archer tower with crenellations and a mounted repeating crossbow.`
- 4 `Twin-crossbow stone turret with purple rune-etched limbs, arrow quivers around the base.`
- 5 `Golden storm ballista battery: three crossbows on a rotating stone mount, orange braziers.`

### 🛡 파수탑 — 밸런스 (원본 경찰 계열 역할: 표준 물리)
- 1 `Wooden watch post with a bell and a spear rack, sandbags of grain around the base.`
- 2 `Timber guard tower with a green shield on the wall and a spiked palisade.`
- 3 `Square stone keep with a blue roof, a portcullis and a sword mounted above the door.`
- 4 `Knight's bastion: dark stone with purple heraldic banners and a mounted lance.`
- 5 `Royal barbican in miniature: gold-trimmed twin towers with an orange flame beacon.`

### 🎯 발리스타 — 장거리 한방 (원본 저격 계열 역할)
- 1 `Crude wooden ballista on a log tripod, a single oversized bolt loaded.`
- 2 `Iron-banded ballista on a raised timber deck, green pennant.`
- 3 `Stone-mounted heavy ballista with a blue-tipped bolt and a winch.`
- 4 `Purple-rune siege ballista with a crystal sight, long thin silhouette.`
- 5 `Gilded dragon-head ballista, orange-hot bolt, the tallest and thinnest tower.`

### 🔮 마법탑 — 마법, 방어 무시 (원본 드론 계열 역할)
- 1 `Hedge-witch hut: crooked wooden shack with a small crystal on a stick.`
- 2 `Wooden wizard tower with a pointed green-shingled roof and a glass orb.`
- 3 `Round stone mage tower with a blue conical roof and a floating orb on top (orb attached by a spire).`
- 4 `Arcane spire with purple rune rings carved around it and a large crystal at the tip.`
- 5 `Grand observatory tower with gold rings and an orange sun-crystal on a spire.`

### 💥 투석기 — 방어력 감소 (원본 철거 계열 역할)
- 1 `Small wooden catapult on a log frame, a pile of rocks beside it.`
- 2 `Iron-reinforced catapult with a green-cloth sling and a rock basket.`
- 3 `Stone-based trebuchet with a blue counterweight box.`
- 4 `Purple-rune trebuchet hurling cracked boulders, runes carved on the arm.`
- 5 `Gold-trimmed bombard cannon on a stone mount, orange fuse glow as a solid shape.`

### ❄ 냉기 제단 — 감속·군중 제어 (원본 교통 계열 역할)
- 1 `Wooden well with a frozen bucket and icicles, low and wide.`
- 2 `Stone-rimmed ice pool with a green-banded wooden frame and small ice spikes.`
- 3 `Blue crystal altar on a stone base with three ice shards.`
- 4 `Purple-rune frost obelisk surrounded by a ring of ice spikes.`
- 5 `Gold-framed frozen fountain with a large ice crystal and orange braziers that never melt it.`

## 4. 사용법

- 컨셉 이미지: 공통 블록 + 등급 문장 + 계열 한 줄. 한 계열 5등급을 한 장에 보려면 끝에 `five towers in a row, same design growing in rank left to right`.
- 이미지→3D(Hi3D 등, 텍스트 입력 없음): 위 프롬프트로 이미지를 먼저 뽑고 그 이미지를 올린다. 발광·연기·떠 있는 부품은 메시가 깨지므로 넣지 않는다. 멀티 뷰는 같은 프롬프트에 `front view` / `side view` / `back view`.
- 텍스트→3D(Hyper3D/Hunyuan): 공통 블록에서 카메라·배경 문장을 빼고 `single object, low poly, flat colors, game ready` + 계열 한 줄.
- 등급색은 지붕·깃발·룬 같은 **작은 면적**에만. 본체는 목재·석재 색을 유지해야 라임색 지형 위에서 등급이 읽힌다.

## 5. 스킬트리 강화 이미지 (기초 이미지 + 참조 생성)

기초 이미지는 Hi3D 텍스트→이미지(Seedream v4.5)로 뽑은 **특별함 궁수탑**. 강화 이미지는 그 이미지를 **참조 이미지로 걸고** 아래 프롬프트로 생성한다. 3장 모두 2026-09-21 생성 확인.

공통 머리말: `Upgrade of the reference tower. Keep the exact same low-poly flat-shaded stone archer tower, same blue tiled roof, same grass tile base, same camera angle and scale.` 꼬리말: `Everything else unchanged.`

| 강화 | 효과 | 프롬프트 본문 |
| --- | --- | --- |
| 1 연발 강화 | 공속 +50% | `Upgrade 1 RAPID FIRE: replace the single crossbow with a triple-barrel repeating crossbow on a rotating iron mount, add a visible crank wheel and gear, add two extra full quivers of bolts on the walls, three bolts flying out in a fan.` |
| 2 독화살 | 독 지속 피해 | `Upgrade 2 POISON ARROWS: the crossbow bolts have glowing toxic green tips, a large iron cauldron of bubbling green poison sits beside the tower with a ladle, green vines and moss creep up the stone walls, the banner turns toxic green, small green skull emblem on the banner.` |
| 3 데미지 증가 | 공속 감소, 피해 대폭 증가 | `Upgrade 3 HEAVY DAMAGE: replace the crossbow with one massive heavy siege ballista with thick iron-banded arms and a single oversized steel-tipped bolt, the stone walls are reinforced with big iron plates and rivets, the tower is slightly bulkier and heavier looking, the banner turns deep red with a bolt emblem.` |

규칙: 강화는 **탑 본체를 바꾸지 않고 무기·소품·깃발색만** 바꾼다. 그래야 기초 탑이 한눈에 같은 탑으로 읽힌다. 다른 계열도 같은 방식(기초 1장 → 참조 걸고 강화 3장).
