# 同梱データの取扱い

## 1. 目的

ERCollectionCheckerJPはゲーム原本を解析せず、レビュー済みのRuntime JSONを実行ファイルへ埋め込んで読み取る。
ゲームデータの抽出・生成とRuntimeデータの保管は非公開のオフライン保守工程で行い、
公開リポジトリにはゲーム由来データを格納しない。

## 2. 公開境界

公開リポジトリに含めるもの:

- Runtime loaderと相互参照検証
- consumer固有の判定モデルと回帰テスト

公開リポジトリや配布物に含めないもの:

- ゲーム原本、PARAM、FMG、BHD/BDT、Oodle DLL
- Runtime JSON、manifest、レビュー用fragment
- セーブfixtureと個人環境のパス
- 抽出・生成ツール、そのソースコード、内部成果物
- 未レビューの中間データ

## 3. Runtime data pack

```text
private-data-root/1.17/runtime/
  reviewed-item-database/
  tarnished-pack/
  armor-conversions/
  gesture-mappings/
  goods-classifications/
```

各packは独立したmanifest境界を持つ。`RuntimeCatalogLoader`は全packのmanifest、payload、
schema version、ファイルサイズ、SHA-256、参照整合性を検証してから、単一のimmutable Catalogへ
合成する。いずれかの検証に失敗した場合は部分的なデータを使用しない。

## 4. データ更新時の公開側確認

更新済みRuntime JSONを非公開領域へ配置した後、リリース工程では次を確認する。

1. RuntimeデータがGit管理外にあり、公開差分へ含まれていない。
2. 原本、中間成果物、個人情報、ローカル絶対パスが含まれていない。
3. manifestと各payloadのサイズ・SHA-256が一致する。
4. category、content pack、localization、alias、rule、conversionの参照が整合する。
5. 同一入力から確定したsnapshotに意図しない差分がない。
6. Release build、全自動テスト、publishが成功する。
7. セーブfixtureを使う場合、解析前後でSHA-256が変化しない。

## 5. Runtimeの責務

Runtimeは実行ファイルへ埋め込んだデータと読み取り専用セーブ解析だけを使用する。埋め込みJSONは
起動時だけ非表示の一時領域へ展開し、既存のfail-closed検証とロード完了後に削除する。外部プロセスの起動、生成工程の
成果物へのアクセス、ゲームインストール先の探索は行わない。

データ不足や参照矛盾は、推測で補わず`DatabaseVersionMismatch`または影響項目の`Unknown`として
扱う。DataOnly項目はidentity解決にだけ使用し、通常の進捗分母には含めない。

## 6. 現在の1.17 snapshot

1.17用snapshotは、取得可能2,768件、DataOnly 599件、alias 3,834件を収録する。
Collectionモードは恒久的な所持状態を判定できる1,940件を対象とし、Strictモードは取得可能な
2,768件を対象とする。防具変換、ジェスチャー対応、Goods分類、Tarnished Pack固有ruleは
それぞれ独立したpackとして検証後に合成する。

製品と`SaveDiagnostics`は同じCatalog snapshotとApplication評価経路を使用し、別の変換・判定経路を
持たない。診断出力にはセーブ本文、SteamID、キャラクター名、個人環境の絶対パスを含めない。
