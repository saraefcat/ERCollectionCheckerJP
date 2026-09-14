# ジェスチャー対応表

## 1. 目的

セーブのpersistent GestureGameDataはGoods Param IDではなくsave gesture IDを保持する。
ERCollectionCheckerJPはこの値を推測変換せず、1.17 `GestureParam`のオフライン監査から確定した固定JSONを
製品用Runtime packとして読み込む。

## 2. 固定入力

| 入力 | bytes | SHA-256 |
|---|---:|---|
| `artifacts/catalog/gesture-param.json` | 6,268 | `4A2BA631A53B566F3FC3A38B9A49C970DAE89031AE23D67E0E69E32501245414` |
| 完全review済み`item-catalog.json` | 16,393,647 | `CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF` |

保守工程ではGestureParamのschema、PARAM名/type、data version、row size、field順、57 row、56 unique item ID、
row順、正数fieldと両fingerprintをfail-closedで検証する。review済みcatalog側では取得可能なGesture 53件だけを採用する。

## 3. 対応規則

各GestureParam rowについて次を生成する。

```text
saveGestureId = GestureParam rowId * 2 + 1
catalog identity = (Goods, GestureParam.itemId)
```

53 catalog itemを54 save IDが被覆する。`Ring of Miquella`は2 rowが同じGoods identityを指すため、
save IDが2個ある。catalogにない3 source rowは収集対象へ昇格しない。

SaveParserは64個のraw IDを読み、空`0xFFFFFFFE`を除外する。対応表にない明示IDは他のIDと区別できるため、
既知Gesture全体をUnknownにはしない。section境界・重複・sentinelが壊れている場合はsectionを拒否し、
Missingへ丸めない。

## 4. Runtime pack

生成工程の中間出力はGit管理外とし、確定したRuntime packだけを製品へ同梱する。

| file | bytes | SHA-256 |
|---|---:|---|
| `gesture-mappings.json` | 7,200 | `57480BECB3E930389BBAB9D5F27975792FB94D7382637BDB4300EA63A409D911` |
| `manifest.json` | 576 | `6585DD863D3E50EB8839CFE54B81FE8A61FDD2783497865B6B4171AD2FF1D112` |

primary/repeat生成は両fileともbyte-identicalである。Runtime loaderはmanifest、version、status、source/catalog
fingerprint、payload length/hash、pack内file集合、save ID算式、sort/unique、item key/ID、53 item完全被覆を検証する。

## 5. 実fixture結果

baseline/all-attiresのslot 0はいずれもpersistent gesture 57件を持ち、review済み範囲は
Owned 50 / Missing 3 / Unknown 0である。Missingは`Ring of Miquella`と2つの`The Ring` identityである。
両fixtureに対応表外のeven ID `110`があるが、既知save IDとは一致しない独立値として保持し、セーブは変更しない。
解析前後SHA-256は両方一致した。
