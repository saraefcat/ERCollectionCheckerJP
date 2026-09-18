# ERCollectionCheckerJP 設計

## 1. 文書の位置付け

この文書は `ERCollectionCheckerJP_SPEC.md` と
`ERCollectionCheckerJP_CODEX_IMPLEMENTATION_GUIDE.md` を上位仕様とする Phase 0 の設計書である。
両文書は 2026-09-14 に全文確認した。矛盾がある場合は、セーブ保護、fail-closed、
データの正確性、仕様書、実装都合の順で優先する。

最重要の判断基準は次の2点である。

- 根拠が足りない項目を `Missing` と断定しない。
- 変換では作れない独立収集物を `CoveredByConversion` と断定しない。

## 2. リポジトリと配布物の境界

Git のルートは外側のプロジェクトフォルダ直下にある `src/` である。
ゲーム原本と共有セーブ fixture は本リポジトリ外の Git 管理外領域に置く。
リリース成果物、原本、セーブ、ゲーム DLL を本リポジトリへ含めない。

```text
ERCollectionCheckerJP/                 Git 管理外の作業領域
  ERCollectionCheckerJP_SPEC.md        上位仕様
  ERCollectionCheckerJP_CODEX_IMPLEMENTATION_GUIDE.md
  data/1.17/                           非公開 Runtime・回帰データ
  src/                                 Git リポジトリ
    src/                               Runtime 製品コード
    tests/
    docs/
```

## 3. Solution 構成と依存方向

```text
ERCollectionCheckerJP.App (WPF/MVVM)
              |
              v
ERCollectionCheckerJP.Application
       |             |             |
       v             v             v
    Domain       SaveParser    ItemDatabase
検証済みRuntime JSON
  -> ItemDatabase
```

依存の原則:

- `Domain` は他の製品プロジェクトへ依存しない。
- `SaveParser` はバイナリを事実データへ変換するが、収集完了を判定しない。
- `ItemDatabase` は versioned JSON の検証とロードを担当し、セーブを読まない。
- `Application` は parser、database、判定 engine を調停する。
- `App` は Application のユースケースだけを呼び、バイナリ offset を知らない。
- データ生成工程を製品コードから分離し、検証済みRuntime JSONだけを同梱する。
- セーブfixture診断はSaveParser/Checker側の責務とする。

## 4. Runtime アーキテクチャ

```text
ER0000.sl2
  -> ReadOnlyFileSnapshot
  -> BND4/MD5 validation
  -> selected slot bytes
  -> SaveSnapshot
       - CharacterInfo
       - InventoryItems
       - StorageItems
       - EquippedItems
       - EquippedSpells
       - AttachedAshesOfWar
       - EventFlags / GestureState / TorrentState
       - UnknownRecords and parser warnings

Git管理外の検証済みRuntime JSON
  -> manifest/schema validation
  -> ItemDatabaseSnapshot

SaveSnapshot + ItemDatabaseSnapshot + user options
  -> CompletionEngine
  -> Owned / Missing / Unknown / Excluded
  -> Armor: OwnedExact / CoveredByConversion / Missing / Unknown / Excluded
  -> localized view models
  -> WPF UI / CSV / JSON
```

Runtime が直接読んでよい入力は `.sl2`、同梱 JSON、`%LOCALAPPDATA%` の設定だけである。
`Data0.*`、`regulation.bin`、Message/FMG、Oodle、外部ツール出力を Runtime から読まない。

## 5. 各層の責務

### 5.1 Domain

言語や保存形式に依存しない次の概念を保持する。

- `ItemKind`、`ContentPack`
- `CompletionState`、`ArmorCollectionState`
- `DataPresence`、`Obtainability`、`ExclusionReason`
- `ItemDefinition`、`DetectionRule`
- canonical item、collection group、物理所持、収集 coverage
- 判定結果、判定根拠、Unknown 理由

Domain は表示名を item identity に使用しない。

### 5.2 SaveParser

読み取り専用スナップショットから、境界検査済みの事実だけを `SaveSnapshot` として返す。
offset、capacity、binary layout はこの層へ閉じ込める。未知構造をずらし読みで探索せず、
invariant を満たさなければ対応エラーまたは Unknown record とする。

### 5.3 ItemDatabase

対応バージョンの manifest と JSON 一式を一度だけロードし、schema、参照整合性、重複、
localization completeness、detection rule、armor conversion graph を検証する。
不完全なデータパックを部分的に採用せず `DatabaseMismatch` とする。

詳細は `ITEM_DATABASE.md` を参照する。

### 5.4 CompletionEngine / Application

SaveParser が返す観測値と ItemDatabase のルールを結合し、収集状態を算出する。
Inventory の不在だけで Missing を決めない。ユーザー設定による Collection/Strict、
content pack、Developer Mode の表示条件はこの層で適用し、物理所持の事実は破棄しない。

### 5.5 App

WPF + MVVM とする。言語切り替え、検索、フィルタ、並べ替えは既存の解析結果へ適用し、
言語変更だけでセーブを再解析しない。画面コードから parser を直接呼ばない。

### 5.6 Bundled Runtime data

製品には、オフライン保守工程で生成・レビューされたRuntime JSONだけを同梱する。
manifest、schema、payload length、SHA-256、相互参照の検証に成功したsnapshotだけを使用し、
Runtime JSON、レビュー前の出力、ゲーム原本を公開リポジトリへ含めない。配布物には検証済みRuntimeだけを
実行ファイルへ埋め込む。詳細は`DATA_PIPELINE.md`を参照する。

## 6. セーブ解析設計

### 6.1 読み取り専用スナップショット

`.sl2` は `FileAccess.Read` と `FileShare.ReadWrite | FileShare.Delete` だけで開く。
読み込み前後の length と `LastWriteTimeUtc` が一致するまで最大3回再試行し、全バイトをメモリへ
コピーしてから解析する。Core/SaveParser に serializer、repair、checksum write-back API を作らない。

### 6.2 Container と slot

BND4 header、12 entry、entry 名、連続 data range、stored MD5 を検証する。
`USER_DATA000`–`009` は character slot、`010` は account/global、`011` は regulation data とする。
全10 slotの空判定と、使用中slotのPlayerGameData character nameを製品UI向けに列挙する。
level、play time、NG周回数はfield layout確定後に追加する。

### 6.3 Offset resolution

固定絶対 offset の一覧ではなく、検証済みの先行 section 末尾と可変長 count から次の section を求める。

```text
GaItem end
  + PlayerGameData size
  -> boundary marker validation
  -> equipped item IDs and GaItem handles
  -> held inventory
  -> projectile count and entries
  -> storage
  -> unlocked-region count and entries
  -> event flags
```

各加算は checked arithmetic と range check を通す。count には上限を設け、marker、capacity、型 prefix、
checksum のどれかが不正なら後続 section を信頼しない。詳細な既知値と検証状況は
`SAVE_FORMAT_NOTES.md` に記録する。

### 6.4 GaItem parsing

GaItem は slot version に応じた件数を読み、record ごとに raw item ID の上位 nibble を分類する。
空 record は8 bytes、武器は21 bytes、防具は16 bytes、その他既知種別は8 bytesとして進める。
handle 種別との一致、duplicate handle、range を検査し、武器 record から装着戦灰 handle を保持する。
未知 prefix や不正 record を既知項目へ丸めない。

### 6.5 SaveSnapshot

個別 reader の戻り値を公開 Runtime 契約へ直結させず、Application が利用する immutable な
`SaveSnapshot` へ集約する。Snapshot は位置、数量、実 param ID、raw ID、装備状態、イベント状態、
未解決 handle、parser warning を保持できる形にする。`Owned`/`Missing` は含めない。

## 7. Item normalization

内部 identity は `(ItemKind, ParamId)` と安定した `Key` を使用する。表示名は identity ではない。

- 武器: version-matched Param と生成済み alias で強化値・派生を canonical weapon へ写像する。
- 防具: exact Param ID は維持し、別形態は有向 conversion graph で coverage を計算する。
- 戦灰: standalone item と武器装着 handle の両方を同じ canonical item へ結ぶ。
- 同名別 ID: collection group の明示がなければ統合しない。
- 不明 ID: 近い ID や名前から推測せず `UnknownItemId` とする。

正規化規則はコードの算術だけに依存させず、レビュー済みのversioned alias dataで監査可能にする。

## 8. Database と localization

完成データパックは最低限次を含む。

```text
manifest.json
items.json
detection-rules.json
exclusions.json
armor-conversions.json
gesture-mappings.json
localization.ja.json
localization.en.json
```

すべて `schemaVersion` と `gameVersion` を持ち、manifest は Regulation、database build、
minimum checker version、生成元ハッシュまたは provenance を保持する。名称はゲームの同一バージョンの
FMG から生成し、未確認の翻訳を作らない。日本語欠損時は英語と「日本語名未確認」を表示する。

## 9. Detection rule engine

判定 source は Inventory、Storage、GaItem、装備item、装備魔術、装着戦灰、イベントフラグ、ジェスチャー、
霊馬装束状態を区別する。leaf rule は `Matched / NotMatched / Unknown` の三値を返し、
`AnyOf` と `AllOf` は Unknown を保存する。`Unsupported` は常に Unknown である。

すべての必要 source が完全に解析され、ルールが検証済みで、対象が通常入手可能な場合だけ、
全ルールの非一致を `Missing` へ変換する。詳細は `DETECTION_RULES.md` を参照する。

## 10. 防具 conversion

形態ごとに `PhysicalOwnership` と `CollectionCoverage` を別々に算出する。
conversion は Param ID 間の有向 edge であり、逆 edge を自動生成しない。

- exact ID を所持: `OwnedExact`
- exact ID はないが、所持形態から検証済み edge で到達可能: `CoveredByConversion`
- source が完全で到達不能かつ独立取得が必要: `Missing`
- edge または取得方法が未確認: `Unknown`
- 除外対象: `Excluded`

Aristocrat Garb (Altered)、Festive Garb (Altered)、Fire Prelate Armor (Altered)、
Banished Knight Armor (Altered) は curated regression case とし、名称だけで edge を生成しない。

`ArmorConversionGraph`と`ArmorCollectionEngine`は実装済みである。graphはself/duplicate/dangling/cross-family edge、
catalog不一致、取得方法との矛盾を拒否し、engineはexact physical ownershipとconversion coverageを別々に返す。
1.17 PARAM監査では`ShopLineupParam.equipId`と、その`mtrlId`が参照する
`EquipMtrlSetParam.materialId01`から、170本の有向edge（明示的な双方向85 pair）を確認した。
オフライン監査で340 source rowを170 directed edgeへ縮約し、722防具entryのRuntime packを
生成・厳格検証した。同一catalog SHAを完全被覆する
場合だけCompletionEngineへ接続し、通常防具を`Reviewed`へ昇格する。根拠と統合境界は
`ARMOR_CONVERSIONS.md`を参照する。

## 11. DataOnly とモード

`DataPresence` と `Obtainability` は別軸で管理する。Param row があり取得経路が見つからないだけなら
`Obtainability.Unknown` であり、`Unobtainable` ではない。通常入手不能の確定には curated evidence を要する。

`Present + Unobtainable` の DataOnly item はデフォルト非表示、常に進捗・Missing・100%判定の対象外とする。
Developer Mode の明示スイッチで表示だけを変更できる。セーブ内に存在しても物理所持を注記し、
collection state は `Excluded` のままとする。

現在のstaging Runtime packは完全レビュー済みcatalogからDataOnly 599件を取り込み、11 aliasを含めて
identity resolverへ登録する。これにより既知の取得不能IDが`UnknownItemId`を発生させることは防ぐが、
所有評価は実行せず`Excluded`を返す。`SaveDiagnostics --show-data-only true`がDeveloper Mode相当の確認口である。

Collection/Strict と content pack filter は denominator の構成を変えるが、未選択項目を Missing として数えない。

## 12. Unknown と fail-closed

次の状況では処理全体を Unsupported/DatabaseMismatch とするか、影響範囲を Unknown とする。

- 未知 schema / game version / slot layout
- checksum、marker、bounds、count invariant の不一致
- DB 未登録 ID、未解決 handle、未対応 record
- localization または rule の参照切れ
- 未検証の event flag、armor conversion、取得可否
- 必須判定 source の parser が未実装または失敗

Unknown を UI や集計段階で Missing に変換しない。Unknown が残る場合は判定 coverage を表示し、
無条件の「完全100%」を表示しない。

## 13. Error model

Application が扱う安定した error code と、技術的な例外詳細を分離する。

| Code | 扱い | ユーザー向け方針 |
|---|---|---|
| `InvalidConfiguration` / `InvalidSlot` | 判定開始前に中断 | 設定またはslot選択の修正を案内 |
| `SaveNotFound` / `AccessDenied` / `SaveReadFailed` | 操作を中断 | パス、権限、利用状態の確認を案内 |
| `SaveChangedWhileReading` | 最大3回の安定snapshot取得後、または前後hash不一致で中断 | 保存完了後の再試行を案内 |
| `InvalidSave` | 対象をfail-closedで拒否 | 破損または未対応形式として表示 |
| `DatabaseNotFound` / `DatabaseVersionMismatch` | 判定開始前に中断 | 対応 DB が必要と表示 |
| `ItemMappingFailed` / `UnknownItemId` | 影響項目を Unknown | 新しいゲーム版の可能性を表示 |
| `Unexpected` | 安全側で中断 | 一般化した初期化失敗を表示し、生の例外詳細は出さない |

`RuntimeCatalogLoader`と`SaveAnalysisService`はparser/loaderの例外を`CollectionCheckErrorCode`へ変換する。
UI に絶対パス、offset、stack traceを直接出さない。

## 14. Logging と privacy

`Microsoft.Extensions.Logging` を使い、Information/Warning/Error/Debug を区別する。
通常ログに SteamID、character name、セーブ内容、絶対パス全文を記録しない。
診断 export は app/DB/slot format version、匿名化した parser warning、unknown ID、件数、例外情報だけを含み、
`.sl2` 本体や個人識別情報を含めない。
開発用 `SaveDiagnostics evaluate-save` は全件／Tarnished Pack別summaryとReviewed項目の状態・理由を出力するが、
絶対パス、SteamID、character name、セーブ本文は含めない。セーブと5入力packの配下を出力先として許可せず、
セーブ評価前後のSHA-256一致を記録する。

## 15. Auto reload と並行処理

`FileSystemWatcher` は変更通知を 1000–2000 ms debounce し、書き込み完了確認後に非同期再解析する。
解析中の再変更は1回の再解析予約へ畳み込み、UI thread をブロックしない。失敗時は前回の成功結果を保持する。
セーブ snapshot と DB snapshot は immutable とし、UI への結果差し替えは一括で行う。

## 16. Test strategy

unit、integration、1.17 regression、実セーブ確認を分ける。fixture は Git 管理外のコピーだけを読み、
解析前後の SHA-256 一致を必須にする。positive case だけでなく未知 schema、truncation、bounds overrun、
矛盾 mapping、未解決 rule などの negative/fail-closed case を同等に重視する。

テスト一覧と Release Gate は `TEST_STRATEGY.md` を参照する。

## 17. Phase と gate

作業順序は実装指示書の Phase を採用する。

1. Phase 0: 設計文書を揃える
2. Phase 1: read-only SaveParser と parser tests
3. Phase 2: Runtime data packのschema・manifest検証
4. Phase 3: ItemDatabase、schema、normalization、exclusion
5. Phase 4: CompletionEngine と全判定 rule
6. Phase 5: WPF/MVVM UI
7. Phase 6: auto reload、CSV/JSON、diagnostics
8. Phase 7: 1.17 regression、実機、release audit

各 Phase は build、tests、docs、known limitations を更新してから次へ進む。

## 18. 2026-09-14 時点の実装監査

この設計書より先に一部コードが作成されたため、ここで順序を是正する。既存コードは設計への適合を
確認できた範囲だけ維持し、未完の機能を対応済みと表現しない。

| 領域 | 現在の状態 | 次の gate |
|---|---|---|
| Phase 0 docs | 本書と関連文書を整備 | 相互リンクと内容監査 |
| read-only loader / BND4 | 実装・unit test あり | 実 fixture の継続 SHA 検査 |
| GaItem / held / equipment / equipped spell / storage / persistent gesture / event flag / character name | 1.17 の既知部分を実装 | level・play time・NG周回数、equipped gesture 等 |
| Runtime data packs | Tarnished Pack、全件DB、防具変換、ジェスチャー、Goods 952件の10カテゴリ分類を厳格検証 | version更新差分report |
| ItemDatabase | Domain model、DataOnly 599件の除外・表示policy、5 Runtime loader、防具有向graph、Goods分類の厳格validation、製品snapshot確定 | 残るGoods 828件の永続source分類 |
| Application / CompletionEngine | 三値rule、全取得可能2,768件の標準rule、全観測source、防具exact/conversion、Collection/Strict集計、製品用Runtime一括ロード・読み取り専用セーブ解析・安定error codeを実装 | auto reload向けcancellation/stale result統合 |
| WPF | Phase 5のMVVM収集一覧、日英切替、進捗、検索/filter、DataOnly開発者表示、前回セーブパス保存を実装 | Phase 6のauto reload、export、その他の設定永続化 |
| diagnostics | Checker所有SaveDiagnosticsとprivacy-safe `evaluate-save` を実装、共有fixture回帰済み | Release auditへの自動統合 |
| export / watcher | 未実装 | Phase 6 で実装 |

非公開データ領域の`1.17/fragments/`は回帰用の移行中データであり、完成データパックではない。
したがってアプリを「1.17の全アイテムを永続取得判定できる」とは表示しない。一般向けreleaseでは、
Collection 1,940件、Strict 2,768件、分類保留Goods、未実装機能をREADMEとリリースノートへ明記する。
