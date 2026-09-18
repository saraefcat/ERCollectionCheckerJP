# Application統合境界

## 1. 目的

WPF、SaveDiagnostics、将来のwatcher/exportが同じRuntime catalogと同じ読み取り専用セーブ評価を利用する。
UIや開発ツールがloader、parser、CompletionEngineを個別に組み立てて判定経路を分岐させない。

## 2. 製品Runtime

Git管理外の非公開Runtime rootに次の5 directoryを置き、ビルド時にWPF実行ファイルへ埋め込む。
既定値はリポジトリ外側の`data/1.17/runtime/`で、`ERCollectionCheckerJPPrivateDataRoot`により変更できる。

```text
reviewed-item-database/
tarnished-pack/
armor-conversions/
gesture-mappings/
goods-classifications/
```

`RuntimeDataPackPaths.FromRoot`がこの固定配置を解決する。`RuntimeCatalogLoader.LoadAsync`は5 packを並行ロードし、
各manifest/payloadのschema、version、length、SHA-256と相互参照を検証する。全検証に成功した場合だけ
`RuntimeCatalogSnapshot`を返す。部分Catalogや前回版との暗黙混在は返さない。

WPFは埋め込んだ16ファイルを起動時だけ非表示の一時rootへ展開し、snapshotを1回構築後に削除する。
異常終了で残ったGUID名の一時rootは、並行起動を妨げないよう24時間経過後の次回起動時に削除する。
Application統合テストは同じ16ファイルをテスト出力へコピーしてloader契約を検証する。
構築した`SaveAnalysisService`をMainWindowへ渡し、配布フォルダには個別JSONを置かない。

## 3. SaveAnalysisService

入力はセーブパスと0–9のslot indexである。処理順序:

1. `ReadOnlyFileSnapshotLoader`で安定したbefore snapshotを取得する
2. BND4/MD5、選択slot、各parser section、CompletionEngineを評価する
3. 同じファイルのafter snapshotを取得する
4. before/after SHA-256が一致した結果だけを返す
5. 全件、Tarnished Pack、Collection、Strictのsummaryと3,367 itemのUI向けprojectionを返す

`SaveAnalysisResult`に絶対パス、SteamID、character name、セーブ本文は含めない。ファイルについて公開するのは
basename、SHA-256、read-only hash確認結果だけである。各itemはCollection/Strictのscope、判定coverage、
Owned/Missing/Unknown/Excluded、Unknown理由、Goods分類、防具変換根拠を保持する。

画面のキャラクター選択には別APIの`ReadSlotsAsync`を使う。全10 slotを同じ読み取り専用snapshotと
MD5検証で調べ、slot index、version、空判定、使用中slotのcharacter nameだけを一時的な
`SaveSlotDescriptor`として返す。character nameは解析結果、診断JSON、export、通常ログへ含めない。

## 4. Error contract

外部入力の失敗は`CollectionCheckException`と安定した`CollectionCheckErrorCode`へ変換する。

| Code | 意味 |
|---|---|
| `InvalidConfiguration` | 必須パス等の設定不備 |
| `InvalidSlot` | slot indexが0–9外 |
| `SaveNotFound` | セーブが存在しない |
| `AccessDenied` | packまたはセーブの読み取り権限なし |
| `SaveChangedWhileReading` | snapshot取得中または解析前後にセーブが変化 |
| `SaveReadFailed` | その他のセーブI/O失敗 |
| `InvalidSave` | BND4、MD5、slot layout等が破損または未対応 |
| `DatabaseNotFound` | 必須Runtime pack/payload欠落 |
| `DatabaseVersionMismatch` | packのschema/version/hash/相互参照不一致 |
| `Unexpected` | UI境界で扱う予期しない失敗 |

ユーザー向けmessageへ絶対パスや内部例外を埋め込まない。技術的な例外は`InnerException`に保持し、将来の
privacy-safe診断IDへ接続する。

## 5. 現在のgate

- Release build成功
- 製品同梱5 pack / 16 filesのロード成功
- 2,768 Reviewed / 599 ExcludedのCatalog合成成功
- synthetic空slotで3,367 itemを評価し、入力hash不変を確認
- 不正設定、欠落DB、欠落セーブ、破損セーブ、範囲外slotのerror codeを自動テスト
- baseline/all-attiresを製品API経由で再評価し、従来結果とSHA-256一致を確認
- MVVM画面から同じ`SaveAnalysisService`をUI thread外で実行
- 全10 slotを列挙し、使用中slotをcharacter name付きで選択
- Collection/Strict、進捗、日英検索、state/category/content pack、DataOnlyの表示queryを自動テスト

Phase 5の詳細は`WPF_UI.md`を参照。次のPhase 6ではauto reload、stale result/cancellation、
CSV/JSON export、前回セーブパス以外の設定永続化を実装する。
