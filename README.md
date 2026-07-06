# KakiMoni WinUI 3 — フルスタック再構築版

Experimental WinUI 3 implementation. `kakimoni-all` is reference only (not modified).

## Architecture

| Project | Role |
|---------|------|
| `KakiMoni.Core` | Shared models & protocol |
| `KakiMoni.Server` | ASP.NET Core + SignalR (in-process) |
| `KakiMoni.Host` | 親機ランチャー + WinUI コンパネ |
| `KakiMoni.Client` | 子機 (Setup + Writing) |
| `KakiMoni.Layout` | レイアウト専用機 |
| `KakiMoni.Host --save-viewer` | 保存データ一覧（単体インストーラーあり） |

**Node.js / WebView2 / HTML UI は不要** — サーバーは親機 exe 内の Kestrel で起動。

## Quick start (Debug)

```powershell
# 親機
cd C:\Users\k-mizukami\Desktop\kakipen\src\KakiMoni.Host
dotnet build -c Debug -p:Platform=x64
.\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\KakiMoni.Host.exe
```

1. 「サーバー起動」をクリック
2. 「コンパネを開く」で 10 席モニター

```powershell
# 子機
cd C:\Users\k-mizukami\Desktop\kakipen\src\KakiMoni.Client
dotnet build -c Debug -p:Platform=x64
.\bin\x64\Debug\net8.0-windows10.0.26100.0\win-x64\KakiMoni.Client.exe
```

1. `http://localhost:3000`、席 ID、背景を選択
2. 「サーバー接続」→「書き画面を起動」→ 描画

## Assets

背景画像: `assets/backgrounds/`  
選択肢画像: `assets/choices/`（コンパネのドロップダウンで選択 → **選択肢送信**）  
オーバーレイ: `assets/overlays/`  
ロゴ: `assets/logo/`

## Publish / Release

バージョン正本: `Directory.Build.props`（現在 **0.0.2**）。手順詳細: [BUILD_RULES.md](BUILD_RULES.md)

```powershell
cd C:\Users\k-mizukami\Desktop\kakipen
.\scripts\release.ps1
```

`dist/` に Release ビルド + **Setup インストーラー** + Portable ZIP（検証用）が出力される。GitHub Release は **アプリごと**（`host-v0.0.1` 等）に **Setup のみ** 公開。詳細: [BUILD_RULES.md](BUILD_RULES.md)

## Notes

- 日常開発は Debug exe で動作確認
- `dist/` は release ビルド後のみ存在（gitignore 済み）
- `[Startup]` ログで起動時間を Output ウィンドウで確認
- TIFF 背景は子機 WinUI のみ対応
