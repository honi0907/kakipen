---
name: kakipen-winui
description: KakiMoni (kakipen) WinUI 3 開発。Host/Client UI、SignalR 連携、x64 ビルド、背景アセット。WinUI/XAML/コンパネ/子機画面の実装・修正・レビュー時に使う。
---

# KakiMoni WinUI 開発

kakipen は WinUI 3 + in-process SignalR の親子端末アプリ。**Microsoft win-dev-skills と併用**する。

## 最初に読むもの

1. 汎用ルール: [cursor-playbook](https://github.com/honi0907/cursor-playbook) の `playbook-*.mdc`（`.cursor/README.md` 参照）
2. プロジェクトルール: `.cursor/rules/kakipen-architecture.mdc`, `kakipen-build-run.mdc`, `kakipen-client-reconnect.mdc`（子機 Hub 再接続）
2. Microsoft スキル（ユーザー環境 `~/.cursor/skills/`）:
   - UI 作業 → **`winui-design`**
   - ビルド知識 → **`winui-dev-workflow`**（実行は下記スクリプトを優先）
   - 品質 → **`winui-code-review`**, **`winui-ui-testing`**

## ビルド & 実行

```powershell
# リポジトリルートから
.\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Both          # ビルドのみ
.\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Host -Run    # 親機ビルド+起動
.\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Client -Run  # 子機ビルド+起動
```

`-Configuration Release` も可。Platform は常に **x64**。

## 画面マップ

| 画面 | プロジェクト | ファイル |
|------|-------------|---------|
| ランチャー | Host | `MainPage.xaml` |
| コンパネ | Host | `CompanelPage.xaml`, `Controls/SeatCardView.*` |
| セットアップ | Client | `SetupPage.xaml` |
| 書き画面 | Client | `WritingPage.xaml` |

## WinUI 作業チェックリスト

- [ ] Fluent / テーマリソース（`ThemeResource`）を優先。ハードコード色は接続ランプ等の意味色のみ
- [ ] Hub 変更後は Host 再起動をユーザーに伝える
- [ ] 子機再接続を触るときは `kakipen-client-reconnect.mdc`（`DetachedFromDispose` をループ条件にしない）
- [ ] `SeatClientState.Strokes` など JSON 復元プロパティは `{ get; set; }`
- [ ] 背景ファイルは `assets/backgrounds/`（`BG_ID{n}.png` 規則）
- [ ] 描画は Win2D Canvas。Hub イベントは `WireHub` / `HostHubService` パターンに合わせる
- [ ] スコープ最小: UI 改善で Server プロトコルを変えない（必要なら Core + Server + 両 Client をセットで）

## 参照 README

- リポジトリ: `README.md`
- 背景配置: `assets/backgrounds/README.md`
