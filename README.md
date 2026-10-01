# ER Collection Checker JP

**日本語** | [English](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/README.en.md)

PC Steam版『ELDEN RING』のセーブデータを解析し、武器、防具、魔術、祈祷、タリスマン、戦灰、
ジェスチャー、遺灰、結晶雫、霊馬装束などの収集状況を確認するWindowsデスクトップアプリです。

> [!IMPORTANT]
> 本アプリは`ER0000.sl2`を読み取り専用で解析し、セーブデータを書き換えません。
> ゲームプロセスのメモリにもアクセスしません。

> [!WARNING]
> PC Steam版・ゲームバージョン1.17専用です。本プロジェクトは非公式のコミュニティツールであり、
> FromSoftwareおよびBandai Namco Entertainmentとは関係ありません。
> 異なるバージョンのセーブデータは正しく解析できない可能性があります。
> 使用前にセーブデータをバックアップしてください。

## 主な機能

- 使用中のキャラクタースロットをキャラクター名付きで選択
- 全体とカテゴリ別の所持数・未所持数・進捗率を表示
- 「コレクション対象」と「取得可能な全アイテム」の2つの収集モード
- 日本語 / EnglishのUI切り替え
- 日本語名・英語名の表示と検索
- 状態、カテゴリ、Base Game / SHADOW OF THE ERDTREE / Tarnished Packによる絞り込み
- 列見出しクリックによる一覧の並べ替え
- 選択中アイテムの表示言語側の名称を［名称をコピー］または`Ctrl+C`でコピー
- 防具の直接所持と、別形態から変換できる状態を区別
- 開発者モードでDataOnly項目、判定メモ、Param ID、内部Keyを確認
- 最後に正常に読み込んだセーブファイルを次回起動時に復元

画面仕様の詳細は[WPF / MVVM UI](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/WPF_UI.md)を参照してください。

## 対応環境

- Windows 10 / Windows 11
- 64ビット（x64）CPU
- PC Steam版『ELDEN RING』
- ゲームバージョン1.17

## ダウンロードと起動

現在の最新版は**v0.1.1**です。

1. [GitHub Releases](https://github.com/saraefcat/ERCollectionCheckerJP/releases/latest)から
   `ERCollectionCheckerJP-v0.1.1-win-x64.zip`をダウンロードします。
2. ZIPを任意の新しいフォルダーへ完全に展開します。
3. ZIPビューア内から直接実行せず、展開先の`ERCollectionCheckerJP.exe`を起動します。

配布ZIPは.NET 10 Desktop Runtimeを同梱した自己完結型です。.NET Runtimeを別途インストールする
必要はありません。インストーラーはありません。

実行ファイルはコード署名されていないため、Windowsが発行元に関する警告を表示する場合があります。
GitHub Releasesから入手したファイルであることを確認し、必要に応じて次の手順でSHA-256を確認してください。

### SHA-256を確認する

ZIPを保存したフォルダーでWindows PowerShellを開き、次を実行します。

```powershell
Get-FileHash .\ERCollectionCheckerJP-v0.1.1-win-x64.zip -Algorithm SHA256
```

表示された`Hash`を、Releaseにある`ERCollectionCheckerJP-v0.1.1-win-x64.zip.sha256.txt`の値と比較してください。

## 基本的な使い方

1. ［ファイルを選択］から`ER0000.sl2`を選択します。
2. セーブ内の使用中キャラクターを選択します。
3. ［収集モード］を選びます。
4. ［解析］を押します。
5. 検索、状態、カテゴリ、コンテンツの条件で一覧を絞り込みます。
6. 必要なアイテムの行を選択します。
7. 名称をコピーする場合は［名称をコピー］を押すか、`Ctrl+C`を使用します。

行を選択しただけではクリップボードを書き換えません。列見出しをクリックすると一覧を並べ替えられます。

標準的なセーブファイルの場所は次のとおりです。`<SteamID>`の部分は環境ごとに異なります。

```text
%APPDATA%\EldenRing\<SteamID>\ER0000.sl2
```

フォルダーではなく、［ファイルを選択］から実際の`ER0000.sl2`を指定してください。
`%APPDATA%\EldenRing`以下に`ER0000.sl2`が1件だけ存在する場合は、起動時の候補として自動検出します。

## 新しいバージョンへ更新する

1. ER Collection Checker JPを終了します。
2. 新しいRelease ZIPをダウンロードします。
3. 旧版とは別の新しいフォルダーへ完全に展開します。
4. 新しい`ERCollectionCheckerJP.exe`を起動します。
5. 正常に動作することを確認します。
6. 問題がなければ旧版のフォルダーを削除できます。

旧版のフォルダーへ上書き展開するのではなく、新しいフォルダーへの展開を推奨します。
設定はアプリのフォルダー外にある次のファイルへ保存されるため、通常はそのまま引き継がれます。

```text
%LOCALAPPDATA%\ERCollectionCheckerJP\settings.json
```

現在保存している主な設定は、最後に正常に使用したセーブファイルのパスです。
設定ファイルが存在しない、破損している、または保存済みのセーブが見つからない場合は、既定値と自動検出へ戻ります。

## 収集モード

### コレクション対象

恒久的に所持状態を確認できる武器、防具、魔術、祈祷、タリスマン、戦灰、ジェスチャー、
遺灰、結晶雫、霊馬装束など**1,940件**を対象にします。

消費や交換後にも「一度取得した」ことを安全に判定できないGoodsは、未所持へ誤分類せず
`分類保留`として進捗対象外にします。

### 取得可能な全アイテム

ゲーム進行上取得可能とレビューされた**2,768件**を対象にします。コレクション対象で保留している
Goodsも含むため、より広い確認向けのモードです。

ゲームデータ上にだけ存在し、通常プレイでは取得できないDataOnly **599件**は、どちらのモードでも
進捗へ含めません。開発者モードの［DataOnlyを表示］で存在と除外理由だけを確認できます。

未解析または確認不足の情報は、未所持ではなく`未判定`として扱います。

判定方針の詳細は[判定ルール](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DETECTION_RULES.md)と
[Goods分類](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GOODS_CLASSIFICATIONS.md)を参照してください。

## セーブデータの安全性

本アプリはセーブエディタではありません。

- `.sl2`を`FileAccess.Read`で開き、変更しません
- セーブを書き戻すAPIを持ちません
- ゲームプロセスのメモリを参照しません
- 1回のスナップショット取得では、読み取り前後のファイルサイズと最終更新日時を確認します
- 読み込んだ内容のSHA-256を計算し、解析前後に取得した2つのスナップショットの内容も比較します
- 読み取り中または解析中にセーブが変化した場合、その結果を採用しません
- ゲーム原本、`regulation.bin`、Oodle、外部のデータ生成ツールを実行時に使用しません

重要なセーブデータについては、本アプリに限らず外部ツールを使う前にバックアップすることを推奨します。

## 現在の制限

- ゲームバージョン1.17以外は未対応
- セーブ更新の自動監視・自動再解析は未実装
- CSV / JSON exportは未実装
- キャラクターのレベル、プレイ時間、周回数表示は未実装
- 一部Goodsは永続的な取得済み判定方法を確認できるまで、コレクション対象で分類保留
- インストーラーは提供せず、自己完結型ZIPで配布
- 実行ファイルはコード署名なし

## 困ったとき

### セーブファイルが見つからない

標準的な場所は`%APPDATA%\EldenRing\<SteamID>\ER0000.sl2`です。`<SteamID>`は環境ごとに異なります。
［ファイルを選択］から`ER0000.sl2`を直接指定できます。

### 解析できない

- PC Steam版のセーブか確認してください。
- ゲームバージョンが1.17か確認してください。
- ゲームがセーブを書き込み中の場合は、少し待ってから再度解析してください。
- 読み取り中または解析中にファイルの変更を検出した場合、安全のため結果を採用しません。

### Windowsから警告が出る

実行ファイルがコード署名されていないため、発行元に関する警告が表示される場合があります。
[GitHub Releases](https://github.com/saraefcat/ERCollectionCheckerJP/releases/latest)から入手したことを確認し、
必要に応じて[SHA-256を確認する](#sha-256を確認する)の手順を実行してください。

### 不具合を報告する

通常の不具合は[GitHub Issues](https://github.com/saraefcat/ERCollectionCheckerJP/issues)へ報告してください。
実物のセーブファイル、Steam ID、キャラクター名などの個人情報を公開Issueへ貼らないでください。
セキュリティ上の問題は[セキュリティ方針](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/SECURITY.md)に従って報告してください。

## 開発者向け

### ソースから起動する

> [!NOTE]
> 公開リポジトリにはゲーム由来のRuntimeデータを含めていません。そのため、クローンした公開ソースだけでは
> 完全なアプリをビルドできません。以下は非公開Runtimeデータを正規の位置へ配置した保守者向けの手順です。

```powershell
dotnet restore ERCollectionCheckerJP.sln
dotnet run --project src/ERCollectionCheckerJP.App -c Release
```

アプリ起動時に、同梱された1.17用Runtimeデータのmanifest、schema、ファイルサイズ、SHA-256、相互参照を
検証します。検証に失敗した場合は解析画面を開きません。

### ビルドとテスト

```powershell
dotnet build ERCollectionCheckerJP.sln -c Release
dotnet test ERCollectionCheckerJP.sln -c Release --no-build
```

カバレッジは`.\tools\Test-WithCoverage.ps1`で取得でき、Git管理外の`TestResults/Coverage/`へ出力されます。
通常Solutionには製品コード、自動テスト、読み取り専用の`SaveDiagnostics`を含みます。
完全なアプリとデータ依存テストには、管理者が別途保持する非公開データが必要です。その配置先は
MSBuildプロパティ`ERCollectionCheckerJPPrivateDataRoot`で指定できます。

### 自己完結型配布物の作成

次のスクリプトは、クリーンなGit作業ツリーとReleaseテストを確認してから、自己完結・単一EXEの
win-x64配布物、ZIP、SHA-256をリリース出力先へ生成します。

```powershell
.\tools\Publish-Release.ps1
```

版番号を指定する場合は、例えば`.\tools\Publish-Release.ps1 -Version 0.1.1`とします。
配布物の`SOURCE_COMMIT.txt`には発行元コミットが、`RUNTIME_DATA.txt`にはゲームバージョン、Runtimeファイル数、
集約SHA-256、使用した.NET SDKと同梱.NET Runtimeのバージョンが記録されます。生のRuntime JSONやゲームファイルは
含みません。詳しくは[リリース手順](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/RELEASE.md)を参照してください。

### プロジェクト構成

```text
src/
├─ ERCollectionCheckerJP.App/          WPF / MVVM画面
├─ ERCollectionCheckerJP.Application/  解析ユースケースと進捗集計
├─ ERCollectionCheckerJP.Domain/       判定モデル
├─ ERCollectionCheckerJP.ItemDatabase/ 同梱データの検証・合成
└─ ERCollectionCheckerJP.SaveParser/   読み取り専用セーブ解析
tests/                                 自動テスト
tools/
├─ ERCollectionCheckerJP.SaveDiagnostics/ 読み取り専用診断
└─ Publish-Release.ps1                    自己完結型リリース作成
docs/                                  設計・監査資料
```

ゲームデータの抽出・生成ツール、ゲーム原本、Runtimeデータ、共有セーブfixture、リリース成果物は
公開リポジトリに含みません。

### 同梱データについて

同梱のアイテムデータは、正規に取得したゲームデータを基に、非公開のオフライン保守工程で生成・検証しています。
ゲーム本体のファイルやセーブデータは配布物に含めません。

Runtime JSONは実行ファイルへ埋め込み、配布フォルダーには個別ファイルとして配置しません。起動時にmanifest、schema、
ファイルサイズ、SHA-256、相互参照を検証してメモリへ読み込んだ後、一時展開物を削除します。異常終了時に残った
アプリ専用の一時ディレクトリは、24時間経過後の次回起動時に削除します。外部ツールやゲーム原本へのRuntimeアクセスは
行いません。実行ファイルへの埋め込みは配布物を見やすくするためのものであり、データを暗号化・秘匿する仕組みではありません。

## 開発資料

- [設計](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DESIGN.md)
- [Application統合境界](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/APPLICATION_INTEGRATION.md)
- [データパイプライン](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/DATA_PIPELINE.md)
- [アイテムデータベース](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ITEM_DATABASE.md)
- [セーブ形式調査](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/SAVE_FORMAT_NOTES.md)
- [テスト戦略](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/TEST_STRATEGY.md)
- [防具変換](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ARMOR_CONVERSIONS.md)
- [ジェスチャー対応表](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GESTURE_MAPPINGS.md)
- [Goods分類](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/GOODS_CLASSIFICATIONS.md)
- [第三者通知](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/THIRD_PARTY_NOTICES.md)
- [アプリ資産の来歴](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/ASSET_PROVENANCE.md)
- [リリース手順](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/RELEASE.md)
- [セキュリティ方針](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/SECURITY.md)
- [変更履歴](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/CHANGELOG.md)

## ライセンスと免責

本プロジェクトのオリジナルソースコードおよび文書は[MIT License](LICENSE)で公開します。
実行ファイルに埋め込まれたゲーム由来データ、ゲームの名称・商標、その他の第三者著作物にはMIT Licenseを適用しません。
詳細は[THIRD_PARTY_NOTICES.md](https://github.com/saraefcat/ERCollectionCheckerJP/blob/main/docs/THIRD_PARTY_NOTICES.md)を参照してください。

本プロジェクトは非公式のコミュニティツールであり、FromSoftwareおよびBandai Namco Entertainmentとは関係ありません。
『ELDEN RING』および関連する名称・商標の権利は、それぞれの権利者に帰属します。
