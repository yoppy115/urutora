# Repository tools

ゲーム挙動を所有しない、再現性・検証・release用の補助scriptを置く。

- `RepositoryProvenance.ps1`: buildへGit commitとtree stateを渡す共通関数。
- `Test-RepositoryHygiene.ps1`: 生成物の誤追跡、不要な`.gitkeep`、主要Markdownの壊れたlocal linkを検査する。
- `Finalize-GitBaseline.ps1`: 全test、staging安全確認、release commit、`v0.2.2` tag、clean確認を一続きで実行する。pushは行わない。
- `Finalize-GitBaseline.Admin.ps1`: `.git` ACLで通常processからfinalizeできない環境専用の昇格wrapper。対象はこのrepositoryのbranch、index、commit、tagだけで、OS設定を変更しない。
- `Finalize-GitBaseline.Admin.cmd`: Windows PowerShellの実行policyに阻まれず、上記wrapperを起動する入口。PowerShell 7は不要。

通常はrepository rootから次を実行する。

```powershell
.\tools\Test-RepositoryHygiene.ps1
.\tools\Finalize-GitBaseline.ps1
```

`.git`への書き込みをOSに拒否された場合、またはPowerShell script実行policyで拒否された場合だけ、次を実行してUACを承認する。

```powershell
& ".\tools\Finalize-GitBaseline.Admin.cmd"
```
