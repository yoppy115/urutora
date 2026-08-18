# Player Observation

**Status:** Baseline constraints / Draft mechanics

## Baseline: current-first observation

プレイヤーが通常見るのは現在の表層状態である。

- マップ
- NPC、集団、勢力
- 現在の行動
- 目立った変化

真の原因、完全な内面、完全な因果、Realityの全履歴を普段のゲームプレイで掘り下げることは主目的にしない。

プレイヤーが「なんでこうなった？」と多少考えつつ、次の結果を見るために時間を進めたくなることを重視する。

## Baseline: numerical DNA and textual phenotype

内部数値は大量に存在してよいが、通常UIでは数値をそのまま公開しない。

```text
internal: riskPreference = 0.82
visible: 危険を恐れず、困難へ積極的に踏み込む。
```

数値をDNA、プレイヤーに見える文章を表現型として扱う。NPC紹介文、集団説明、勢力状態も同じ方針に従う。

## Baseline: pins

目立った出来事、人物、勢力変化を自動的にピンとして記録する。候補には次を含む。

- 即位、大規模戦闘、集団形成・分裂
- 疾病流行、大規模移住、覇権成立
- 重要人物の誕生・死亡
- 上位存在からの加護
- 上位存在の継承

ピンは現在観測の補助であると同時に、詩篇生成時の重要ログ索引になる。

## Boundary

- 製品UIの観測情報と、開発・デバッグ用の完全Reality表示を分ける。
- ピンは原因を断定せず、観測された重要な変化を示す。
- ピンや説明文の生成がSimulation Coreの結果を変更してはならない。

## v0.2 Desktop Application

v0はheadless Coreに加え、観測用Desktop Applicationを実装対象とする。AppはCoreを呼び出すだけでSimulation LogicやRealityの権威を持たない。

最低限、次を表示・操作できるようにする。

1. 64×64 World Map。NPC、Empty、闘争・生存・交流Landmarkを視覚的に識別できる。
2. 現在のYear / Day。
3. Pause、通常速度、複数の高速化段階。可能ならMax Speed。
4. 最近のBirth、Death、Attack、CollisionAttack、Communication、Reproduction、ConceptMark Eventのスクロールログ。

v0.2ではMap上でSettlement Centerと、必要に応じてCore / Influenceを識別可能にする。Current World Phase、Settlement数、主要Statistics、Invasion中Settlementも確認可能にする。Generation→Orderの条件、Settlement / Affiliation、暴力、Reproduction、Invasion、Concept / Auraの最低限統計は [`STATISTICS.md`](../architecture/STATISTICS.md) に従う。

v0.2.2以降、ConceptMark HolderをMap/UIで識別可能にし、Pause、1x、2x、3x、5x、10x、50xの速度段階を提供する。Mark旗、ボタン、色はPresentation detailであり、速度や描画頻度でSimulation結果を変えない。

v0.2.3以降、Settlementは不変IDで識別し、表示色は最大約60色の再利用可能paletteとして扱う。消滅済みSettlementは通常Mapから隠してよいがHistory / Statisticsから削除しない。Settlement詳細、Friction、NPCの行動履歴とKill countを観測可能にする。

v0.2.4ではCurrent Support、P/R/S、LowSupportDays、所属者のCore / Influence / 外部分布、Home Bias、Rest、Foreign movement、Invasion診断をSettlement詳細とStatisticsへ追加可能にする。

v0.2.5では、NPCが知らないPerson / Event / Settlement fieldを`0`や`false`に見せず`?`等のUnknownとして表現する。観測可能な詳細にはPerson Memory capacity / 使用率、category別Knowledge、SupportPotential / 累積Support / Renewal、Fission / 親子関係、Invasion participant state / front / 連続勝敗counterを追加できる。最近ログは有限buffer、長期表示はMilestone / Pin、統計は増分projectionから取得する。

診断UIではMandatory / Pin経路別EventBelief、SettlementBelief取得経路とKnown率、AggregatePersonConfidence、Fission Center選択、Migration完了経路、Expansion Indicatorsを表示可能にする。これは開発・観測projectionであり、NPCへ非公開Realityを渡したりExpansionをWorldPhaseとして表示したりするものではない。

具体倍率、配置、UI framework、デザインは実装時の裁量とする。

Simulation TickとUI Render Updateを分離する。高速進行では複数Tick後の最新Stateだけを描画してよく、render頻度、frame rate、UI操作がSimulation Event列を変えてはならない。自動テストはGUIなしで実行可能にする。

## Draft mechanics

- 数値から文章へ変換する区間、文法、優先順位。
- ピンの重要度計算、保存上限、統合、寿命。
- 同時に大量のピンが発生した場合の表示方法。
- プレイヤーが閲覧できる過去情報の範囲。
- v0最低要件を超える最終UIと操作方法。
