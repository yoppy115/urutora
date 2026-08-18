# ADR-0030: broaden fission hotspots and replace invasion re-arm with cooldown

- **Status:** Accepted
- **Date:** 2026-08-19
- **Amends:** ADR-0024、ADR-0027、ADR-0028

## Context

v0.2.5のFission hotspotはUnaffiliatedだけを数えたため、既存Settlement所属者が新しい生活圏へ集中してもFission候補にならなかった。Invasionは動員が小さく、近距離と遠距離で攻撃者不在の終了判定が同じ7日だった。またPressure低下30日の再武装は、Event間隔を生活圧の推移へ間接的に依存させ、再開時期を読み取りにくくした。

## Decision

- Fission hotspotの30日Resident-Daysと現在人口を全Alive NPCから集計する。形成位置、閾値、migrant eligibilityは変更しない。
- Invasion Target Forceへ既存Mobilization rateの2.0倍を適用し、所属人口を上限とする。eligible規則と能力値非選抜を維持する。
- 攻撃者不在によるDefense Victoryを`7 + Ceil(Center間Chebyshev距離 * 1.0)`日連続とし、必要日数をEvent開始時に固定する。
- armed / 低Pressure再武装stateを廃止し、攻撃Settlementごとの開始間隔を60日にする。Active Invasion禁止とFission先行gateは維持する。

## Reasons

- 既存社会の人口移動もFission候補として観測し、Hotspot不足だけで平和的拡張が封じられる状態を減らす。
- 動員規模を明示的な係数で増やし、既存rate、cohort、eligible境界を壊さない。
- 遠距離侵攻ほど一時的な攻撃者不在で即時終了しにくくする一方、90日膠着上限は維持する。
- 再開条件を開始tick基準の単一counterへ置換し、Pressureによる開始条件と時間的cooldownを分離する。

## Consequences

- Fission候補は増えるが、他Settlementとの空間重複制約と親所属migrant最低数により成立しない場合がある。
- 高Pressure時のTarget Forceは従来の2倍で、所属人口を超えない。
- Invasionは開始後60日でcooldown上は再開可能だが、Event継続中は開始できない。
- Config schemaを5、diagnostics schemaを7へ更新し、resident集計、最終開始tick、cooldown残日数、Center距離、攻撃者不在必要日数を記録する。
