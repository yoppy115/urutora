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

## v0 Desktop Application

v0はheadless Coreに加え、観測用Desktop Applicationを実装対象とする。AppはCoreを呼び出すだけでSimulation LogicやRealityの権威を持たない。

最低限、次を表示・操作できるようにする。

1. 64×64 World Map。NPC、Empty、闘争・生存・交流Landmarkを視覚的に識別できる。
2. 現在のYear / Day。
3. Pause、通常速度、複数の高速化段階。可能ならMax Speed。
4. 最近のBirth、Death、Attack、CollisionAttack、Communication、Reproduction、ConceptMark Eventのスクロールログ。

具体倍率、配置、UI framework、デザインは実装時の裁量とする。

Simulation TickとUI Render Updateを分離する。高速進行では複数Tick後の最新Stateだけを描画してよく、render頻度、frame rate、UI操作がSimulation Event列を変えてはならない。自動テストはGUIなしで実行可能にする。

## v0観測実験用の管理・分析view

**Status:** Baseline for v0 observation tooling

- Appは独立した初期世界を生成する `世界生成` commandを持つ。生成単位を世界ライフサイクル上の「次世界」と混同せず、観測runとしてrelease version内で連番のWorld IDを付ける。versionが変わると0001から開始する。
- NPCは世界内で一意かつ出生後も再利用しない数値IDを持つ。Map上のNPCを選択すると、そのID、現在位置、生死、年齢、Base / Effective能力、HP、Needs、ConceptMark、親子ID、およびNPCが主体または対象になった既存Eventの行動履歴をread-onlyで確認できる。行動履歴は移動の反復で重要Eventが埋まらないよう `Move` と `MoveFailed` を除外し、その他のEvent種別や成否を変更しない。
- 世界全体について、人口、平均年齢、死因別死亡数・比率・平均死亡年齢、現在の生存NPCの年齢分布、ActionIntentとして選択された行動commandの累積回数と比率を表示する。人口と平均年齢は時系列graphでも表示する。
- NPC詳細と世界統計はv0の検証・デバッグ用分析viewであり、最終製品UIが常に内部数値を公開するという仕様にはしない。
- これらのprojection取得、選択、graph描画、表更新、ログ保存はSimulation結果、Event列、乱数streamを変更しない。

## Draft mechanics

- 数値から文章へ変換する区間、文法、優先順位。
- ピンの重要度計算、保存上限、統合、寿命。
- 同時に大量のピンが発生した場合の表示方法。
- プレイヤーが閲覧できる過去情報の範囲。
- v0最低要件を超える最終UIと操作方法。
