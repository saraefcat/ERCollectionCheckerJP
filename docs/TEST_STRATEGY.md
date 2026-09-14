# Test Strategy

## 1. 目的

本プロジェクトのテストは「動く」ことより、誤った `Missing`、誤った `CoveredByConversion`、セーブ変更、
新旧 version の混同を防ぐことを優先する。自動テスト、1.17 fixture、目視可能な実データ比較を組み合わせ、
自動テストだけで対応完了とはしない。

## 2. Test layers

| Layer | 対象 | 入力 |
|---|---|---|
| Unit | parser primitive、Domain、rule、Runtime loader | 小さな synthetic bytes/JSON |
| Integration | slot section の連結、DB load、CompletionEngine、export | synthetic slot/data pack |
| 1.17 regression | 固定 ID、Tarnished Pack、armor curated case | reviewed JSON + synthetic/real facts |
| Actual fixture | BND4、全 slot、Inventory/Equipment/Storage/EventFlags、read-only | Git 管理外の copied `.sl2` |
| Release audit | Runtime 依存、write API、build/package、privacy | Release output と source |

## 3. SaveParser unit tests

最低限:

- BND4 header、12 entry、entry name、連続 range、MD5
- 空 slot と有効 slot、最大10 character slot
- slot version allowlist と未知 version 拒否
- GaItem type decode と可変長 8/16/21-byte record
- duplicate/mismatched handle、unknown prefix、truncated record
- Inventory common/key の capacity、record、quantity、handle 解決
- Storage common/key と可変 projectile count
- equipped item、equipped spell、gesture
- attached Ash of War handle
- EventFlags の可変 offset、address bounds、bit ordering
- malformed length/count、bounds overrun、missing marker
- 読み込み中のファイル変更と最大3回の再試行
- parser warning / unknown record の保存

negative test では、例外または Unknown が期待結果であり、値を返し続けることを成功としない。

## 4. Domain / CompletionEngine tests

- weapon upgrade normalization
- weapon affinity normalization
- duplicate-name item を別 identity として保持
- Inventory、Storage、Equipped の `AnyOf`
- AttachedAshOfWar の所有判定
- `AnyOf` / `AllOf` の三値真理表
- Unsupported / missing source / unresolved handle の Unknown propagation
- Unknown/Excluded の denominator 除外
- DataOnly のデフォルト非表示と Developer Mode 表示
- DataOnly を Missing/100%判定へ含めない
- `Obtainability.Unknown` を Unobtainable と誤判定しない
- Collection / Strict 切り替え
- Base / SOTE / Tarnished Pack filter
- category summary と coverage
- armor directional graph、一方向、双方向、到達不能
- `OwnedExact` / `CoveredByConversion` / `Missing` / `Unknown`
- PhysicalOwnership と CollectionCoverage の分離
- graph未投入、`AcquisitionKind.Unknown`、未解析sourceをMissingへ丸めないこと
- duplicate/self/dangling/cross-family edge、catalog/取得方法矛盾の拒否

## 5. ItemDatabase / localization tests

- manifest、schema、game version、minimum checker version
- 必須ファイル不足時の全体拒否
- duplicate ParamId/item key/canonical key/conversion edge/localization key
- orphan localization と dangling rule/item reference
- unresolved detection rule と armor node
- missing-ja/missing-en と誤翻訳なしの fallback
- ja/en の即時切り替えが SaveParser を再実行しないこと
- 日本語・英語の両方を対象にした case-insensitive search
- content pack/category/mode enum の妥当性

## 6. 1.17 regression set

最低限、次を固定する。

- Base Game item と SOTE item
- Tarnished Pack の武器、防具、霊馬装束
- Idus Knight / Heavy Knight 関連
- 霊馬装束3種の reviewed event flag
- 通常の双方向軽装化 pair
- Aristocrat Garb (Altered)
- Festive Garb (Altered)
- Fire Prelate Armor (Altered)
- Banished Knight Armor (Altered)
- attached Ash of War
- Collection/Strict と3 content pack の組み合わせ

ID、名称、取得可否、変換 edge は version-matched game data と curated evidence の双方で検証する。

## 7. Actual fixture tests

fixture は次に置き、Git へ含めない。

```text
<local-fixture-root>\1.17\baseline\ER0000.sl2
<local-fixture-root>\1.17\all-attires\ER0000.sl2
```

各 run で:

1. `SHA256(before)` を取得
2. read-only snapshot、container、全 slot を解析
3. 期待する item/rule だけを assertion
4. `SHA256(after)` を取得
5. hash の完全一致を assertion

`all-attires` は Tarnished Pack の他 item も含むため、全差分を霊馬装束だけとみなさない。
将来、Inventory-only、Storage-only、Equipped、強化済み、派生、attached Ash、armor conversion 等を
明示的に作れる fixture が増えた場合は期待値 manifest をセーブ本体とは別に管理する。

fixture のキャラクター名、SteamID、絶対パスを test output や snapshot file へ記録しない。

## 8. Read-only static audit

Release Gate では SaveParser/Core/Application に対し、セーブ用途の次の経路がないことを review/scan する。

```text
FileAccess.Write
WriteAllBytes / WriteAllText
BinaryWriter
save serializer
checksum write-back
repair / restore API
```

設定、ログ、CSV/JSON exportの書き込みは許可するが、共有fixture/セーブdirectoryを出力先にしない。
exportへ`.sl2`、SteamID、character name、個人pathを含めない。

## 9. Auto reload tests

- change notification の debounce
- 書き込み途中での snapshot 再試行
- 解析中の複数通知を1回の再解析へ集約
- cancellation と stale result の破棄
- 失敗時に前回成功結果を保持
- UI thread 非ブロック
- watcher dispose と path switch

## 10. Performance checks

代表的な 1.17 save で snapshot + parse + completion を計測し、通常2秒以内を目標とする。
検索・filter は既存 view model/index 上で即時に行い、DB は process あたり1回だけロードする。
性能のために checksum、bounds、Unknown 判定を省略しない。

## 11. CI

Windows runner で最低限次を実行する。

```text
dotnet restore
dotnet build -c Release
dotnet test -c Release
Runtime data pack validation
no-save-write static audit
```

実セーブ fixture は CI へアップロードせず、ローカル Release audit とする。

## 12. Phase gate

各 Phase 終了時に build、全該当 test、docs、known limitations を更新する。
Release 前には次をすべて満たす。

- Release build success
- all tests success
- JSON/schema/consistency validation success
- critical Unknown rule 0
- 1.17 regression success
- read-only SHA-256 success
- Runtime game-file/web dependency 0
- save write API 0
- actual save test success
- docs と THIRD_PARTY_NOTICES 更新

## 13. 現在の coverage と不足

現時点の通常Solutionにはloader、BND4/MD5、GaItem、held inventory、equipment、storage、event flag address、DataOnly policy、
Tarnished Pack curationに加え、Runtime packのfail-closed契約、SaveDiagnosticsのoption/path safety testsがある。
`evaluate-save` のoption契約、DataOnly表示フラグ、セーブ・5 Runtime pack配下への出力拒否、CLI最上位の
例外捕捉と出力console切断時のfail-safeも固定した。さらに製品同梱5 packの一括ロード、空slotの製品API評価、
不正設定・欠落DB・欠落セーブ・破損セーブ・範囲外slotの安定error code、入力非変更を統合テストで固定した。
Phase 5ではUI用queryとViewModelのテスト、STA上でWPF windowを実際に表示する起動回帰、
全10 slot列挙、character name parser、言語切替後のComboBox選択保持、clipboardコピー、
前回セーブパス保存の回帰を含め、公開Solutionの全テストをRelease gateで実行する。

実データ回帰では29 items / 80 aliases / 3 event flagsを含むreviewed 6断片の意味的一致を確認した。
共有fixtureではslot 0のbaseline 0件からall-attires 21件（武器6、防具12、霊馬装束3）への差分を検出し、
両ファイルの解析前後SHA-256一致を確認した。

確定したreviewed databaseから、取得可能2,768件、DataOnly 599件、alias 3,834件の軽量Runtime packを作成した。
Runtime側ではmanifest、payload、alias graph、Longsword 13観測値、
DataOnly/unknown/wrong-type identityの回帰テストを追加した。
既存Tarnished Pack overlay、防具変換pack、ジェスチャー対応packとの実データ合成では取得可能2,768件を
`Reviewed`として保持し、legacy self alias 8件とstrict alias 72件を区別して検証した。
取得可能Goods 952件は公式`goodsType`から10カテゴリへ分類し、分類packのprimary/repeat出力がbyte-identicalで
あること、合成時に同じcatalogを完全被覆することを確認した。
確定した全16 RuntimeファイルはApplication test出力へコピーし、WPFでは実行ファイルへ埋め込む。
埋め込みデータを一時展開して`RuntimeCatalogLoader`が2,768 Reviewed / 599 Excludedのsnapshotを構築し、
ロード後に一時directoryを削除することを自動検証する。

CompletionEngineはAnyOf/AllOfの三値論理、source未解析/破損、unknown ID、wrong-kind ID、alias、
武器強化suffix、invalid rule、event flag、Unsupportedを自動テストする。実fixtureではbaselineとall-attiresの
解析前後hashを維持し、all-attiresの既知21件（武器6、防具12、霊馬装束3）をOwnedとして検出した。
equipmentは既知19 slotのkind、item ID/handle一致、GaItem解決、未解決instance、inventory境界を固定し、
実fixtureではbaseline 14件、all-attires 10件を未解決0件で読み取った。Applicationでは装備武器の強化suffixも
containerと同じ規則で正規化する。
equipped spellsは14 slotの空/使用中sentinel、選択index、truncationを固定し、実fixtureではbaseline 5件・
選択index 0、all-attires 0件・選択index -1を読み取った。魔術・祈祷はcontainerまたはequipped spellの
`AnyOf`で判定し、Goods source identityとしてcanonical resolverへ渡す。
persistent gesturesは64 slot、空sentinel、重複、truncation、未知ID保持を固定し、実fixtureでは両方57件を読む。
固定GestureParam sourceの57 rowからreview済み53 item / 54 save IDを生成し、mapping source/Runtimeのstrict contract、
fingerprint、完全被覆をテストする。両fixtureのreview済みgesture結果はOwned 50 / Missing 3 / Unknown 0である。
装着戦灰handleは解決成功と未解決の両方を固定し、未解決時にMissingを返さないことを検証する。
進捗集計はUnsupportedの分離、Owned / Missing分母、Reviewed内Unknown、判定coverage、content pack範囲、
catalog/result不一致を検証する。実fixtureのTarnished Pack範囲ではbaselineが進捗0%、all-attiresが
進捗72.41%で、両方ともcoverage 100%・Unknown 0となる。
`evaluate-save` のTarnished Pack実行結果はbaselineがOwned 0 / Missing 29 / Unknown 0、all-attiresが
Owned 23 / Missing 6 / Unknown 0で、いずれも評価前後SHA-256一致を確認した。通常JSONのDataOnly一覧は0件、
開発フラグ指定時は599件（うち名称なし144件）である。絶対パス、ユーザー名、SteamID、character nameを
含まないことをscanした。
診断は製品用`SaveAnalysisService`へ委譲した後も、同梱Runtime snapshotを使った実行ファイル直接回帰で
両fixtureとも終了コード0、同じ集計値、解析前後SHA-256一致を確認した。
Collection対象1,940件ではbaselineがOwned 1,864 / Missing 76、all-attiresがOwned 1,886 / Missing 54となる。
Strict対象2,768件ではbaselineがOwned 2,427 / Missing 341、all-attiresがOwned 2,510 / Missing 258で、
両fixtureともUnknown 0 / coverage 100%となる。Goods 952件だけではbaselineがOwned 687 / Missing 265、
all-attiresがOwned 748 / Missing 204である。

Collection/Strict scopeは判定状態から独立してテストする。Collectionでは既存恒久カテゴリと遺灰・結晶雫を
Included、残るGoodsをUnreviewed、DataOnlyをExcludedとし、Strictでは取得可能な既知kindをIncludedとする。実データでは
CollectionがIncluded 1,940 / Unreviewed 828 / Excluded 599、detection coverage 100%、Strictが
Included 2,768 / Excluded 599、detection coverage 100%となる。CollectionのIncluded項目はすべて判定可能である。
content pack filterが分類や状態を
書き換えないことも固定した。

WPF向けには、Collection/Strictのscope、Owned/Missing/Unknown/Excluded、category、
content pack、日英名検索、開発者限定のID/key検索、DataOnly非表示を組み合わせて自動テストする。
ViewModelは日英ラベルの再生成、mode別scope表示、DataOnlyトグル時のExcludedフィルター連動を
セーブ再解析なしで固定した。Unknownは進捗の分母から外れ、判定coverageとして別集計する。

防具についてはsynthetic dataで、有向edgeの片方向性、明示的な逆edge、推移的到達、
`OwnedExact` / `CoveredByConversion` / `Missing` / `Unknown` / `Excluded`、
exact physical ownershipとの分離、graph未投入時の`UnverifiedArmorConversion`を固定した。
graph validationはduplicate/self/dangling/cross-family edge、DataOnly node、catalog/取得方法矛盾を拒否する。
実1.17 PARAMの読み取り専用監査では340 ShopLineup rowを170 directed edgesへ正規化し、
全edgeに明示的な逆edgeがある85 pairを確認した。確定した722 entryのRuntime packについて、
固定fingerprint、170 edge、分類件数、独立取得6 pairを自動回帰する。
実fixtureの防具結果はbaselineがOwnedExact 615 / CoveredByConversion 81 / Missing 26、all-attiresが
627 / 83 / 12で、どちらもUnknown 0である。

まだ必要な主要テスト:

- level・play time・NG周回数とequipped gestureのparser tests
- Base/SOTEを含むfull data pack loader/schema tests
- WPF実ウィンドの目視・キーボード操作・スクリーンリーダー回帰
- watcher/exportとSaveDiagnostics実fixtureの自動化
- CI と no-save-write static audit
- 実ゲーム中の保存更新を含む Release audit
