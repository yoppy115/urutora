# Ideas Backlog

ここは正史ではありません。保留、探索中、却下した案を、理由と一緒に失わないための場所です。実装指示として扱わず、採用時に設計文書へ昇格させます。

## On hold

### Settlement / 集落システム

- Status: On hold until after v0.15 Run
- Preserved direction: 個体→集落→社会→国家の基盤とし、強い生存・繁殖・社会利益によって所属が結果的に圧倒的に適応的になる構造を目指す。
- Not specified: 生成、Affinity、所属、各種Bonus、安全圏、帰巣、敵対、Raid、State / Nation。
- Why it is not in v0.15: Targeted Action、繁殖、短寿命、情報容量、HP/Damageの生態系修正を先に再評価するため。
- Future pressure: 高密度が疾病、内部対立、資源、階層、Settlement間競争を生む可能性。
- Related design: [`V0_15_ECOLOGY.md`](../design/V0_15_ECOLOGY.md)

### v0.15 implementation-blocking design decisions

- Status: Unresolved
- Vitality Curveの各Control Point DailyVitalChange値。
- Subject + Propertyが3件を超えた場合のHeld Information Eviction Rule。
- Attack / Communication / ReproductionのTargeted Action内部Resolution順。
- Rule: Codex / Workが独自にBaselineまたはdefaultを設定しない。
- Related design: [`V0_15_ECOLOGY.md`](../design/V0_15_ECOLOGY.md)

### NPCの思考ラグを能力と接続する

- Status: On hold
- Why it is interesting: 情報遅延とは別に、判断頻度の差が逸脱を生む可能性がある。
- Why it is not in the current design: v0では日次ObservationとAction由来Micro Roundを採用し、別の思考頻度能力は入れない。
- Conditions for reconsideration: v0観測で古い情報による逸脱が不足または過剰と分かった時。

## Exploring

### v0後の社会・世界システム

- Status: On hold for v0
- Preserved canon: 国家、宗教、経済、疾病、暴戻、不和、Culture、上位存在の継承、世界フェーズ・再編、詩篇、啓示、動的新概念、LLM Historian。
- Why it is not in v0: 最小NPC相互作用の観測価値を先に検証するため。
- Conditions for reconsideration: headless v0と観測Appで創発的逸脱を評価した後。
- Related design: [`V0_SIMULATION.md`](../design/V0_SIMULATION.md)、[`WORLD_LIFECYCLE.md`](../design/WORLD_LIFECYCLE.md)、[`PSALM_AND_INHERITANCE.md`](../design/PSALM_AND_INHERITANCE.md)

### 上位存在が自然淘汰を歪める範囲

- Status: Exploring
- Adopted subset: ピン止めされた重要人物が、親和性と少量の乱数により加護を得る可能性。
- Still open: 集団全体、出生率、突然変異率、環境へ直接作用するか。
- Related design: [`PSALM_AND_INHERITANCE.md`](../design/PSALM_AND_INHERITANCE.md)

## Rejected

### Realityの完全因果を通常プレイで随時調査する

- Status: Rejected
- Reason: 過去を掘り続ける調査ループは、次の結果を見るため時間を進めたいプレイ傾向と衝突する。
- Boundary: 開発・デバッグ用の完全Reality表示まで禁止するものではない。
- Related design: [`PLAYER_OBSERVATION.md`](../design/PLAYER_OBSERVATION.md)

## Promoted to canon

- 安定世界の成功を分析し、新しい困難または概念を次世界へ追加する。
  - [`WORLD_LIFECYCLE.md`](../design/WORLD_LIFECYCLE.md)
  - [`CONCEPTS.md`](../design/CONCEPTS.md)
- ピン、加護、本人と詩篇の矛盾。
  - [`PSALM_AND_INHERITANCE.md`](../design/PSALM_AND_INHERITANCE.md)

## Entry template

```markdown
### Idea title

- Status: On hold / Exploring / Rejected
- Added: YYYY-MM-DD
- Problem or opportunity:
- Why it is interesting:
- Why it is not in the current design:
- Conditions for reconsideration:
- Related design or ADR:
```
