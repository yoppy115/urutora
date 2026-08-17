# ADR-0018: ConceptMark gains a temporary social aura

- **Status:** Accepted
- **Date:** 2026-08-17

## Context

Concept Landmarkは個体Markを介して淘汰を歪めてきたが、社会へ影響が伝播する経路はなかった。Settlementが定住と長寿を生むv0.2では、Conceptが社会へ広がる最小の仕組みが必要になる。

## Decision

Landmark Exposureをradius 4へ拡大し、ConceptMark Holderがradius 2以内の同一Settlement所属者へ非遺伝・一時的Auraを与える。Auraは敵とUnaffiliatedへ作用せず、同種はstackしない。異種は併存できる。

Invasion中のHolderは、現在radius 2以内にいる同一Event参加者へCohesion Biasを与える。Advance Biasを主、Cohesionを副とし、遠距離吸引や前進停止を起こさない。

## Reasons

- Landmark→Individual Mark→Aura→Settlement Societyという説明可能な伝播を作る。
- ConceptMark自体を遺伝・強制配布せず、個体と社会の二層を保つ。
- Invasion隊列へConcept Holderを中心とした局所的まとまりを生む。

## Consequences

- Effective statsはBase、本人Mark、一時Auraを区別して計算する必要がある。
- Aura対象、範囲、stack、所属、Invasion Eventを決定論的に評価する。
- 同種MarkとAuraの正確な合成、生存Aura出入り時のHP上限は別途確定が必要である。
