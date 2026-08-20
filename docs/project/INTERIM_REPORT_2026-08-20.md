# World Sim 開発中間報告書

- **Status:** Project record / non-canon
- **Report date:** 2026-08-20
- **Implementation snapshot:** `a608944ce31584a7aaaeffdb8a62060b5ffe1db0`
- **Current line:** v0.2.6 Fission / Invasion Throughput Update + 全所属員動員追補

この文書は、現時点までの目的、成果、実装状態、課題、継続可能性を一度俯瞰するための中間報告である。ゲーム仕様の正本ではなく、将来この時点の判断と進捗を振り返るためのProject recordとする。現在の仕様は`docs/design/`、責務境界は`docs/architecture/`、採用理由は`docs/decisions/`を優先する。

共同作業上の姿勢と進め方は [`COLLABORATION_RULES.md`](COLLABORATION_RULES.md) に分離する。

## 1. Executive summary

本開発は、会話上のゲームアイデアから、再現可能なheadless Simulation、Windows Desktop観測App、Replay Runner、構造化ログ、配布物まで到達した。現時点では「企画書だけ」でも「完成ゲーム」でもなく、個体生態系と初期社会形成を実際に観測・改修できる研究用ゲームプロトタイプである。

最大の成果は、次の三点を同時に成立させたことである。

1. RealityとPerceptionを分離し、NPCが主観だけで行動する。
2. seed、Config、commitを固定すれば、表示頻度やCPU並列度に左右されず結果を再現できる。
3. 個体の誕生・移動・会話・戦闘・繁殖・死から、Settlement、Fission、Invasionまでを一つの因果列として接続した。

実現可能性は高い。ただし今後の主な難所は、コードを書けるかどうかではない。追加システムが本当に「次を見たくなる逸脱」を増やしているかを、観測可能な指標で判断し続けられるかである。機能追加速度より、実験・解釈・正史整理の品質が成否を決める段階へ入っている。

## 2. 制作目的と変わっていない中核

このプロジェクトの目的は販売や完成率の最大化ではなく、制作者本人が世界の予想外の応答を眺める感情的満足を得ることである。プロトタイプで止まることを失敗とせず、再開・追加・削除・再構成しやすい状態を重視する。

中核体験は一貫して次である。

```text
観測 -> 予測 -> 期待 -> 逸脱 -> 結果 -> 解釈 -> 次の観測
```

逸脱は大きな乱数イベントから直接作らない。理解可能な小さな規則、主観差、情報変形、空間競合、淘汰、社会構造が接続された結果として発生させる。プレイヤーは万能管理者ではなく、現在を観測し、限られた啓示を投げ、世界から返る答えを見る立場である。

## 3. 現在までの開発系譜

| 段階 | 主な到達点 |
| --- | --- |
| 設計基盤 | VISION、Reality / Perception、Utility AI、繁殖、概念、世界継承を正史・ADR・Backlogへ分離 |
| v0 | 64×64 Grid、日次tick、Micro Round、Need、Utility、Move、Communication、Combat、Reproduction、Genetics、Vitality、Concept Landmark、Desktop観測App |
| v0.15 | 初回Runの人口崩壊、TargetAbsent、繁殖不全、長寿命・記憶肥大を受け、生態系時間scaleとTargeted Action処理を再設計 |
| v0.2 | 繁殖HotspotからSettlementが自然形成され、GenerationからOrderへ進む社会層を追加 |
| v0.2.1～v0.2.3 | Hotspot調整、出生所属、観測UI、空間index、決定論的並列化、Settlement重複防止 |
| v0.2.4 | Rest、Home / Foreign移動、Proto-Order、Support、Pressure、Friction、Invasionを長期Run向けに安定化 |
| v0.2.5 | Person / Event / Settlement Knowledge、増分Statistics、累積Support、Renewal、Fission、Migration、持続型Invasion |
| v0.2.6 | Fission hotspotを全Alive NPCの生活履歴へ拡張し、Invasionを全所属員動員、距離連動Defense、60日cooldownへ更新 |

初期scaffold以降、現在snapshotまで29 commit、137 files、約20,866行追加が積み上がっている。Production `src`は67 C# files・約11,455行、`tests`は24 C# files・約2,842行である。これらは規模の優劣ではなく、すでに単発スクリプトではなく継続保守対象のソフトウェアになったことを示す目安である。

## 4. 現在実装されているもの

### Simulation Core

- 日次tick、最大回数付きMicro Round、固定Targeted Action phase。
- Base / Effective stat、Need、Utility Top 3 + softmax、RiskPreference。
- Reality、Observation、Perception、Person / Event / Settlement Belief。
- Move、Collision Attack、Threat Memory、Rest、Communication、情報変形、Attack、Counterattack、Flee、Pursuit。
- Reproduction、Birth arbitration、Genetics、Mutation、Vitality curve、Immediate Death。
- Concept Landmark、Exposure、ConceptMark、Aura。
- Settlement Formation、Affinity、Affiliation、Generation / Order、Support、Renewal、Fission、Migration。
- Friction、SettlementPressure、Invasion、FieldRest、Retreating、Conquest。

### Observation and operations

- Windows Formsによる64×64 Mapと現在状態の軽量観測。
- 人口、所属率、平均年齢、Action選択等のグラフ・集計。
- headless実行、複数World自動進行、明示的World完了。
- World別構造化ログ、完了WorldのZIP化、SHA-256、release manifest。
- Configとseedを含むrecord / verify replay。

### Verification snapshot

2026-08-20に既存Release test executableを再実行し、次を確認した。

| Suite | Passed |
| --- | ---: |
| Simulation.Core.Tests | 53 / 53 |
| Simulation.App.Tests | 12 / 12 |
| Simulation.Runner.Tests | 3 / 3 |
| **Total** | **68 / 68** |

`outputs/World Sim v0.2.6/release-manifest.json`はrepository commit `a608944...`、clean tree、App / Core / Runner / Config各artifactのSHA-256を保持している。

## 5. アーキテクチャ上の到達点

現在の構造は、少なくとも思想上は次の依存方向を維持している。

```text
Simulation.App -------> Simulation.Core
Simulation.Runner ----> Simulation.Core
Tests ----------------> Core / App / Runner
Simulation.Core ------> Presentationへ依存しない
```

重要な契約は次である。

- DecisionはRealityを直接読まず、Perceptionと自己状態からActionIntentを作る。
- ResolutionだけがRealityを検証・変更し、失敗もActionOutcomeとして認識へ戻す。
- UI、Statistics、Logging、LLMはSimulation結果へ権威を持たない。
- 乱数は用途別keyから派生し、collection順や追加された無関係な乱数利用へ全面連鎖しない。
- ObservationとIntent planningだけを安全に並列化し、Action ResolutionとMaintenanceの権威的順序は直列に保つ。
- Social stateを個人ThreatやMoveへ暗黙に埋め込まず、Settlement、Support、Fission、Invasionをtyped stateとして扱う。

この境界は将来拡張にかなり有利である。国家・文化・疾病等を追加する場合も、既存Coreを全面破壊せず、Observation、Policy input、Event、Maintenance phaseへ接続できる余地がある。

## 6. 正史上は存在するが、まだ本格実装していないもの

現在実装されている正式WorldPhaseはGenerationとOrderだけである。次は将来正史またはBacklogとして保存されているが、現行Simulationの完成済み機能ではない。

- Struggle / 群雄割拠と、その正式遷移条件。
- 国家、属国、占領統治、反乱、階層、資源、経済、宗教、疾病。
- 高度なCulture、学習、親子社会関係。
- 上位存在の簒奪・授与・継承。
- World collapse / reorganizationと次世界生成。
- Reality → Perception → History → Psalmの本格変換。
- Revelationと限定的プレイヤー介入。
- 動的Concept / Difficulty network。
- LLM Historian / Interpreter。

これらを消したのではない。最小個体生態系とSettlement社会が持続し、観測に値する因果を作れるか確認するまで、意図的に先送りしている。

## 7. 開発を通じて得た知見

### 仕様の面白さとSimulationの成立は別問題

初回v0 Runでは、企画上は魅力的だった規則も、TargetAbsent、Combat偏重、繁殖失敗、長すぎる世代時間によって観測不能になった。面白い構想を守るには、その構想が起きる前に生態系が壊れない処理順と時間scaleが必要だった。

### 主観境界は文章だけでは守れない

Reproduction Candidate、Attack Utility、SettlementBelief等では、Realityの値を使うと実装が簡単になる。しかしそれを許すと本作の逸脱源が消える。型、phase、test、ADRへ同じ境界を重ねて初めて維持できる。

### 社会システムはBonusより負圧が重要

Settlementは単に所属者を強化する装飾ではなく、人口集中、Support、Pressure、Fission、Friction、Invasionを生む装置へ発展した。成功が次の困難を生むという長期構想の小型実験になっている。

### 観測性能もゲーム設計の一部

全履歴を毎描画するUIは、Simulationを遅くして「次を見たい」体験を損なう。軽量Desktop projectionと詳細headless diagnosticsの分離は、単なる最適化ではなく観測ゲームとしてのテンポを守る設計判断だった。

### 正史・実装・実験結果を分離すると再開しやすい

会話を仕様にせず、Baseline、configurable default、Draft、ADR、Backlog、Implementation Register、Run identityへ分けたことで、長い議論と多数の更新を経ても「何が確定し、なぜそうしたか」を追跡できている。

## 8. 実現可能性と継続性の評価

| 観点 | 評価 | 所見 |
| --- | --- | --- |
| 個体生態系の継続改良 | 高い | Core、Config、headless test、統計が揃っている |
| Settlement以後の社会拡張 | 高いが条件付き | 追加可能だが、Maintenanceとstate責務の肥大化を監視する必要がある |
| 長期Runと比較実験 | 高い | seed、replay、commit、Config、構造化logがある |
| 観測ゲームとしての面白さ検証 | 未確定 | 技術的成立と「次を見たい」は別で、複数seedの観察が必要 |
| World Lifecycle / 次世界 | 中程度 | 上位設計はあるが、現行社会層からの入力契約をまだ定義していない |
| LLM Historian | 高い | Core外adapterなら導入可能。入力となるHistory / Pin選別の成熟が先 |
| 長期保守 | 概ね良好 | 文書・ADR・testsは強い。Git統合とversion整合に現在大きな負債がある |

総評として、実現不可能な大風呂敷ではない。段階的に切り出せる構造をすでに持っている。ただし全構想を一気に実装すると破綻する。現在まで成功してきた「小さく追加し、Runで壊れ方を観測し、正史を補正する」方法を維持することが条件である。

## 9. 現在の重要なリスクと技術的負債

### 9.1 Gitの安定基線が実態に追いついていない

ローカル`main`と`origin/main`は初期scaffold `0d8e175`のままで、実装本体は複数の`codex/v0xx-*` branchへ直列に積み上がっている。GitHub上のDraft PR #1もv0.2.5文書commitまでであり、現在のv0.2.6実装全体を安定branchへ統合した記録ではない。

現物commitと配布物は存在するため直ちにコードが失われる状態ではないが、新しい作業者や未来の自分が`main`を見たとき、現在地を誤認する危険が高い。これは今いちばん大きい継続性リスクである。

### 9.2 同じv0.2.6名に二つのcommitがある

Git tag `v0.2.6`は`d58c74c`を指す一方、全所属員動員を含む配布物`World Sim v0.2.6`のmanifestは`a608944`を指す。Run identityにcommitがあるため識別はできるが、人間向けversion名としては曖昧である。

既存tagを黙って移動するより、後続patch versionまたは明示的なamendment releaseとして整合させる方が安全である。

### 9.3 社会層の集中化

Settlement Maintenance、Invasion、Fission、Knowledgeが増え、Coordinator、Engine、Domain Modelsの変更範囲が広がっている。現時点で即座に全面refactorする必要はないが、Struggle、国家、疾病を載せる前に、phaseごとのread / write setとdomain ownershipを再確認すべきである。

### 9.4 面白さを示す比較Runがまだ不足

仕組みが動くtestは強い一方、v0.2.5とv0.2.6の差が、Fission数、Invasion期間、Settlement寿命、人口、EventBelief、プレイヤーの観測欲求をどう変えたかは、まだ中間結論がない。これ以上の機能追加は、原因が分からないまま結果だけ複雑にする危険がある。

### 9.5 保存・再開と長期観測UI

World snapshotの保存・再開、Run比較画面、長期graph downsampling、log schema migrationは未実装である。現状でも自動Runとlog分析は可能だが、観測者が特定世界へ戻る遊び方にはまだ弱い。

## 10. 次の推奨Checkpoint

次の大型仕様を足す前に、`v0.2.6 Consolidation`を一度行う価値が高い。

1. 現行commit列を失わず、安定branchへ統合できる形に整理する。
2. tagとrelease manifestのversion / commit対応を一意にする。
3. v0.2.5と現行v0.2.6を同一Config・複数seedで比較する。
4. 人口、Settlement数、Fission、Migration、Invasion、死因、Knowledge量を因果別に要約する。
5. 実際の観測で「次を見たい」と感じたEvent列と、退屈・停滞した区間を人間側で記録する。
6. その結果から、Struggleへ進むか、個体・Settlement挙動をもう一度調整するかを決める。

このCheckpointの目的は開発を止めることではない。ここまで高速に積み上げた成果を、次の拡張が安心して乗れる地盤へ変えることである。

## 11. 中間結論

この開発は、当初の「こんな世界を見たい」という抽象的な願望を、かなり忠実に実行可能な形へ落とし込めている。とりわけ、主観差、情報変形、遺伝、空間競合、Settlementの成功と圧力が同じSimulation内で接続されたことは大きい。

一方、まだ検証しているのは壮大な最終構想そのものではなく、その土台が観測に値するかである。現在の正しい自己評価は「完成へ近づいた」より、「世界を壊し、測り、直せる実験装置を手に入れた」に近い。

それは地味だが、かなり強い地点である。
