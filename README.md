# ImeCenterView

IME のオン／オフを切り替えたときに、画面の中央へ「あ」または「A」を一瞬表示する Windows 用の常駐アプリです。
以前の Microsoft IME にあった「入力モードの切り替えを画面中央に表示する」機能を再現します。

- IME をオンにすると「あ」、オフにすると「A」を表示します
- 表示はフォーカスを奪わず、クリックも下のウィンドウへ通します
- 一定時間たつと、フェードアウトして消えます
- ウィンドウを切り替えただけでは表示しません

## 動作環境

- Windows 10 / 11（x64）
- [.NET 10 デスクトップ ランタイム](https://dotnet.microsoft.com/download/dotnet/10.0)

## インストール

配布用の exe は用意していないため、ソースからビルドします。ビルドには [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) が必要です（SDK にはランタイムも含まれます）。

1. リポジトリを取得し、そのフォルダーへ移動します

   ```powershell
   git clone https://github.com/shinshu68/ImeCenterView.git
   cd ImeCenterView
   ```

2. ビルドします。`src\ImeCenterView\bin\Release\net10.0-windows\win-x64\publish\` に `ImeCenterView.exe` ができます

   ```powershell
   dotnet publish src/ImeCenterView -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
   ```

3. `ImeCenterView.exe` を、置いたままにできる場所へコピーします。次の例では `%LOCALAPPDATA%\Programs\ImeCenterView` に置きます

   ```powershell
   New-Item -ItemType Directory -Force "$env:LOCALAPPDATA\Programs\ImeCenterView" | Out-Null
   Copy-Item src\ImeCenterView\bin\Release\net10.0-windows\win-x64\publish\ImeCenterView.exe "$env:LOCALAPPDATA\Programs\ImeCenterView\"
   ```

4. コピーした `ImeCenterView.exe` を起動します。タスクトレイに「あ」のアイコンが出れば起動しています

   ```powershell
   Start-Process "$env:LOCALAPPDATA\Programs\ImeCenterView\ImeCenterView.exe"
   ```

必要なファイルは `ImeCenterView.exe` だけです。ビルドしたフォルダーの exe をそのまま使うと、ビルドし直したときに消えたり置き換わったりするため、コピーして使ってください。

## 使い方

起動するとタスクトレイに常駐し、ウィンドウは開きません。そのまま IME を切り替えると、アクティブなウィンドウがあるモニターの中央に表示されます。

操作はタスクトレイのアイコンを右クリックして出るメニューから行います。

| メニュー | 動作 |
|---|---|
| 一時停止／再開 | 表示を止めます。もう一度選ぶと再開します |
| 設定 | 設定ウィンドウを開きます |
| スタートアップに登録 | サインインしたときに自動で起動するようにします。もう一度選ぶと解除します |
| 終了 | アプリを終了します |

### スタートアップへの登録

「スタートアップに登録」は、そのとき起動している `ImeCenterView.exe` の場所を記録します。exe を別の場所へ移した場合は、移した先の exe を起動し、登録を一度解除してから登録し直してください。

## 設定

設定ウィンドウで変更した内容は、すぐ表示に反映されます。見た目に関わる項目を変えると、確認用に「あ」が表示されます。

| 項目 | 既定値 | 範囲 | 内容 |
|---|---|---|---|
| 表示時間 | 700 ms | 100〜5000 ms | フェードアウトを始めるまで表示しておく時間 |
| フェード時間 | 300 ms | 0〜3000 ms | フェードアウトにかける時間。0 にするとすぐ消えます |
| サイズ | 162 | 50〜500 | 表示する四角の一辺の長さ。文字も合わせて拡大縮小します |
| 背景の不透明度 | 85 % | 10〜100 % | 四角の背景の濃さ。文字は常に不透明です |
| ポーリング間隔 | 100 ms | 50〜1000 ms | IME の状態を調べる間隔。短いほど切り替えから表示までが速くなります |
| フルスクリーンのアプリの実行中は表示しない | オフ | ― | ゲームや、ブラウザー・動画の全画面表示の間は表示しません |

「既定値に戻す」で、すべての項目が既定値に戻ります。

設定は `%APPDATA%\ImeCenterView\settings.json` に保存されます。

## 既知の制限

次の場合は IME の状態を取得できず、表示されません。誤った表示をしないことを優先しています。

- 管理者権限で動いているアプリのウィンドウ（本アプリを通常の権限で動かしている場合）
- コンソール系のアプリ（Windows Terminal など）
- 一部のストア アプリやゲーム

そのほかの制限は次のとおりです。

- ウィンドウを切り替えた直後は、切り替え先の状態を記録するだけで表示しません。そのウィンドウのまま IME を切り替えると表示されます
- 表示されるのはオン（「あ」）とオフ（「A」）の 2 種類です。カタカナや全角英数などの入力モードは区別しません

## アンインストール

1. 「スタートアップに登録」をオンにしている場合は、メニューからオフにします
2. メニューの「終了」でアプリを終了します
3. `ImeCenterView.exe` を置いたフォルダーを削除します
4. 設定も消す場合は、`%APPDATA%\ImeCenterView` フォルダーを削除します

## 開発

```powershell
dotnet build                             # ビルド
dotnet run --project src/ImeCenterView   # 実行
dotnet test                              # テスト
```

二重起動を防止しているため、すでに ImeCenterView が常駐していると、`dotnet run` で起動したほうはすぐ終了します。先に常駐しているほうを終了してください。

設計や開発の経緯は [docs/plan.md](docs/plan.md) と [CLAUDE.md](CLAUDE.md) にあります。

## ライセンス

[MIT License](LICENSE) です。
