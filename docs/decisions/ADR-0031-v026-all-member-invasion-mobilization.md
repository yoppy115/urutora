# ADR-0031: mobilize every living affiliated member for invasion

- **Status:** Accepted
- **Date:** 2026-08-19
- **Amends:** ADR-0024、ADR-0030

## Context

v0.2.6開始時点は既存Pressure rateを2倍してTarget Forceを算出したが、高Pressureでも所属者全員が参加するとは限らなかった。今回のInvasionは部分的な遠征隊ではなく、攻撃Settlement全体の戦闘参加として扱う。

## Decision

- Invasion開始時、攻撃SettlementにActive Affiliationを持つ全Alive NPCを攻撃参加者にする。
- 同日にRestしたNPCも参加対象から除外しない。開始後にRestを選んだ場合は既存のFieldRest / Retreating規則を使う。
- `MobilizationBase`、Pressure係数、20～50% Clamp、2.0倍係数をTarget Force算出から廃止する。
- Active Invasion stateが残る所属者が1人でもいる場合は部分動員せず、そのSettlementから新規Invasionを開始しない。
- 最低3名、Fission先行、60日cooldown、Core / Frontier分類、能力値非選抜は維持する。

## Consequences

- `InitialAttackForce`は開始時のAlive affiliated populationと一致する。
- Core / Frontierは参加可否ではなく、全参加者内の初期cohort分類としてのみ機能する。
- Simulation Config schemaを6へ更新し、`mobilizeAllLivingAffiliatedMembers = true`を必須にする。
