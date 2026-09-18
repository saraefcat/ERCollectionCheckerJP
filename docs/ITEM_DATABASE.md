# Item Database 設計

## 1. 目的

Item Database はゲーム version 固有の事実を製品コードから分離し、Runtime が `.sl2` と同梱 JSON だけで
所持判定できるようにする。初期対応は App/Regulation 1.17、Base Game、Shadow of the Erdtree、
Tarnished Pack である。

## 2. Versioned data pack

```text
private-data-root/1.17/
  manifest.json
  items.json
  detection-rules.json
  exclusions.json
  armor-conversions.json
  gesture-mappings.json
  goods-classifications.json
  localization.ja.json
  localization.en.json
```

起動時に manifest と全必須ファイルを一括検証し、成功した immutable snapshot だけを共有する。
ファイル不足、schema 不一致、game version 混在、参照切れがあれば部分ロードせず `DatabaseVersionMismatch` とする。

## 3. Manifest

最低限の契約:

```json
{
  "schemaVersion": 1,
  "gameVersion": "1.17",
  "regulationVersion": "1.17",
  "databaseVersion": "2026.09.14.1",
  "minimumCheckerVersion": "1.0.0",
  "localizationSource": "GameData",
  "languages": ["jpnjp", "engus"],
  "sourceGameVersion": "1.17"
}
```

生成の再現性と監査のため、原本 hash、curation revision、build tool version を追加してよい。
Runtime は対応していない `schemaVersion` または `minimumCheckerVersion` を無視して読み進めない。

## 4. Item identity

Item identity は表示名ではなく次の値で管理する。

- `Key`: `weapon:3560000` のような安定した一意 key
- `ParamId`
- `ItemKind`
- `CanonicalKey`
- 任意の `CollectionGroupId`

同名でも別 item なら別 key とする。名称一致だけで統合しない。

## 5. ItemDefinition

最低限の field:

```text
Key
ParamId
Kind
Category / SubCategory
CanonicalKey / CollectionGroupId
ContentPack
DataPresence
Obtainability
ExclusionReason
IncludeInCollectionMode
IncludeInStrictMode
DetectionRules
```

`isDataOnly` のような派生値は JSON の正本にせず、`DataPresence` と `Obtainability` から Runtime で算出する。
生成 fragment に診断用の派生値を含める場合も、loader は整合性を検証する。

## 6. Category と mode

内部 category key は言語非依存とし、表示名だけを localization resource から得る。最低限、武器、防具、
タリスマン、魔術、祈祷、戦灰、遺灰、結晶雫、製法書、鈴玉、重要品、道具、地図、素材、消耗品、弾薬、
霊馬装束、その他を表せるようにする。

- Collection Mode: 恒久・準恒久の収集対象。
- Strict All Items Mode: 通常取得可能な消耗品・素材等を追加。

content pack と mode の無効化は表示/分母の対象外であり、item を Missing や Excluded へ書き換えない。
`CollectionScopePolicy`は`Included / Unreviewed / Excluded`を返す。現在、Collectionでは武器、防具、
タリスマン、魔術、祈祷、戦灰、ジェスチャー、霊馬装束に加え、Goods分類packで確認した遺灰と結晶雫を
Includedとする。消費後も取得済み状態を判定できる永続sourceが未確認のGoodsカテゴリはUnreviewedとする。
Strictでは取得可能な既知kindをIncludedとする。

## 7. Data presence、obtainability、exclusion

```text
DataPresence: Present | NotPresent
Obtainability: Obtainable | Unobtainable | Unknown
ExclusionReason:
  CutContent | DeveloperTest | NpcOnly | Placeholder | InternalDummy |
  Unobtainable | ModOnly | Other
```

Param row の存在と通常入手可否を分離する。取得参照が自動抽出できなかっただけなら `Unknown` とする。
`Unobtainable` の確定には reviewed curated evidence が必要である。

DataOnly (`Present + Unobtainable`) と他の exclusion は常に進捗、Missing 件数、100%判定から除外する。
DataOnly はデフォルト非表示で、Developer Mode の専用スイッチでだけ表示する。

## 8. Canonical alias

武器の強化値・派生など、複数の observed Param ID が同一収集物を表す場合は versioned alias を持つ。

```json
{
  "sourceKind": "Weapon",
  "observedParamId": 8530100,
  "canonicalKey": "weapon:8530000"
}
```

同一 observed ID を複数 canonical key へ割り当てない。算術 pattern が見えても、version-matched Param から
生成・検証した mapping を正本とする。実際の強化値や派生は観測 detail として残す。

## 9. Detection rules

rule は `detection-rules.json` に分離して item key へ関連付ける。通常物理 item、装備、装着戦灰、event flag、
gesture、Torrent attire、composite rule を表現できる。rule がない item を Missing とせず `Unsupported/Unknown` とする。

評価の詳細は `DETECTION_RULES.md` を参照する。

## 10. Armor conversions

`armor-conversions.json` は各形態と有向 edge を保持する。

```text
ParamId
ArmorVariantId
VariantType: Normal | Altered | Special
AlterationFamilyId
DirectlyObtainable
AcquisitionKind: DirectOnly | TransformOnly | DirectOrTransform | Unknown
TransformTargets[]
```

family が同じだけで相互変換とはしない。逆 edge は明示がある場合だけ存在する。
directly-obtainable altered armor の curated case を regression test に固定する。

現在は`ArmorConversionGraph`のconsumer modelとvalidationを実装済みである。duplicate key/Param ID/variant ID、
self/duplicate/dangling target、familyをまたぐedge、DataOnly node、catalog/取得方法の矛盾を拒否する。
1.17 PARAMのオフライン監査で170 directed edges / 85 explicit bidirectional pairsを確認した。722 entry / 170 evidence rowの
Runtime JSONはprimary/repeatでbyte-identicalとなり、loaderが固定fingerprint、分類件数、edge/evidence、
独立取得6 pairをfail-closedで検証する。Catalog composerは同一source catalogの取得可能防具722件を
完全被覆する場合だけgraphを接続する。
詳細は`ARMOR_CONVERSIONS.md`を参照する。

## 11. Localization

`localization.ja.json` と `localization.en.json` は item key から公式 FMG 名を引く。
UI 全体の resource key は item 名とは別 namespace で管理する。

日本語 UI の fallback:

1. verified Japanese
2. English + `日本語名未確認`

English UI の fallback:

1. English
2. internal display name + warning

検索 index は ja、en、reviewed alias を別々に保持し、デフォルトで両言語を検索する。言語切り替えで parser や
CompletionEngine を再実行しない。

## 12. Goods classifications

`goods-classifications.json`は完全review済みcatalogの取得可能Goods 952件を、公式PARAM由来の`goodsType`で
General、KeyItem、Material、Remembrance、SpiritAsh、Flask、CrystalTear、ReusableContainer、Information、
UpgradeMaterialへ分類する。Runtime loaderはcatalog SHA-256、全件被覆、item key/Param ID、型、`sortId`、
カテゴリ別件数をfail-closedで検証する。

この分類はCollection scopeの判断にだけ使う。現段階では物理的に保持される遺灰84件と結晶雫40件だけを
Collectionへ含める。残る828件を自動でMissingへ流さず、鍵・製法書・鈴玉などを含む細分類と、消費後にも
取得済みを示すpersistent sourceを別工程で確認する。詳細は`GOODS_CLASSIFICATIONS.md`を参照する。

## 13. Validation

Runtime loaderとデータ保守工程は最低限次を検査する。

- duplicate item key、`(ItemKind, ParamId)`、alias、conversion edge、localization key
- canonical key と collection group の参照整合性
- item key と detection rule/localization/exclusion の参照整合性
- `DataPresence`、`Obtainability`、`ExclusionReason` の矛盾
- mode/content pack/category の既知 enum
- empty composite rule、未知 rule type、invalid flag ID
- armor graph の未知 node と意図しない自己 edge
- `missing-ja`、`missing-en`、orphan localization
- schema/game version の一致

critical error があればデータパック全体を拒否する。warning を黙って捨てず build summary と診断へ出す。

## 14. 現在の状態

Domain の `ItemDefinition` と enum、DataOnly visibility policy、Tarnished Pack 29項目の reviewed curation/fragment、
ja/en 名称、weapon canonical alias、霊馬装束 rule fragment は存在する。

非公開Runtime rootには現行製品scopeの5 packを確定した。各packは独立manifestを保ち、Applicationが
全manifest、payload、version、fingerprint、相互参照を検証してから単一Catalogへ合成する。残るGoods 828件の
永続source分類と統合schemaは未完了のため、「1.17の全アイテムを永続取得判定できるDB」とは扱わない。
現在のCollection/Strict scopeと分類保留を明示したうえで、一般向けreleaseに利用する。

reviewed databaseの軽量Runtime packは取得可能2,768件とDataOnly 599件をloadでき、
既存Tarnished Pack overlayとの合成境界を実装した。overlayとidentity・日英名称・content pack・aliasを照合し、
武器555件、タリスマン154件、戦灰116件、魔術84件、祈祷129件、ジェスチャー53件、取得可能防具722件と
取得可能Goods 952件に検証済みruleを割り当て、取得可能2,768件を`Reviewed`、DataOnlyを`Excluded`として保持する。
DataOnlyはデフォルト非表示で、開発フラグ時だけ存在情報を表示し、常に進捗対象外とする。
防具はexact physical ownershipとconversion coverageを別々に評価し、診断結果に変換元を残す。
Collection/Strict scopeは実装済みで、Collection 1,940件、Strict 2,768件となる。scope inclusionと
detection coverageを分離する。Goods 952件のうち遺灰84件と結晶雫40件をCollectionへ含め、残る828件は
未レビューとして分母外に保つ。Strictではquantity付きcontainer所持を判定し、全2,768件のcoverageを100%とする。
