# Goods分類

## 1. 目的

取得可能Goods 952件をStrict All Items Modeで一律に物理所持判定しつつ、Collection Modeへ安全に含められる
恒久所持品だけを区別する。分類はCheckerが所有し、完全review済みcatalogをオフライン保守工程の
入力とする。製品Runtimeは生成工程やゲーム原本を参照しない。

## 2. 固定入力

対象はゲームバージョン1.17用に確定した`item-catalog.json`である。

| 入力 | byte length | SHA-256 |
|---|---:|---|
| 完全review済みcatalog | 16,393,647 | `CB355F2B47AFA6C11AD1504A8B448167F9E3BFAAB8D8452E35B2BB7B7F2B84EF` |

保守工程では取得可能な`Goods` 952件について、metadata keyが
`appearanceReplaceItemId, goodsType, isDeposit, reinforceGoodsId, sortId`と完全一致することを検証する。
未知key、欠落、型不一致、未知`goodsType`、重複identity、catalog fingerprint不一致は出力前に拒否する。

## 3. 分類

1.17の公式PARAM由来`goodsType`を次のconsumerカテゴリへ決定的に変換する。

| goodsType | Category | 件数 | Collection |
|---:|---|---:|---|
| 0 | General | 261 | Unreviewed |
| 1 | KeyItem | 285 | Unreviewed |
| 2 | Material | 106 | Unreviewed |
| 3 | Remembrance | 25 | Unreviewed |
| 7 | SpiritAsh | 51 | Included |
| 8 | SpiritAsh | 33 | Included |
| 9 | Flask | 2 | Unreviewed |
| 10 | CrystalTear | 40 | Included |
| 11 | ReusableContainer | 4 | Unreviewed |
| 12 | Information | 102 | Unreviewed |
| 14 | UpgradeMaterial | 43 | Unreviewed |

現段階でCollectionへ含めるのは、物理itemとして恒久所持される遺灰84件と結晶雫40件だけである。
残る828件には鍵、製法書、鈴玉、消耗品、素材、強化素材などが混在する。containerにないことを取得漏れと
断定できないため、取得済みflag等のpersistent sourceとconsumer側の細分類を確認するまでUnreviewedとする。

Strict All Items Modeは従来どおり952件すべてを`AnyContainerItem`で評価する。分類はdetection rule、
obtainability、ContentPack、公式名称を書き換えない。

## 4. Runtime pack

```text
goods-classifications-v0.2.0/
  manifest.json
  goods-classifications.json
```

生成済みpack:

| file | byte length | SHA-256 |
|---|---:|---|
| `manifest.json` | 486 | `06C01740118AA288DDAC0299AA955DFB78E6BD4D0C88A9A38663759AF5DD0011` |
| `goods-classifications.json` | 165,245 | `7FAD387FA90A6FDFCD090B3EFEA4D56334CE66E18AA540D6739254E068212E4C` |

database versionは`1.17.goods-classifications.cb355f2b`である。Runtime loaderはschema/game/regulation/status、
manifest length/hash、source catalog SHA-256、全952件のsorted unique identity、`goodsType`とカテゴリの対応、
カテゴリ別件数をfail-closedで検証する。Catalog composerは同じsource catalogの取得可能Goodsを完全被覆する場合だけ
分類を接続する。primary/repeat生成物はbyte-identicalである。

## 5. 実セーブ回帰

Goods分類を接続したCollectionはIncluded 1,940 / Unreviewed 828 / Excluded 599となる。

| fixture | Collection Owned | Missing | Unknown | completion |
|---|---:|---:|---:|---:|
| baseline | 1,864 | 76 | 0 | 96.08% |
| all-attires | 1,886 | 54 | 0 | 97.22% |

両fixtureとも遺灰84件と結晶雫40件をすべてOwnedとして検出した。Strict結果はbaselineが
Owned 2,427 / Missing 341、all-attiresがOwned 2,510 / Missing 258で、Unknown 0、coverage 100%を維持する。
`SaveDiagnostics` schema 6は各Reviewed項目へGoods categoryを出力し、評価前後のセーブSHA-256一致を確認する。

## 6. 次のgate

- KeyItemとInformationを製法書、鈴玉、地図、鍵などのconsumerカテゴリへ分割する
- 使用・納品・交換後も取得済みと判断できるversioned persistent sourceを確認する
- statefulなitemへcontainer不存在だけを根拠としたMissingを出さない
- WPFのカテゴリfilterと日本語表示へ接続する
