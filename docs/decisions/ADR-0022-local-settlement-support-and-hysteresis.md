# ADR-0022: maintain settlements with local support and hysteresis

- **Status:** Accepted
- **Date:** 2026-08-18

## Context

World Population比による自然消滅は、Settlement自身が維持されていても他地域の人口増だけで消滅させ、繁殖Hotspotという形成理由と維持理由を断絶した。

## Decision

直近90日のResident Presence、Reproduction Continuity、Social Activityから`Support = 50P + 30R + 20S`を算出する。Reproduction Continuityは現行Formation thresholdを再利用する。Support 25未満でLowSupportDaysを進め、25以上35未満では凍結、35以上でresetし、365 LowSupportDaysで自然消滅する。

消滅時はAliveな所属者をUnaffiliatedへ戻し、Founder、History、Statisticsを保持する。World Population比は判定に使わない。

## Consequences

形成と維持が局所生活活動で連続する。Support成分・閾値はConfig調整可能だが、局所性とHysteresisの境界を単純比率へ戻さない。
