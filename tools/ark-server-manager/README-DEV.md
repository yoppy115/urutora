# ARK Server Manager source

Codexが作成したARK管理ツールの保守用ソースです。利用者向けファイルは `outputs/ARK Server Manager` にあります。

## 構成

- `src/ArkServerManager` — Windows管理アプリとスマホ操作サーバー
- `src/ArkDinoSearch` — 保存データから恐竜を検索する補助アプリ
- `src/ResourceProbe` — 資源量を取得するArkApiプラグイン
- `tests/ArkServerManager` — 日時設定とスマホAPIの統合テスト
- `tests/Diagnostics` — ResourceProbe・コンソール接続の診断用コード
- `vendor` — 再ビルド用のArkApiヘッダー、C++ツール、Python依存物、導入パッケージ
- `build.ps1` — 管理アプリの再現可能なビルド

## 管理アプリのビルド

PowerShellでプロジェクト直下から次を実行します。

```powershell
.\build.ps1 -RunTests
```

成果物は既定で `build/ARK Server Manager.exe` に作成されます。日時設定ロジックは
`ScheduleLogic.cs` に分離され、PC画面とスマホAPIで共通利用します。

ResourceProbeは次で再ビルドできます。

```powershell
.\build-resourceprobe.ps1
```

## 配布時の注意

本体を置き換える前にARKサーバーと管理アプリの両方が停止していることを確認します。
`ARK Dino Search.exe`、スマホ接続設定ファイル、ResourceProbe管理フォルダーは本体と同じ配布フォルダーに残します。
