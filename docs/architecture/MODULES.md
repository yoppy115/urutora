# Modules

**Status:** Baseline responsibilities / Draft APIs

実装技術を選ぶ前の責務一覧。名称や粒度は確定APIではない。

| Module | Responsibility | Must not |
| --- | --- | --- |
| Simulation Runner | 日次phase、Attack→Reproduction→Communication、Movement、Rest、Micro Roundを調整する | 終了済みphaseへ再抽選Intentを巻き戻す |
| Reality Store | 客観状態を構成されたstate sliceとして保持する | NPC向け判断APIを直接提供する |
| Observation | RealityとActionOutcomeから観測事実を生成する | Utilityを評価する |
| Perception Store | NPCごとの有限な観測・伝聞履歴、Confidence、LifeStage、Threat Memory、Position invalidationを扱う | 未観測Realityを透過参照・最新値で自動上書きする |
| Needs | 生存、休息、活動、交流、繁殖の状態を評価する | 行動を直接解決する |
| Utility Decision | 主観情報から候補を評価しActionIntentを作る | RealityやUIへ依存する |
| Action Resolution | IntentをRealityで検証しTargetAbsent等のOutcomeを返す | NPCの知識を意思決定前後に超能力的に補完する |
| Interrupt Coordinator | Attack / Reproduction Accept時のIntent破棄と上限付き再評価を調整する | Action枠を増やす、RejectでIntentを消す |
| Spatial Resolution | Grid占有、Move競合、Collision Attack変換を扱う | UtilityへReality占有状態を漏らす |
| Combat Resolution | Attack、Damage、Counterattack、Pursuit Attackを扱う | Reactionを無限再帰させる |
| Communication | Held Information交換と受信時変形を扱う | 未知Reality情報を生成する |
| Lifecycle / Aging | 年齢、Vitality、即時Dead遷移、tick末cleanupを扱う | 繁殖やUtility式を所有する |
| Reproduction | Attempt/Reaction、遺伝、BirthRequest、batch出生競合、系譜を扱う | 老化方式やqueue順で結果を決める |
| Settlement Formation | Reproduction Successのrolling spatial集計、Candidate、Center、Founderを扱う | Order Bonus、Combat、所属変更を所有する |
| Settlement Affiliation | Affinity、Active Affiliation、移籍、Invasion lock、消滅時Unaffiliated化を扱う | NPC PerceptionやUtilityを直接書き換える |
| World Phase | 人口系列、PopulationCV、DemographicImbalance、Generation→Orderを扱う | Settlement個別Bonusを実装する |
| Settlement Policy | WorldPhaseと地域・所属からRest、Vitality、Reproduction、Collision policyを提供する | Spatial、Utility、Lifecycleの権威的計算を奪う |
| Settlement Movement Policy | Home / Foreign Biasと自Settlement内Move fatigue modifierを投影する | Flee、Invasion Advance / Defense、Reality validationを上書きする |
| Settlement Support | 90日P/R/S、Support、Hysteresis、自然消滅を扱う | World Population比で局所生活を代用する |
| Settlement Relations | 日次正規化Friction、方向性Hostility、Founder cohortからの初期関係、宣言時retentionを扱う | 個人に存在しないThreatを捏造する |
| Settlement Pressure | 30日ResidentLoad / MovementCongestion / ReturnFailureとHigh / Low counterを扱う | CoreOccupancyをInvasion Triggerとして再利用する |
| Invasion | eligibility、target、mobilization、cohort、Bias、勝敗、統合を扱う | 専用Utility AI、Center勝利、国家制度を追加する |
| Concept / Difficulty | 概念・困難データと世界進化を扱う | 表示名を安定IDとして使う |
| Concept Exposure | Landmark距離、Exposure、Mark取得とEffective補正を扱う | Base遺伝値を書き換える |
| Concept Aura | 所属・距離・Invasion Eventから一時AuraとCohesionを投影する | Markを付与・遺伝・無限stackする |
| World Lifecycle | 萌芽から次世界までの状態と遷移を扱う | 単純な全能崩壊ゲージだけで遷移させる |
| Higher Entity | 上位存在、影響圏、加護を扱う | 通常NPCとして世界を直接支配する |
| Revelation Intake | プレイヤーの啓示を世界の認識入力へ変換する | NPCへ直接命令する |
| Pin / History | 重要イベントの索引と住人による歴史を構築する | Realityの完全因果を捏造する |
| Inheritance | 簒奪・授与と次世界への実体継承を扱う | 詩篇と本人を同一データに潰す |
| Psalm Generator | Historyとピンから詩篇入力を構成する | Simulation Coreの結果を変更する |
| Narrative Adapter | LLMまたはfallbackで人間可読文を生成する | 世界状態の権威を持つ |
| Configuration | schema検証済み設定とゲームデータを供給する | 実行中に暗黙のglobal状態になる |
| Event Log | 機械可読な世界内イベントを記録する | ドメインの結果を変える |
| Statistics Projection | World / Settlement / Invasion / Concept集計を構築する | 集計値からSimulationへcommandを返す |
| Research Exporter | 注目実験の再現情報と要約を保存する | 巨大な生ログを正史へ混ぜる |
| Player Observation | 現在中心のviewと文章表現を作る | Simulation Coreから参照される |
| Presentation Adapter | ゲームエンジン・UIへ接続する | Simulation Coreから参照される |

## Cross-module contracts

- queryは読み取り専用、commandは意図、eventは起きた事実として区別する。
- ActionIntentとActionOutcomeを別の型にする。
- IDは保存、ログ、replay、次世界継承をまたいで安定させる。
- interfaceへ渡すデータは必要最小限にし、内部状態オブジェクトを丸ごと共有しない。
- 時刻と乱数は外部依存として注入し、テストで差し替えられるようにする。
- エラー時には再現に必要なseed、config識別子、tick、entity IDを報告する。
- Narrative Adapterは読み取り専用の入力を受け、ドメインへcommandを返さない。
- Reactionは明示的な深さ制限を持ち、CounterattackやPursuitから同種Reactionを連鎖させない。
- Base能力とEffective能力を型または命名で区別し、遺伝にはBaseだけを渡す。
- 乱数contextは用途単位で派生し、UIやログと共有しない。
- 通常ActionとReactionを別contractにし、ReactionがAction回数や通常Need costを消費しないことを型またはdispatcherで守る。
- EntityがDeadへ遷移したら後続Intent/Reaction eligibilityとGrid占有を即時無効化し、Event/collection cleanupはtick末へ遅延できる。
- BirthRequestは受胎時Positionと遺伝入力をimmutableに保持し、batch resolverが希望Cell競合を順序非依存で再抽選する。
- Held Information capacityはSubject + Propertyごとに3件とし、4件目で最古をFIFO削除する。Confidence代表値選択と容量管理を分ける。
- Reproduction Candidateには対象PerceptionのAlive / Position / LifeStageだけを渡し、対象RealityのHP / CooldownはResolution portの内側に閉じる。
- Subject消滅の直接確認はPerception StoreへSubject全Property削除commandを発行する。TargetAbsentや死亡伝聞では発行しない。
- Reproduction Success EventはSettlement FormationとAffinityへ安定ID付きで渡し、過去のRealityを遡及変更しない。
- Settlement PolicyはGeneration / Order、地域、Affiliation、Invasion関係を明示的なvalueとして各Resolverへ渡す。
- InvasionのAdvance / Defense / Cohesion Biasは既存Move direction policyへ合成し、新Actionや別Utility pipelineを作らない。
- Statistics ProjectionはDomain Eventとread-only queryだけを受け、Simulation用random streamを消費しない。
- Settlement Maintenance Coordinatorは確定した12段階の日末順だけを調整し、各domain ruleを所有しない。日中Eventと翌Tick stateを分離する。
- Hotspot arbitratorはimmutable Candidate snapshot、Reproduction Success数、named seedを使い、scan / collection / thread順へ依存しない。
- Frictionは対称Pair state、Hostilityは方向性stateとして別型にし、日次root incidentを人口scaleして更新する。Counterattackによる二重計上とActive Invasion combat加算を防ぐ。
- Aura由来MaxHP解除はDamage portを経由せずstate normalizationとしてClampする。
- Rest fatigue policyはAction / Reaction種別からRest deltaを返し、Collision AttackへMoveとAttackを二重加算しない。
- Generation Proto-OrderとOrder Benefitを別policyとして表現し、倍率を重ねない。
- Settlement Supportは日末のread-only 90日集計からP/R/Sを算出し、Membership変更と自然消滅をMaintenance順でcommitする。
- Settlement Pressureは日末のread-only 30日集計から3成分を算出し、High / Low counterとともに翌Tick stateへcommitする。InvasionはPressure値を所有せず、eligibility inputとして受け取る。
- Mobilizationはeligible snapshot、SettlementPressure、Affinity、named seedだけを使い、Combat / Action値やcollection順でcohortを歪めない。
- Observation cache、近傍index、CPU並列read phaseは最適化portの内側に閉じ、権威的順序・random stream・Event IDを変えない。
- Run envelopeはVersion、repositoryCommit、Config、Seedを一組として記録する。

## Open architecture work

- Realityのstate sliceとmutation ownership。
- tick内で各moduleが読めるsnapshotと書き込み権限。
- domain eventの配送順と失敗処理。
- LLMを使わない場合の文章生成fallback。
- module間の許可依存表とarchitecture test。

