# Third-party notices

## ライセンスの適用範囲

ルートの`LICENSE`に記載したMIT Licenseは、ER Collection Checker JP contributorsが作成した
オリジナルソースコードおよび文書に適用する。

実行ファイルへ埋め込まれたゲーム由来データ、ゲーム内の名称、ELDEN RINGおよび関連する名称・商標、
その他の第三者著作物にはMIT Licenseを適用しない。これらに関する権利は各権利者に帰属する。

実行ファイルへ埋め込むRuntime JSONには、ゲームバージョン1.17に対応するアイテム識別子、分類、
日英名称、判定規則、対応関係などが含まれる。配布フォルダへ個別JSONとして配置しない。
Runtime JSON、ゲーム本体のバイナリ、画像、音声、セーブデータ、抽出工程の中間物は公開リポジトリへ含めない。
ゲーム本体のバイナリ、画像、音声、セーブデータ、抽出工程の中間物は配布物へ含めない。

本プロジェクトは非公式のコミュニティツールであり、FromSoftwareおよびBandai Namco Entertainmentとは
関係がない。

## Runtimeと保守工程

アプリ固有の製品コードには、現時点で外部NuGetライブラリをリンクしていない。
ゲームデータのオフライン保守工程で使用する依存関係、外部入力、ゲームDLLは本リポジトリの
推移的依存ではなく、通常版リリースへ含めない。

## Microsoft .NET

- Product: Microsoft .NET 10
- Usage: win-x64自己完結版へ.NET RuntimeとWPF Runtimeを同梱
- Distribution terms: 発行に使用した.NET SDK付属の`LICENSE.txt`
- Third-party notices: 発行に使用した.NET SDK付属の`ThirdPartyNotices.txt`
- Redistributed notices: 配布物の`DOTNET_LICENSE.txt`および`DOTNET_THIRD_PARTY_NOTICES.txt`

発行スクリプトは、実際に使用する`dotnet`実行ファイルと同じディレクトリから上記2ファイルを取得し、
改変せず配布物へコピーする。これにより、自己完結版へ含まれるRuntimeに対応した配布条件と
第三者通知を同梱する。

## EldenRing-SaveForge

- Project: oisis/EldenRing-SaveForge
- Branch consulted: `main`（2026-09-17確認）
- Repository: https://github.com/oisis/EldenRing-SaveForge
- License: GPL-3.0
- Usage: `.sl2`、slot、GaItem、Inventory、Equipment、EquippedSpells、Storage、GestureGameData、EventFlagsの公開仕様を独立実装の検証資料として参照
- Copied code / concept: コードコピー、リンクなし。公開された形式説明と手元fixtureを照合
- Modification: 該当なし
- Distribution: repository、source、仕様書をGitおよび製品へ含めない
- Attribution requirement: 調査資料としてproject/repository/license/利用範囲を本項で明示

SaveParserは仕様記述と手元の1.17 fixtureを照合した独立実装であり、セーブ書き込みAPIを持たない。

## 運用方針

通常版へ新しいlibrary、CLI、参考実装、data sourceを採用した時点で、Project、Repository、License、Usage、
Copied code / concept、Modification、Distribution、Attribution requirementを追記する。
ライセンス不明のコードをコピーせず、GPL codeを通常版Runtimeへリンクしない。
