# v0.2 Statistics and Diagnostics

**Status:** Baseline observation obligations / Draft storage and UI mechanics

v0.2はRaw Logを人間が直接読み続けるより、ゲーム内Statistics UIとheadless集計で因果を確認できることを優先する。StatisticsはEvent / Realityのread-only projectionであり、集計や表示がSimulation結果、処理順、乱数消費を変えてはならない。

## World phase

最低限、次を確認可能にする。

- Current World Phase。
- Generation開始日、Order開始日。
- Population、PopulationCV。
- Births、Deaths、DemographicImbalance。
- Generation → Order条件の各現在値と連続成立日数。

Order Transitionの前90日と後90日をv0.2 default比較Windowとし、Population、Birth、Death、Combat Death、Collision Attack、Reproduction、平均Age、Settlement所属率を比較する。

## Settlement

World全体でSettlement数、Affiliated / Unaffiliated Population、所属率を持つ。各Settlementについて次を保持・表示可能にする。

- ID、Center、成立日、Founder数。
- 所属人口、World Population比。
- Affinity加入数、離脱数。
- Core Occupancy、CrowdingPressure。
- Initial Hostility、Friction。
- 消滅日、消滅理由、征服 / 統合先。

## Affiliated versus unaffiliated

Settlement所属NPCとUnaffiliated NPCについて、最低限次を比較する。

- 平均Age、平均Death Age、平均HP。
- Combat Death数 / 率、Vitality Death数 / 率。
- Rest Action率。
- Reproduction Attempt、Success、Birth。
- ConceptMark取得数。

## Violence

最低限、次を分離集計する。

- Collision総数、Collision Attack数。
- 同Settlement Collision抑制数。
- Unaffiliated保護Collision数。
- Other Settlement Collision数、Friction増加件数。
- Explicit Attack、Counterattack、Pursuit Attack。
- Collision Attack / Explicit Attack / Counterattack / PursuitのDamage。

死亡は最後の一撃だけでなく、Combat Damage Sourceの寄与を診断可能な構造にする。

## Reproduction

Failureを少なくともTargetAbsent、Maturity、HP、Cooldown、Distance、Reject、Other Reality Failureへ可能な範囲で分離する。Settlement Core内 / 外でもAttempt、Success、Failureを比較可能にする。

NPC向けActionOutcomeへ非公開Reality値を露出しないという境界は維持する。診断projectionが見えることとNPCが知ることを混同しない。

## Invasion

各Invasion Eventについて次を確認可能にする。

- 開始日、終了日、Attack Settlement、Defense Settlement。
- Trigger CrowdingPressure、Target選択理由。
- Initial Force Size、Core Cohort人数、Frontier Cohort人数。
- Advance Bias離脱数、Combat Death、Rest離脱。
- 最大Core占有率、Center占拠有無。
- Attack / Defense Victory、戦争期間、統合後人口。

## Concept and aura

最低限、次を集計する。

- Landmark別Exposure。
- ConceptMark取得数、現在Holder数、取得時Age。
- Settlement所属 / Unaffiliated別取得率。
- SettlementごとのMark Holder数。
- Aura対象延べNPC数、Invasion中Aura Holder数。

## Determinism boundary

- StatisticsはSimulation用random streamを消費しない。
- 表示、sampling、logging量を変えてもSimulation Event列を変えない。
- Event ID、Settlement ID、Invasion Event ID、Config、RunSeed、commitを用いてheadless結果を再照合できる。
- 集計値からSimulation Coreへcommandや補正を返さない。

保存形式、schema version、長期集計の圧縮、UIレイアウトはDraftである。
