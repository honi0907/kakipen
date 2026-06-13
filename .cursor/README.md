# Cursor ルール（kakipen）

運用の全体像: [cursor-playbook PROJECT_WORKFLOW.md](https://github.com/honi0907/cursor-playbook/blob/master/docs/PROJECT_WORKFLOW.md)

## 構成

| 種類 | ファイル | 管理 |
|------|---------|------|
| **汎用** | `playbook-*.mdc` | [cursor-playbook](https://github.com/honi0907/cursor-playbook) から導入 |
| **kakipen 専用** | `kakipen-*.mdc` | このリポジトリ |

## 初回 / 更新（playbook ルール）

```powershell
# cursor-playbook を clone 済みとして
..\cursor-playbook\scripts\Install-CursorRules.ps1 -ProjectPath "C:\Users\k-mizukami\Desktop\kakipen"
```

`playbook-*.mdc` のみ上書き。`kakipen-*.mdc` は触らない。

## 学びの還流

- kakipen だけの内容 → `kakipen-*.mdc` に追記
- 汎用化できた → cursor-playbook の `rules/` / `docs/` に push

## 読む順番

1. cursor-playbook `docs/PROJECT_WORKFLOW.md`
2. `kakipen-architecture.mdc`
3. 子機 Hub / 再接続 → `kakipen-client-reconnect.mdc`
4. 作業対象に応じて `kakipen-build-run.mdc` / `kakipen-external-display.mdc` など
