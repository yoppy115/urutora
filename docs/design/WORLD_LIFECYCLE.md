# World Lifecycle

**Status:** Baseline lifecycle / v0.2 Generation-Order defaults / Draft later phases

## Baseline lifecycle

```text
萌芽 -> 秩序 -> 争覇 -> 滅亡 / 再編 -> 詩篇 -> 次世界
```

フェーズは内部状態として扱う。通常UIへ名前や単純ゲージを常に明示する必要はないが、v0.2の観測・診断UIではGeneration / Orderと遷移条件を表示する。

## 萌芽

上位存在の周囲に、個体、集団、集落、文化、社会が形成される。複数の上位存在は、それぞれ影響圏を持ち得る。

影響圏は信者だけを意味しない。嫌悪、恐怖、対立を通じて社会が上位存在を中心に形成されている場合も影響下とみなす。

### v0.2 Generation

v0.2は萌芽の最小実装を `WorldPhase = Generation` と呼ぶ。秩序を持たない個体群の繁殖、移動、Collision、Threat、Combatを世界生成過程として扱い、Generation中からReproduction Success HotspotにSettlementが形成される。

v0.2.4ではSettlementが根付くため、Founder、Affinity、所属、Birth Affiliationに加え、同所属Collision抑制、Core正Vitality`×1.25`、通常Affinity gain`×2`、Home / Foreign移動規則、Move疲労軽減を限定Proto-OrderとしてGenerationから有効にする。Order専用Rest、負Aging軽減、Outside Reproduction Penaltyはまだ解禁しない。

## 萌芽から秩序

上位存在の周辺に持続可能な秩序が成立することで移行する。国家成立だけを条件にせず、単独集団や特殊な共同体も許容する。

v0.2では、90日rolling windowのPopulationCVとBirth / DeathのDemographicImbalanceがConfig閾値を30日連続で満たすことを、GenerationからOrderへの最小判定とする。絶対人口や固定経過日数だけでは決めない。判定は固定順のTick末Settlement Maintenanceで行い、commitしたOrder stateは原則翌Tickから有効にする。Order移行後、既存および新規SettlementがRest、Vitality、Aging、Reproduction、Collision等へ局所的な社会Ruleを適用する。詳細は [`V0_2_SETTLEMENT_ORDER.md`](V0_2_SETTLEMENT_ORDER.md) を参照する。

## 秩序から争覇

複数の影響圏が接触し、既存秩序の処理能力が試される。争覇における争いは戦闘だけでなく、次のような広義の困難を含む。

- 資源不足、疾病、不和、情報問題
- 文化摩擦、人口変動
- 社会制度の副作用
- 上位存在と社会の矛盾

### v0.2.5 Expansion observation

v0.2.5で正式に実装するWorldPhaseはGenerationとOrderだけである。Struggle / 群雄割拠を追加しないことは未決ブロッカーではなく、Run観測までの意図的延期である。

Order内部でWorld Population Growth、Affiliated Population Ratio、Active / Child Settlement Count、Fission Count、SettlementPressure、高Support / 高Pressure Settlement数、Invasion Count、Parent / Child network、Settlement間Friction、Settlement間人口差を`Expansion Indicators`として増分集計できる。

Expansionは診断・統計上の状態でありWorldPhaseではない。新しいWorld Rule、Bonus、Difficultyを自動解禁しない。

正式なStruggle条件は、複数Settlementの持続、親子の独立存続、InvasionによるCombat・死傷・征服、先行者独占、人口圧のFission / Invasion変換、継続的Settlement競争をv0.2.5 Runで評価してから設計する。

## 争覇から滅亡・再編

```text
困難 -> 適応 -> 副作用 -> 新しい困難 -> さらに適応
```

困難への対応そのものが別の困難を生み、既存秩序の処理能力を超えることで崩壊または再編へ進む。単純な `World Stability -10` のような単一ゲージを主因にしない。

## 滅亡・再編

世界自体が部分的に壊れ、従来のシミュレーション規則や接続が機能を失い得る。マップ領域、交易、情報網、国家機能、疾病、移動、集団などへの具体的な作用はDraftとする。

このフェーズでは上位存在の力が簒奪または授与され、次世界への継承が発生する。詳細は [`PSALM_AND_INHERITANCE.md`](PSALM_AND_INHERITANCE.md) を参照する。

## Stable worlds

世界が困難へ適応し長期間安定した場合も、永遠に放置しない。その世界を「現在の問いを解いた世界」とみなし、適切なタイミングで再編する。

成功を分析し、その成功では簡単に解けない新しい困難または概念を次世界へ追加する。

```text
問題 -> 適応 -> 成功 -> 新しい問題
```

## Draft mechanics

- Order以降のフェーズ遷移の正確な閾値と検出方法。Generation→Orderのv0.2 defaultは確定済み。
- v0.2.5後のStruggle正式遷移、軍事占領、Leader / 能力値による軍事選抜。Expansion Indicatorsは確定済みだが、遷移閾値とStruggle固有RuleはBacklogである。
- 一世界の実時間と各フェーズの長さ。
- 滅亡時にどのシステムをどう変調・停止するか。
- 安定世界を再編するタイミング。
- 複数の遷移条件が同時成立した場合の優先順位。

採用理由は [`ADR-0003`](../decisions/ADR-0003-causal-world-lifecycle.md) を参照する。
