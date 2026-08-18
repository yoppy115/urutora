# ADR-0027: accumulate settlement support and try fission before invasion

- **Status:** Accepted
- **Date:** 2026-08-18
- **Amends:** ADR-0022、ADR-0024
- **Amended by:** ADR-0029

## Context

旧Supportは90日活動の瞬間評価で、長期に繁栄したSettlementの蓄積や更新を表さなかった。また高Pressureが直接Invasionへ接続され、生活圏の飽和が平和的な拡張を生む経路がなかった。

## Decision

旧`50P + 30R + 20S`を`SupportPotential`へ改名し、0～100の`SettlementSupport`を日次累積する。既存自然消滅hysteresisは累積Supportへ適用する。Support 100かつPotential 80以上を365日維持したSettlementはRenewalし、ActiveのままSupport / counterをresetしてEvent / Pinを残す。

Order中のActive SettlementがSupport 35以上、非戦時・非migrationでPressure 0.40以上を90日維持したらFission hotspotを先に探索する。有効hotspotがあればchild Settlementを生成し、40%の決定論的migrant cohortを移す。有効候補がない場合だけ既存Invasion前提を再Validationする。

直接親子Settlement間は関係が有効な間、平時のInvasion target・Affiliation差だけの攻撃・Collision combatを抑制する。

## Consequences

- Pressure 0.65を30日で直接Invasionする旧経路は、Fission gateなしには使えない。
- Support Potential、累積Support、Renewal、Resident-Days、Migration、親子関係を別state / eventとして追跡する。
- Fission成功時の人口変化が以後のSupport / Pressureへ自然に反映される。
- ADR-0029により、CenterはFission開始時snapshot上のCell別Unaffiliated Resident-Daysを第一順位として選び、MigrationはAliveかつActive child SettlementのInfluence内へ実到達した時点で完了すると確定した。
