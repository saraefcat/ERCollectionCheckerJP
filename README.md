# ER Collection Checker JP

PC Steam版『ELDEN RING』のセーブデータを読み取り専用で解析し、武器、防具、魔術、祈祷、
タリスマン、戦灰、ジェスチャー、遺灰、結晶雫、霊馬装束などの収集状況を確認する
Windowsデスクトップアプリです。

> [!IMPORTANT]
> 対応するのはPC Steam版・ゲームバージョン1.17です。異なるバージョンのセーブデータは
> 正しく解析できない可能性があります。使用前にセーブデータをバックアップしてください。

## 主な機能

- `ER0000.sl2`を読み取り専用で解析
- 使用中のキャラクタースロットをキャラクター名付きで選択
- 全体とカテゴリ別の所持数・未所持数・進捗率を表示
- 「コレクション対象」と「取得可能な全アイテム」の2つの収集モード
- 日本語名と英語名の表示・検索
- 状態、カテゴリ、Base Game / SHADOW OF THE ERDTREE / Tarnished Packによる絞り込み
- 列見出しクリックによる一覧の並べ替え
- 行選択、「名称をコピー」ボタン、`Ctrl+C`によるアイテム名のコピー
- 防具の直接所持と、別形態から変換できる状態を区別
- 開発者モードでDataOnly項目、判定メモ、Param ID、内部Keyを確認
- 最後に正常に読み込んだセーブファイルを次回起動時に復元

画面仕様の詳細は[WPF / MVVM UI](docs/WPF_UI.md)を参照してください。

## セーブデータの安全性

本アプリはセーブエディタではありません。

- `.sl2`を変更しません
- セーブを書き戻すAPIを持ちません
- ゲームプロセスのメモリを参照しません
- 解析前後のSHA-256を比較し、読み取り中にセーブが変化した場合は結果を採用しません
- ゲーム原本、`regulation.bin`、Oodle、外部のデータ生成ツールを実行時に使用しません

重要なセーブデータについては、本アプリに限らず外部ツールを使う前にバックアップすることを推奨します。

## 対応環境

- Windows 10 / Windows 11
- 64ビット（x64）CPU
- PC Steam版『ELDEN RING』
- ゲームバージョン1.17

一般利用者向け配布物は.NET 10 Desktop Runtimeを同梱した自己完結型です。.NETの別途インストールは
必要ありません。ZIPを任意のフォルダへ展開し、`ERCollectionCheckerJP.exe`を起動します。

現在の配布物はコード署名されていません。Windowsが発行元の警告を表示する場合があります。
配布ページに掲載されたSHA-256と、同梱の`SHA256SUMS.txt`を確認してから使用してください。

## ソースから起動する

> [!NOTE]
> 公開リポジトリにはゲーム由来のRuntimeデータを含めていません。そのため、クローンした
> 公開ソースだけでは完全なアプリをビルドできません。以下は非公開Runtimeデータを正規の位置へ
> 配置した保守者向けの手順です。

```powershell
dotnet restore ERCollectionCheckerJP.sln
dotnet run --project src/ERCollectionCheckerJP.App -c Release
```

アプリ起動時に、同梱された1.17用Runtimeデータのmanifest、schema、ファイルサイズ、SHA-256、
相互参照を検証します。検証に失敗した場合は解析画面を開きません。

## 基本的な使い方

1. ［ファイルを選択］から`ER0000.sl2`を選択します。
2. セーブ内の使用中キャラクターを選択します。
3. ［収集モード］を選びます。
4. ［解析］を押します。
5. 検索、状態、カテゴリ、コンテンツの条件で一覧を絞り込みます。
6. 行を選択すると、現在の表示言語のアイテム名がクリップボードへコピーされます。

`%APPDATA%\EldenRing`以下に`ER0000.sl2`が1件だけ存在する場合は、起動時の候補として自動検出します。
最後に正常に読み込んだセーブパスは、次のファイルへ保存します。

```text
%LOCALAPPDATA%\ERCollectionCheckerJP\settings.json
```

設定ファイルが存在しない、破損している、または保存済みセーブが見つからない場合は、既定値と自動検出へ戻ります。

## 収集モード

### コレクション対象

恒久的に所持状態を確認できる武器、防具、魔術、祈祷、タリスマン、戦灰、ジェスチャー、
遺灰、結晶雫、霊馬装束など1,940件を対象にします。

消費や交換後にも「一度取得した」ことを安全に判定できないGoodsは、未所持へ誤分類せず
`分類保留`として進捗対象外にします。

### 取得可能な全アイテム

ゲーム進行上取得可能とレビューされた2,768件を対象にします。コレクション対象で保留している
Goodsも含むため、より広い確認向けのモードです。

ゲームデータ上にだけ存在し、通常プレイでは取得できない599件は、どちらのモードでも進捗へ
含めません。開発者モードの［DataOnlyを表示］で存在と除外理由だけを確認できます。

判定方針の詳細は[判定ルール](docs/DETECTION_RULES.md)と
[Goods分類](docs/GOODS_CLASSIFICATIONS.md)を参照してください。

## 現在の制限

- ゲームバージョン1.17以外は未対応
- セーブ更新の自動監視・自動再解析は未実装
- CSV / JSON exportは未実装
- キャラクターのレベル、プレイ時間、周回数表示は未実装
- 一部Goodsは永続的な取得済み判定方法を確認できるまで、コレクション対象で分類保留
- インストーラーは提供せず、自己完結型ZIPで配布
- 実行ファイルはコード署名なし

未解析または確認不足の情報は、未所持ではなく`未判定`として扱います。

## ビルドとテスト

```powershell
dotnet build ERCollectionCheckerJP.sln -c Release
dotnet test ERCollectionCheckerJP.sln -c Release --no-build
```

カバレッジを取得する場合は、決定的ビルド用のsource path変換を一時的に無効化する次のスクリプトを
使用します。結果はGit管理外の`TestResults/Coverage/`へ出力されます。

```powershell
.\tools\Test-WithCoverage.ps1
```

通常Solutionには製品コード、自動テスト、読み取り専用の`SaveDiagnostics`を含みます。
ゲームデータの抽出・生成ツール、ゲーム原本、Runtimeデータは、このリポジトリに含みません。
完全なアプリとデータ依存テストをビルドするには、管理者が別途保持する非公開データが必要です。
その配置先はMSBuildプロパティ`ERCollectionCheckerJPPrivateDataRoot`で指定できます。

### 自己完結型配布物の作成

次のスクリプトは、クリーンなGit作業ツリーとReleaseテストを確認してから、自己完結・単一EXEの
win-x64配布物、ZIP、SHA-256を外側の`artifacts/releases/`へ生成します。

```powershell
.\tools\Publish-Release.ps1
```

版番号を変更する場合は、例えば`.\tools\Publish-Release.ps1 -Version 0.1.0`と指定します。
配布物の`SOURCE_COMMIT.txt`には発行元コミットが記録されます。
`RUNTIME_DATA.txt`にはゲームバージョン、Runtimeファイル数、内容を識別する集約SHA-256、
使用した.NET SDKと同梱.NET Runtimeのバージョンが記録されます。生のRuntime JSONやゲームファイルは
含みません。
公開手順と確認事項は[リリース手順](docs/RELEASE.md)を参照してください。

## プロジェクト構成

```text
src/
├─ src/
│  ├─ ERCollectionCheckerJP.App/          WPF / MVVM画面
│  ├─ ERCollectionCheckerJP.Application/  解析ユースケースと進捗集計
│  ├─ ERCollectionCheckerJP.Domain/       判定モデル
│  ├─ ERCollectionCheckerJP.ItemDatabase/ 同梱データの検証・合成
│  └─ ERCollectionCheckerJP.SaveParser/   読み取り専用セーブ解析
├─ tests/                                 自動テスト
├─ tools/
│  ├─ ERCollectionCheckerJP.SaveDiagnostics/    読み取り専用診断
│  └─ Publish-Release.ps1                        自己完結型リリース作成
└─ docs/                                  設計・監査資料
```

このGitリポジトリのルートは、外側のプロジェクトフォルダにある`src`ディレクトリです。
ゲーム原本、Runtimeデータ、共有セーブfixture、参考資料、ビルド・リリース成果物はGit管理対象外です。

## 同梱データについて

同梱のアイテムデータは、正規に取得したゲームデータを基に、非公開のオフライン保守工程で
生成・検証しています。ゲーム本体のファイルやセーブデータは配布物に含めません。

Runtime JSONは実行ファイルへ埋め込み、配布フォルダには個別ファイルとして配置しません。起動時に
manifest、schema、ファイルサイズ、SHA-256、相互参照を検証してメモリへ読み込んだ後、一時展開物を
削除します。異常終了時に残ったアプリ専用の一時ディレクトリは、24時間経過後の次回起動時に削除します。
外部ツールやゲーム原本へのRuntimeアクセスは行いません。なお、実行ファイルへの埋め込みは配布物を
見やすくするためのものであり、データを暗号化・秘匿する仕組みではありません。

## 開発資料

- [設計](docs/DESIGN.md)
- [Application統合境界](docs/APPLICATION_INTEGRATION.md)
- [データパイプライン](docs/DATA_PIPELINE.md)
- [アイテムデータベース](docs/ITEM_DATABASE.md)
- [セーブ形式調査](docs/SAVE_FORMAT_NOTES.md)
- [テスト戦略](docs/TEST_STRATEGY.md)
- [防具変換](docs/ARMOR_CONVERSIONS.md)
- [ジェスチャー対応表](docs/GESTURE_MAPPINGS.md)
- [Goods分類](docs/GOODS_CLASSIFICATIONS.md)
- [第三者通知](docs/THIRD_PARTY_NOTICES.md)
- [アプリ資産の来歴](docs/ASSET_PROVENANCE.md)
- [リリース手順](docs/RELEASE.md)
- [セキュリティ方針](SECURITY.md)
- [変更履歴](CHANGELOG.md)

## ライセンスと免責

本プロジェクトのオリジナルソースコードおよび文書は[MIT License](LICENSE)で公開します。
実行ファイルに埋め込まれたゲーム由来データ、ゲームの名称・商標、その他の第三者著作物には
MIT Licenseを適用しません。詳細は[THIRD_PARTY_NOTICES.md](docs/THIRD_PARTY_NOTICES.md)を参照してください。

本プロジェクトは非公式のコミュニティツールであり、FromSoftwareおよび
Bandai Namco Entertainmentとは関係ありません。『ELDEN RING』および関連する名称・商標の権利は、
それぞれの権利者に帰属します。
