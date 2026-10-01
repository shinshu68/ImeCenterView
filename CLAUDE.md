# CLAUDE.md

このファイルは Claude Code がこのリポジトリで作業する際のガイドです。

## プロジェクト概要

**ImeCenterView** は、IME のオン／オフが切り替わったときに、画面中央に四角い領域と「あ」または「A」を一瞬表示する Windows 常駐アプリです。
かつての Microsoft IME にあった「入力モード切り替え時に画面中央へ表示する」機能の再現を目的としています。

- IME オン → 「あ」を表示
- IME オフ → 「A」を表示
- 表示はフォーカスを奪わず、クリックも透過し、一定時間後にフェードアウトして消える

開発計画とタスクの詳細は `docs/plan.md` を参照してください。

## 応答・ドキュメントの言語

- ユーザーへの応答、コードコメント、コミットメッセージ、ドキュメントはすべて日本語で書く
- 識別子（クラス名・メソッド名・変数名）は英語

## 技術スタック

- 言語：C#（最新の言語バージョン、`Nullable` 有効）
- フレームワーク：.NET 10（`net10.0-windows`）、WPF
- タスクトレイ：`System.Windows.Forms.NotifyIcon`（csproj で `UseWindowsForms` を有効化）
- Win32 API：P/Invoke（`user32.dll`, `imm32.dll`）
- テスト：xUnit
- 対象 OS：Windows 10 / 11（x64）

## 開発環境

- エディタは **VS Code** を使う（Visual Studio は使わない）
- 必要なもの：.NET 10 SDK、VS Code 拡張機能「C# Dev Kit」（または「C#」）
- ビルド・実行・テストは `dotnet` CLI で行う。Visual Studio でしか使えない機能や手順（XAML デザイナー、`.vcxproj`、VS 専用の拡張など）に依存しない
- XAML は手書きで編集する。見た目の確認は実際に起動して行う
- デバッグ実行用に `.vscode/launch.json` と `.vscode/tasks.json` を用意する（フェーズ 0）

## ディレクトリ構成（予定）

```
ImeCenterView.sln
CLAUDE.md
docs/
  plan.md
src/ImeCenterView/
  ImeCenterView.csproj
  app.manifest              # Per-Monitor V2 DPI 対応
  App.xaml / App.xaml.cs    # エントリポイント、常駐処理、二重起動防止
  Native/
    NativeMethods.cs        # P/Invoke 定義をここに集約
  Ime/
    ImeState.cs             # IME 状態を表す型（On / Off / Unknown）
    IImeStateReader.cs
    ImeStateReader.cs       # Win32 API で IME 状態を取得
    IForegroundWindowProvider.cs
    ImeMonitor.cs           # ポーリングと変化検知
  Overlay/
    OverlayWindow.xaml(.cs) # 画面中央の表示ウィンドウ
    FullScreenDetector.cs   # フルスクリーンのアプリが実行中かの判定
  Tray/
    TrayIcon.cs             # タスクトレイアイコンとメニュー
    StartupRegistration.cs  # スタートアップ登録（HKCU...Run）
  Resources/
    tray.ico                # トレイアイコン（埋め込みリソース）
    app.ico                 # exe のアイコン（16〜256px。csproj の ApplicationIcon）
  Settings/
    AppSettings.cs
    SettingsStore.cs        # %APPDATA%\ImeCenterView\settings.json
    SettingsWindow.xaml(.cs) # 設定ウィンドウ（トレイメニューから開く）
  Diagnostics/              # Debug ビルド専用
    ResourceMonitor.cs      # ハンドル数・GDI/USER オブジェクト数・メモリの定期ログ
    StressTest.cs           # オーバーレイ連続表示によるリーク検証
tests/ImeCenterView.Tests/
  ImeMonitorTests.cs
```

## よく使うコマンド

```powershell
dotnet build                                   # ビルド
dotnet run --project src/ImeCenterView          # 実行
dotnet test                                    # テスト
dotnet publish src/ImeCenterView -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 設計上の重要ポイント

### IME 状態の取得

1. `GetForegroundWindow` でアクティブウィンドウを取得する
2. より正確にするため、`GetWindowThreadProcessId` と `GetGUIThreadInfo` でフォーカスを持つ子ウィンドウ（`hwndFocus`）を取得し、取れればそちらを使う
3. `ImmGetDefaultIMEWnd` で IME ウィンドウを取得する
4. `SendMessage(imeWnd, WM_IME_CONTROL (0x0283), IMC_GETOPENSTATUS (0x0005), 0)` の戻り値が 0 なら IME オフ
5. 0 以外（IME が開いている）なら、`IMC_GETCONVERSIONMODE (0x0001)` で入力モードも取得する。`IME_CMODE_NATIVE (0x0001)` が立っていなければ（英数モード）オフ、立っていればオン。入力モードが取れなければオンとする

5 が必要なのは、未確定の文字があるときに IME をオフにすると、IME は開いたまま入力モードだけが英数に変わるため（タスクバーの表示は「A」になり、確定した時点で閉じる）。開閉だけを見ていると、この切り替えを拾えない。

`ImmGetDefaultIMEWnd` が `IntPtr.Zero` を返した場合や取得に失敗した場合は `ImeState.Unknown` とし、表示は行わない。
`SendMessage` がハングしないよう、必要に応じて `SendMessageTimeout`（`SMTO_ABORTIFHUNG`）を使う。

### 変化検知のルール

- `DispatcherTimer` で 100ms ごとにポーリングする
- **フォアグラウンドウィンドウが前回と異なる場合は、状態を記録するだけで表示しない**（ウィンドウ切り替えだけで表示されるのを防ぐ）
- 同じウィンドウのまま状態が On ⇔ Off に変化したときだけ表示する
- `Unknown` への変化・`Unknown` からの変化では表示しない
- 判定ロジックは `IImeStateReader` / `IForegroundWindowProvider` を介して Win32 から切り離し、ユニットテストできるようにする

### オーバーレイウィンドウ

- `WindowStyle="None"`, `AllowsTransparency="True"`, `Background="Transparent"`, `Topmost="True"`, `ShowInTaskbar="False"`, `ShowActivated="False"`
- `SourceInitialized` で拡張スタイル `WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW` を付与する
- **絶対にフォーカスを奪わないこと**。入力中のアプリからフォーカスが外れると致命的な不具合になる
- ウィンドウは 1 つだけ生成して使い回す（表示のたびに new しない）
- 表示中に再度切り替わった場合は、文字を差し替えてタイマーとアニメーションをリセットする
- 位置はアクティブウィンドウがあるモニター全体（`MonitorFromWindow` + `GetMonitorInfo` の `rcMonitor`、タスクバーを含む）の中央（以前の Microsoft IME と同じ）。DPI の混在に備え、座標計算は物理ピクセルで行い `SetWindowPos`（`SWP_NOACTIVATE`）で配置する

### 表示仕様（デフォルト値）

| 項目 | 値 |
|---|---|
| 領域 | 正方形 162×162（DIP）、角丸なし |
| 背景 | グレー `#2F2F2F`、不透明度 85%（`#D92F2F2F`。白の上で 78、暗い灰色 30 の上で 44 になる） |
| 文字 | グレー `#DEDEDE`（不透明）。「あ」は Yu Gothic UI Regular 126、「A」は Yu Gothic Regular 137（DIP）。インクの下端を領域の下端から 27 にそろえる |
| 表示時間 | 700ms 表示後、300ms かけてフェードアウト |

領域の大きさ・背景の不透明度・表示時間・フェード時間・ポーリング間隔は設定（`AppSettings`）で変更でき、上の表はその既定値。大きさを変えると文字も比例して拡大縮小する。

設定で「フルスクリーンのアプリの実行中は表示しない」を有効にすると（既定は無効）、切り替えを検知したときにフルスクリーンのアプリが実行中なら表示しない。アクティブウィンドウがタイトルバーなし（`WS_CAPTION` なし）でモニター全体を覆っている場合（デスクトップは除く）、または `SHQueryUserNotificationState` が `QUNS_BUSY` / `QUNS_RUNNING_D3D_FULL_SCREEN` を返す場合をフルスクリーンとみなす（後者だけではブラウザーの全画面表示を拾えない）。判定は切り替えを検知したときだけ行い、ポーリングのたびには行わない。

### 常駐・その他

- メインウィンドウは持たない。`ShutdownMode="OnExplicitShutdown"` とし、トレイメニューの「終了」で終了する
- 名前付き `Mutex` で二重起動を防止する
- スタートアップ登録は `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` を使う（管理者権限不要）

### リソース管理（リーク防止）【最重要】

本アプリは長時間常駐し、100ms ごとに処理が走る。わずかなリークでも数日で大きく膨らむため、**ハンドル・メモリを確保したまま解放しない不具合を絶対に作らない**ことを最優先とする。

**Win32 ハンドル**

- `ImmGetDefaultIMEWnd` / `GetForegroundWindow` などが返す `HWND` は借用であり、解放不要（解放してはいけない）
- **`ImmGetContext` は使わない**。使う場合は必ず `ImmReleaseContext` と対にする必要があり、他プロセスのウィンドウでは失敗するため、状態取得は `WM_IME_CONTROL` 方式に統一する
- 解放が必要な Win32 リソース（`HICON`、`HDC`、`HBITMAP` など）を扱う場合は `SafeHandle` の派生クラスで包み、`using` で確実に解放する
- `Bitmap.GetHicon()` で作ったアイコンは `DestroyIcon` が必要。原則としてアイコンは `.ico` ファイル／埋め込みリソースから読み込み、`GetHicon` は使わない

**毎周期・毎表示で new しない**

- ポーリング処理（100ms ごと）の中で、オブジェクト・配列・文字列・デリゲート（ラムダ）を生成しない。構造体と使い回しのバッファで完結させる
  - ただし `DispatcherTimer` 自体が tick ごとに内部で約 500 バイトを確保する（フェーズ 1 で計測）。これは GC で回収される短命なオブジェクトでリークではないため、許容する。自分で書くコードの中で確保しないことを守る
- `DispatcherTimer`、`Storyboard` / `DoubleAnimation`、`Brush`、`OverlayWindow` はすべて起動時に 1 回だけ生成して使い回す
  - 設定で変わる `Brush` と `DoubleAnimation` だけは、設定が変わったとき（`OverlayWindow.ApplySettings`）に作り直す。表示のたびには作らない
- 生成して使い回す `Brush` などの Freezable は `Freeze()` する

**イベント購読**

- イベントハンドラ（`+=`）は初期化時に 1 回だけ登録する。**表示のたびに `Completed += ...` のような登録をしない**（ハンドラが積み重なって解放されなくなる）
- `SystemEvents`（`DisplaySettingsChanged`、`SessionSwitch`、`PowerModeChanged` など）は静的イベントのため、購読したら終了時に必ず `-=` で解除する
- `HwndSource.AddHook` で追加したフックは、ウィンドウ破棄時に `RemoveHook` する
- `SetWindowsHookEx` / `SetWinEventHook` を使う場合は、対応する `UnhookWindowsHookEx` / `UnhookWinEvent` を必ず呼び、コールバックのデリゲートは GC されないようフィールドに保持する

**破棄の責任**

- `IDisposable` を実装するクラス（`TrayIcon`、`ImeMonitor`、`Mutex`、`RegistryKey`、`NotifyIcon`、`Icon`、`ContextMenuStrip` など）は、所有者を明確にし、`using` または所有者の `Dispose` で必ず破棄する
- 終了処理は `App.OnExit` に集約し、破棄の順序（タイマー停止 → イベント解除 → トレイアイコン破棄 → Mutex 解放）を守る
- `NotifyIcon` は `Dispose` しないとトレイにアイコンの残骸が残るので、必ず破棄する

**確認方法**

- Debug ビルドでは、プロセスのハンドル数（`Process.HandleCount`）、GDI オブジェクト数・USER オブジェクト数（`GetGuiResources`）、プライベートメモリを定期的にログ出力する診断機能を持つ
- 実装や修正のたびに、`docs/plan.md` の「リソースリーク検証」の手順で、数値が増え続けないことを確認する
- 新しく Win32 API を使うときは、そのリソースに解放が必要かどうかをドキュメントで確認し、コードコメントに明記する

## 既知の制限

- 管理者権限で動いているウィンドウは、UIPI により通常権限の本アプリから状態を取得できない
- コンソール系（Windows Terminal 等）、一部の UWP アプリやゲームでは取得できないことがある
- これらは `Unknown` として扱い、誤表示しないことを優先する

## コーディング規約

- P/Invoke 定義は `Native/NativeMethods.cs` に集約し、他のファイルに `DllImport` / `LibraryImport` を散らさない
- 可能な限り `LibraryImport`（ソース生成）を使う
- Win32 呼び出しの失敗は例外にせず、`Unknown` などの値で呼び出し側に返す
- UI スレッドをブロックする処理を書かない
- 1 クラス 1 ファイル。public なメンバーには日本語の XML ドキュメントコメントを付ける

## 作業の進め方

- `develop` ブランチから作業ブランチ（`feature/xxx`, `fix/xxx`）を切って作業する
- `docs/plan.md` のフェーズ・タスク単位で進め、1 タスク終わるごとにユーザーの動作確認を待つ
- 動作確認が必要な項目（実際に IME を切り替えて見た目を確認するなど）は、確認手順を具体的に提示する
- ユーザーの確認が取れるまでコミットしない
- 計画に変更が生じたら `docs/plan.md` も更新する
