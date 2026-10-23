# Audio Defaults Sync

Windowsの**通常用と通話用の出力を連動**させ、**指定した入力デバイスを固定**する小さな常駐アプリです。

Windows右下でヘッドホンを切り替えたのにDiscordの音だけ前のヘッドホンから出る、再起動やVRアプリの利用で既定のマイクが変わる、といった場面に使えます。

## できること

- **出力の双方向同期：** 通常用・通話用のどちらを変更しても、最後に選んだデバイスへ両方をそろえます。Windows標準の出力切り替えも使えます。
- **入力の固定：** 通常用・通話用の両方を、セットアップで選んだマイクや仮想入力へ戻します。
- **ログイン時の自動起動：** チェックを入れると、そのWindowsユーザーのログイン時に起動します。
- **別PCでも再設定可能：** マイクを一覧から選ぶので、ソースコードの書き換えは不要です。
- **軽い待機処理：** Windowsの音声変更通知を受け取り、変更時に処理します。補助の再確認は既定で30秒ごとです。

音声を録音・加工する機能はありません。ネットワーク通信、利用状況の送信、自動更新も行いません。

## 対応環境

- Windows 10 / 11 **x64**、.NET Framework 4.8を想定しています。
- 通常のユーザー権限で使用します。ソースからのビルドにも.NET SDKやVisual Studioは必要ありません。
- 実機で確認したのはWindows 11 x64です。Windows 10やすべての音声ドライバーでの動作を保証するものではありません。
- このアプリを同時に複数起動することはできません。

## ダウンロードして使う

1. [Releases](https://github.com/skmtrd/audio-defaults-sync/releases/latest)から `AudioDefaultsSync-1.0.0-win-x64.zip` をダウンロードします。`Source code` は開発用です。
2. ZIPを、今後も置いておくフォルダーに**展開**します。例：`%LOCALAPPDATA%\Programs\AudioDefaultsSync`。ZIPの中から直接実行しないでください。
3. 展開した `AudioDefaultsSync.exe` をダブルクリックします。
4. 「固定する入力デバイス」で、使いたいマイクを選びます。VB-CABLEを使う場合は **CABLE Output (VB-Audio Virtual Cable)** を選びます。
5. 出力も連動させる場合は「通常用と通話用の出力を、最後の選択にそろえる」をオンにします。
6. 再起動後も使う場合は「Windowsへのログイン時に自動起動する」をオンにします。
7. 「保存して開始」を押します。設定画面が閉じ、右下の通知領域に常駐します。

実行ファイルはコード署名をしていません。公開されているソースから自分でビルドすることもできます。

### Discordなどのアプリ側

Discordの音声設定で、入力・出力をどちらも **Default（デフォルト）** にします。するとWindowsの通話用既定デバイスに追従する構成になります。

アプリ内で特定の機器を個別指定している場合は、その選択が優先されることがあります。このツールはWindowsの既定デバイスを変更します。

### 設定を変更する

通知領域のアイコンをダブルクリックするか、右クリック →「設定…」を開きます。アイコンが見えない場合は、右下の隠れているアイコンの一覧を確認してください。

実行ファイルをもう一度開いても、既に動いているアプリの設定を開きます。

### 別のPCへ移す

同じZIPを新しいPCで展開し、起動して、そのPCの入力デバイスを選び直してください。設定にはPCごとのデバイスIDが含まれるため、古いPCの `config.json` をコピーする必要はありません。

VB-CABLEを選ぶには、移行先にも[VB-CABLE](https://vb-audio.com/Cable/)を別途導入し、必要な音声の経路を設定してください。このアプリには音声ドライバーやミキサーは含まれていません。

## 停止・自動起動の解除・削除

- **今だけ停止：** 通知領域のアイコンを右クリック →「終了」。
- **自動起動を解除：** 設定画面で「Windowsへのログイン時に自動起動する」をオフにして保存し、必要ならアプリを終了します。
- **削除：** 自動起動を解除して終了したあと、展開したフォルダーを削除します。個人用設定も不要なら `%LOCALAPPDATA%\AudioDefaultsSync` を削除してください。

タスクスケジューラでは **Audio Defaults Sync** という名前です。アプリを起動できない場合は、このタスクを無効にして「終了」する方法でも停止できます。設定を戻す場合はアプリを起動し、自動起動のチェックを入れて保存してください。

フォルダーを移動した場合や別の場所へ更新版を展開した場合は、新しい場所の実行ファイルを起動して設定を保存し、自動起動の参照先も更新してください。

## 動作のしくみと制限

- Windows Core Audioの `IMMNotificationClient` で変更通知を受け取り、`IPolicyConfig` でConsole・Multimedia・Communicationsの3役割をそろえます。
- 自分自身による変更通知を区別し、通知の循環を防ぎます。短い待ち時間を入れ、まとめて届く変更通知を処理します。
- 起動時や通知を取りこぼした場合は、通常用（Multimedia）の出力を基準にそろえます。
- 固定した入力が切断・無効化されている間は固定できません。利用可能になった時点で戻します。IDが変わった場合は、同じ名前の入力が1つだけ存在するときに再認識します。同名が複数あるときは設定画面で選び直してください。
- VRアプリなどが既定のマイクを変更しても元へ戻すため、VRのマイクへ自動切り替えしたい場面では一時的に終了するか、固定先を変更してください。
- `IPolicyConfig` はWindowsの内部インターフェースです。Windowsの更新によって修正が必要になる場合があります。
- 音声の遅延・音質・各機器の音量設定を調整するツールではありません。

## 設定・ログの場所

`%LOCALAPPDATA%\AudioDefaultsSync` に保存します。

| ファイル | 内容 |
| --- | --- |
| `config.json` | 選択した入力、出力同期のオン・オフ、補助確認の間隔 |
| `monitor-status.json` | 最新の入力・出力の状態とプロセスID |
| `monitor-log.jsonl` | 実際に既定デバイスを直した記録。約512 KiBで世代交代 |
| `monitor-log.previous.jsonl` | 1世代前の変更ログ |
| `monitor-fatal.log` | 起動・実行を続けられないエラー |

これらはGitで追跡せず、配布ZIPにも含めません。ログには機器名・デバイスIDやローカルパスが含まれることがあるため、問い合わせ時は内容を確認してから共有してください。

## ソースからビルドする

Windows PowerShellでリポジトリのフォルダーを開き、次を実行します。

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
```

`dist\AudioDefaultsSync.exe` ができます。Windowsに付属する.NET FrameworkのC#コンパイラーを使用し、外部NuGetパッケージの取得はありません。

配布ZIPとSHA-256を作る場合：

```powershell
.\scripts\package.ps1 -Version 1.0.0
```

テストは同期方向・古い通知の除外・入力の再認識・設定の保存を検証します。自動テスト中にPCの既定の音声デバイスを変更しません。実機での変更通知・通話アプリの追従・再起動後の起動確認は別に行ってください。

## English summary

Audio Defaults Sync is a small Windows tray application that keeps normal and
communications playback defaults together, in either direction, while pinning
all recording roles to a selected input. Download the portable x64 ZIP from
Releases, extract it to a stable folder, run the EXE, choose an input and save.
The optional per-user logon task starts it automatically. No audio recording,
network access, telemetry, driver installation, or external NuGet dependencies.
Configuration is local to each PC. Use the tray menu to change settings or exit.

## ライセンス

[MIT License](LICENSE)。利用したCOM宣言の出典とライセンスは[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)を参照してください。
