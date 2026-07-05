# Build Rules (kakipen)

WinUI 3 版 KakiMoni（Host / Client / Layout / 保存一覧）のビルド・リリース手順。

共通ルール（build 前のプロセス終了、パッチ 0–9 など）は cursor-playbook `playbook-common-app-rules.mdc` を参照。

## バージョン

- 正本: リポジトリルート `Directory.Build.props` の `<Version>`
- 現在: **0.0.3**
- パッチは 0–9。`X.Y.9` の次は `X.(Y+1).0`（`1.0.10` 不可）
- **リリース tag はアプリごと**: `host-vX.Y.Z` / `client-vX.Y.Z` / `layout-vX.Y.Z` / `saveviewer-vX.Y.Z`

## 日常ビルド（Debug）

```powershell
.\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Host -Run
.\.cursor\skills\kakipen-winui\scripts\BuildAndRun.ps1 -Target Client -Run
```

Platform は常に **x64**。ビルド前に実行中 exe を終了する。

## リリースビルド

**既定は変わったアプリだけ**（高速）。全4アプリまとめては `-Target All`。

```powershell
cd C:\Users\k-mizukami\Desktop\kakipen

# 親機だけ（SaveViewer も同時に Setup + GitHub Release）
.\scripts\release.ps1 -Target Host

# 子機だけ
.\scripts\release.ps1 -Target Client

# レイアウトだけ
.\scripts\release.ps1 -Target Layout

# 保存一覧だけ（Host を publish して SaveViewer の Setup のみ）
.\scripts\release.ps1 -Target SaveViewer

# 複数指定
.\scripts\release.ps1 -Target Host,Client

# 全4アプリ（初回・大きな変更時。約7〜10分）
.\scripts\release.ps1 -Target All
```

| `-Target` | publish | Setup + GitHub Release |
|-----------|---------|------------------------|
| `Host` | Host | Host + **SaveViewer**（同一 exe のため） |
| `Client` | Client | Client |
| `Layout` | Layout | Layout |
| `SaveViewer` | Host | SaveViewer のみ |
| `All` | 3種 | 4種すべて |

`KakiMoni.Core` / `KakiMoni.Server` を変えたときは **`-Target Host`**（必要なら Client も）。

内部で `publish-apps.ps1`（**Setup のみ・ZIP なし**）→ `installer/build-installers.ps1` を実行する。

Portable ZIP が必要なときだけ（ユーザー指示時）:

```powershell
.\scripts\release.ps1 -Target Host -WithPortableZip
```

ビルドのみ（push / GitHub なし）:

```powershell
.\scripts\release.ps1 -Target Host -DryRun
```

### 前提

- **Inno Setup 6**（`ISCC.exe`）。未インストール時は `installer/build-installers.ps1` が winget 導入を試みる
- 手動: `winget install --id JRSoftware.InnoSetup -e`

### 出力（`dist/`）

| 種類 | パス |
|------|------|
| 親機 publish | `dist/Host/KakiMoni.Host.exe` + **`KakiMoni.Host.pri`**（必須） |
| 子機 publish | `dist/Client/KakiMoni.Client.exe` |
| レイアウト publish | `dist/Layout/KakiMoni.Layout.exe` |
| **Setup（GitHub 公開・オンライン更新用）** | `dist/KakiMoni_*-${version}-Setup.exe`（全4種） |
| Portable ZIP | **既定では作らない**（`-WithPortableZip` 時のみ・GitHub 非公開） |

インストーラーは **publish 出力 + 実行に必要な `assets/`** を `{app}\assets` に同梱。バックアップ用の追加同梱は指示があるときだけ。

## 配布の最小化（重要）

アップデート時のダウンロード時間を短くするため、次を守る。

| 層 | 載せるもの |
|----|-----------|
| **GitHub Release** | 各アプリの **Setup.exe 1 ファイルのみ** |
| **オンライン更新** | 上記 Setup.exe を 1 本ダウンロード → インストーラー起動 |
| **作らない（既定）** | Portable ZIP、`All` ZIP、ソース tarball、余分なアセット |

追加のバックアップ成果物が必要なときは、**ユーザーが指示してから** `-WithPortableZip` や個別ファイルを用意する。

| Setup | インストール先（既定） |
|-------|----------------------|
| `KakiMoni_Host-*-Setup.exe` | `%ProgramFiles%\KakiMoni Host\` |
| `KakiMoni_Client-*-Setup.exe` | `%ProgramFiles%\KakiMoni Client\` |
| `KakiMoni_Layout-*-Setup.exe` | `%ProgramFiles%\KakiMoni Layout\` |
| `KakiMoni_SaveViewer-*-Setup.exe` | `%ProgramFiles%\KakiMoni Save Viewer\`（`KakiMoni.Host.exe --save-viewer` で起動） |

### 禁止・運用

- **部分リリース時**は対象アプリの `dist/` サブフォルダと Setup だけ削除して再生成（他アプリは残す）
- **`-Target All` のときだけ** `dist/` ごと削除して再生成
- **通常のリリースビルドは Setup のみ**（`release.ps1` 既定。ZIP は `-WithPortableZip` のときだけ）
- **GitHub Release / オンライン更新に載せるのは Setup のみ**
- `dist/` は `.gitignore` 済み

## リリース手順チェックリスト

1. `Directory.Build.props` の `<Version>` を上げる（`app.manifest` の `assemblyIdentity` も `X.Y.Z.0` に合わせる）
2. 実行中の `KakiMoni.*.exe` を終了
3. 変わったアプリだけ `.\scripts\release.ps1 -Target Host` 等を実行（全アプリなら `-Target All`）
4. Setup をクリーン環境にインストールし、親機→子機の疎通を確認
5. commit → push（`release.ps1` が実行する場合あり）
6. GitHub Release は **対象アプリ分だけ** 作成（各 Setup 1 ファイル）

```powershell
gh release create host-v0.0.1 `
  "dist/KakiMoni_Host-0.0.1-Setup.exe" `
  --title "KakiMoni Host v0.0.1" `
  --notes "WinUI 3 親機（サーバー同梱）"

gh release create client-v0.0.1 `
  "dist/KakiMoni_Client-0.0.1-Setup.exe" `
  --title "KakiMoni Client v0.0.1" `
  --notes "WinUI 3 子機"

gh release create layout-v0.0.1 `
  "dist/KakiMoni_Layout-0.0.1-Setup.exe" `
  --title "KakiMoni Layout v0.0.1" `
  --notes "WinUI 3 レイアウト専用機"

gh release create saveviewer-v0.0.1 `
  "dist/KakiMoni_SaveViewer-0.0.1-Setup.exe" `
  --title "KakiMoni Save Viewer v0.0.1" `
  --notes "WinUI 3 保存データ一覧"
```

## アプリ構成

| exe | 役割 |
|-----|------|
| KakiMoni.Host | 親機ランチャー + コンパネ + in-process サーバー |
| KakiMoni.Client | 子機（Setup + Writing） |
| KakiMoni.Layout | レイアウト専用機 |
| KakiMoni.Host `--save-viewer` | 保存一覧（単体配布は Save Viewer インストーラー） |

## オンライン更新

各ランチャーの「オンライン更新」が上記 GitHub Release から Setup を取得して起動する。

| アプリ | tag 接頭辞 | アセット |
|--------|-----------|---------|
| 親機 | `host-v` | `KakiMoni_Host-*-Setup.exe` |
| 子機 | `client-v` | `KakiMoni_Client-*-Setup.exe` |
| レイアウト | `layout-v` | `KakiMoni_Layout-*-Setup.exe` |
| 保存一覧 | `saveviewer-v` | `KakiMoni_SaveViewer-*-Setup.exe` |

- リポジトリ: `honi0907/kakipen`
- 開発ビルド（`src/KakiMoni.*/bin/...`）では適用をブロック
- トークン: 環境変数 `KAKIMONI_GITHUB_TOKEN`（プライベート repo 用）

## 関連

- [README.md](README.md)
- `installer/` — Inno Setup 定義
- `.cursor/rules/kakipen-build-run.mdc`
