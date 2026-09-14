# Armor conversion design and 1.17 evidence

## 1. Purpose

防具の現物所持と、軽装化などの変換による収集カバーを分離する。
名前の `Altered` / `軽装`、Param ID差分、同一familyだけを根拠にedgeを生成しない。

```text
PhysicalOwnership(exact ParamId)
  + reviewed directed conversion graph
  -> OwnedExact | CoveredByConversion | Missing | Unknown | Excluded
```

## 2. ELDEN RING 1.17 source-fact audit

ゲームバージョン1.17のPARAMをオフラインで読み取り専用監査した。変換方向は次の2表の参照から復元できる。

1. `ShopLineupParam` の `equipType = 1` と `equipId` が生成先防具を示す。
2. 同rowの `mtrlId` が `EquipMtrlSetParam` rowを参照する。
3. 参照先が `materialCate01 = 1`、`itemNum01 = 1` で、追加の正数量materialを持たない場合、
   `materialId01` を1個消費する単一防具変換として扱える。
4. よってedgeは `materialId01 (source) -> equipId (target)` であり、名称やID差分から方向を推測しない。

完全review済みcatalogの`ReviewedObtainable`防具だけに限定した監査結果は次の通り。

| 項目 | 件数 |
|---|---:|
| reviewed obtainable armor | 722 |
| 条件を満たすShopLineup row | 340 |
| 重複除去後の有向edge | 170 |
| 明示的な逆edgeも存在するedge | 170 |
| 明示的な双方向pair | 85 |

340 rowには無料・有料menuの重複があり、同じsource/targetを正規化すると170 edgeになる。
たとえばDrake Knight Helmでは、`60000 -> 61000` と `61000 -> 60000` が別々の
ShopLineup/EquipMtrlSet参照として存在する。双方向性はこの2本の明示edgeから得る。

使用した完全catalogは16,393,647 bytes、SHA-256
`CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF`である。
調査用抽出JSONはrelease payloadではなく、Runtime packを作るための根拠資料とする。

## 3. Directly obtainable altered armor exceptions

次の6 pair、12 itemは上記170 edgeに含まれず、完全catalogには`CharacterInitialization`以外の
直接取得参照がある。
変換カバーへ統合せず、各exact itemを独立した直接取得対象として扱う。

| Normal Param ID | Altered Param ID | English name |
|---:|---:|---|
| 200000 | 201000 | Banished Knight Helm |
| 200100 | 201100 | Banished Knight Armor |
| 360100 | 361100 | Fire Prelate Armor |
| 370100 | 371100 | Aristocrat Garb |
| 800100 | 801100 | Festive Garb |
| 810000 | 811000 | Commoner's Headband |

`CharacterInitialization`だけを直接取得の根拠にはしない。Commoner's Headband通常形は`MapItemLot`、
残る11 itemは`EnemyItemLot`を根拠にする。

## 4. Checker-side contract

Checkerは`ArmorConversionEntry`を次の情報として受け取る。

```text
ItemKey
ParamId
ArmorVariantId
VariantType: Normal | Altered | Special
AlterationFamilyId
DirectlyObtainable
AcquisitionKind: DirectOnly | TransformOnly | DirectOrTransform | Unknown
TransformTargets[]
```

`ArmorConversionGraph`はduplicate key/Param ID/variant ID、self/duplicate/dangling target、
familyをまたぐedge、catalogとの不一致、DataOnly node、`AcquisitionKind`との矛盾を拒否する。
逆edgeは自動生成しない。

`ArmorCollectionEngine`はexact IDのInventory、Storage、Equipmentを先に評価し、明示edgeを逆向きに探索して
所持sourceから対象へ到達できるかを判定する。結果にはexact physical ownershipも残す。

- exact所持: `OwnedExact`
- 明示pathのsource所持: `CoveredByConversion`
- 全sourceが完全に解析済みで非所持、graph/nodeもreview済み: `Missing`
- graph/node未投入、取得方法未確認、必要source未解析: `Unknown`
- DataOnly: `Excluded`

## 5. Integration boundary

PARAM readerとsource-fact抽出は非公開のオフライン保守工程に限定し、通常版Checkerへ含めない。
次の2 payloadを検証済みの監査入力として使用した。

| payload | bytes | SHA-256 |
|---|---:|---|
| `params/ShopLineupParam.json` | 171,181 | `5FCDBC119D101AA57DD61B7D6491EF98F5F00EF45E27CD2F65C3952582C4B961` |
| `params/EquipMtrlSetParam.json` | 468,630 | `F958721B35EBD5CAC99549D8AF6424EA96AEE07F66E5D0968B58E36081D77D83` |
| `manifest.json` | 532 | `335A011E37F05436486828845699C6ED03994671FE92AC3DD4D44C023CBC5F73` |

primary/repeat packはbyte-identicalで、標準validationを通過している。確定したRuntime loaderは
manifestのversion/length/SHA-256、payload contract、catalog fingerprintを検証する。

- 1.17の`ShopLineupParam`と`EquipMtrlSetParam` provenance
- 無料・有料rowの決定的な重複除去
- source/targetがreviewed obtainable armorであること
- 170 directed edges / 85 explicit bidirectional pairsの固定回帰
- 例外6 pairをedgeへ混入させないこと
- direct acquisition根拠と`AcquisitionKind`のreview結果

生成済みRuntime packは722 entry / 170 evidence row / 170 directed edge / 85 bidirectional pairを持つ。
variantはNormal 630 / Altered 90 / Special 2、取得分類はDirectOnly 552 / TransformOnly 84 /
DirectOrTransform 86である。`armor-conversions.json`は256,582 bytes、SHA-256
`806E5B21985E74658F912E265069BF56198CE9E306EC544B626D5DC8220876B9`、manifestは581 bytes、SHA-256
`D2167F3E01A6AC79B22B9404A0C863593D44064439A94B165AC7849C85D96E13`で、primary/repeatはbyte-identicalである。

Runtime loaderはsource pack/catalog fingerprint、manifest、payload hash、全件順序、edge/evidence一致、
分類件数、例外6 pairを再検証する。Catalog composerは同じsource catalog SHAの取得可能防具722件と
entry集合が完全一致する場合だけgraphを有効化する。CompletionEngineはこのgraphを使って通常防具を
`Reviewed`へ昇格し、診断結果に`ArmorState`、exact physical ownership、conversion source keyを保持する。
