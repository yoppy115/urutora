# Implementation Register

この台帳は、採用済み実装のうちゲーム正史そのものではない最適化・Presentation・運用事項と、ユーザーが採用したminor defaultを追跡する。正史境界は対応するdesign / ADRを正本とする。

| Item | Status | Canonical record |
| --- | --- | --- |
| v0.2.1 Hotspot | Adopted configurable default | 90日、5×5、Success 3、15日評価。旧4×4 / Success 4を置換。spacing 7は単独では変更しない |
| v0.2.2 birth affiliation | Implemented boundary | 同所属は場所非依存、片親所属は受胎時両親が所属先Influence内、異所属は同じ一意なActive Core内 |
| ConceptMark display | Presentation requirement | Map/UIでHolderを識別可能。具体旗はdetail |
| Simulation speed | Presentation / Operations | Pause、1x、2x、3x、5x、10x、50x。結果非干渉 |
| Observation cache | Optimization | 観測意味論とEvent列を変更しない |
| Log flush interval | Operations | configurable。World ruleではない |
| NPC history | Read-only history | 行動履歴とKill count |
| v0.2.3 overlap prevention | Implemented boundary | 既存Influence内Successを除外。新Core全Cellを既存Active Influenceと非重複。default実効Center距離 > 9 |
| Settlement palette | Presentation detail | 不変IDと分離した最大約60色の再利用palette |
| Settlement details / extinct display / friction | Presentation | 詳細とFrictionを分離表示。消滅SettlementはMap非表示、History保持 |
| NPC neighborhood index | Optimization | query semanticsを変更しない |
| Deterministic CPU parallelization | Engineering | read / planningだけを分離並列化し、stable順でmergeする |

v0.2.5は未実装の正史更新であり、Person / Event / Settlement Belief、増分Statistics、累積Support / Renewal、Fission、Invasion participant state / 継続Victoryを含む。v0.2.4以前の実装状態をこの文書更新だけで変更済みとみなさない。実装後にConfig schema、Event schema、最適化、保守的技術既定値を追記する。
