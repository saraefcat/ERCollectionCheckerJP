# Detection Rule 設計

## 1. 目的

Detection Rule Engine は、ItemDatabase の item definition と SaveSnapshot の観測値から
`Owned / Missing / Unknown / Excluded` を根拠付きで算出する。SaveParser は収集状態を判定せず、
UI は独自の所有判定を持たない。

最重要制約は、必要な解析 source が未実装・破損・未確認なら `NotMatched` ではなく `Unknown` とすることである。

## 2. 評価モデル

内部の rule 評価値は三値とする。

```text
Matched      根拠となる観測値が存在
NotMatched   source を完全に解析できた上で観測値が不存在
Unknown      source/rule/version/対応付けを信頼できない
```

item の最終状態:

1. 除外条件に該当すれば `Excluded`。物理所持は補足情報として残せる。
2. 有効な rule が `Matched` なら `Owned`。
3. rule が Unknown を含み、Owned の根拠がないなら `Unknown`。
4. 通常入手可能、対象 mode/content pack 内、必要 source がすべて完全、全 rule が `NotMatched` の場合だけ `Missing`。

未解決 rule を黙ってスキップしない。rule が0件または `Unsupported` なら `Unknown` とする。

## 3. Rule types

| Type | 照合 source | 主な用途 |
|---|---|---|
| `InventoryItem` | held inventory | 手持ちだけを明示する特殊規則 |
| `StorageItem` | storage box | 木箱だけを明示する特殊規則 |
| `AnyContainerItem` | held + storage | 通常の物理アイテム |
| `GaItem` | GaItem map | instance/登録状態の補助検出 |
| `EquippedItem` | equipped items | inventory list 外の装備を補完 |
| `EquippedSpell` | equipped spell 14 slot | container list 外の魔術・祈祷を補完 |
| `AttachedAshOfWar` | weapon record の装着 handle | 武器へ装着中の戦灰 |
| `EventFlag` | event flag bit | 取得後に物理 item が残らない要素 |
| `GestureUnlock` | persistent GestureGameData | ジェスチャー解放 |
| `TorrentAttireUnlock` | 検証済み専用 state/flag | 霊馬装束 |
| `AnyOf` | child rules | いずれかの根拠で Owned |
| `AllOf` | child rules | 複数条件が必要な特殊要素 |
| `Unsupported` | なし | 明示的な未対応 |

`InventoryItem` と `StorageItem` を通常 item の別々の必須条件として `AllOf` にしない。
通常はどちらかにあればよいため `AnyContainerItem` を使う。

## 4. Composite rule の三値論理

### AnyOf

- child に `Matched` が1件でもある: `Matched`
- `Matched` がなく `Unknown` が1件でもある: `Unknown`
- すべて `NotMatched`: `NotMatched`

### AllOf

- child に `NotMatched` が1件でもある: `NotMatched`
- `NotMatched` がなく `Unknown` が1件でもある: `Unknown`
- すべて `Matched`: `Matched`

空の `AnyOf`/`AllOf` は schema error とし、一般 value を与えない。

## 5. JSON shape

```json
{
  "schemaVersion": 1,
  "gameVersion": "1.17",
  "rules": [
    {
      "itemKey": "torrentattire:2009600",
      "rule": {
        "type": "TorrentAttireUnlock",
        "flagId": 60101
      },
      "evidence": [
        "1.17 ItemLotParam row",
        "fixture comparison"
      ]
    }
  ]
}
```

leaf ごとに許可 field を限定する。例えば `EventFlag` は `flagId` が必須、item rule は `paramId` または
canonical key が必須である。余分な field、未知 type、循環参照、dangling item key は validation error とする。

## 6. Physical item の判定

通常 item は、held、storage、equipped の少なくとも1か所で exact/alias ID が確認できれば Owned とする。
quantity を持つ record は `quantity > 0` を要求する。複数 location の数量と実 ID は判定結果の evidence に残す。

取得可能Goods 952件はheldまたはstorageの`AnyContainerItem`で物理所持を判定する。これによりStrict All Items
Modeでは全件を判定できる。Collection Modeに含める恒久・準恒久Goodsの分類は別契約であり、消費後に物理itemが
残らない項目へcontainer ruleだけを使わない。公式`goodsType`で遺灰84件と結晶雫40件を恒久所持品として
Collectionへ含め、残る828件は永続sourceの確認が終わるまで分母外に保つ。

武器は実 Param ID を versioned canonical alias で正規化し、強化値・派生が異なっても canonical weapon を Owned とする。
ただし UI 詳細には observed Param ID、強化値、派生、location を残す。

セーブ内の武器Param IDは末尾2桁の強化段階を除去して派生IDへ正規化した後、versioned canonical aliasへ渡す。
この算術は武器のセーブ表現だけに限定し、他kindや派生統合そのものへ一般化しない。

未解決 GaItem handle がある状態では、その handle が該当 item ではないと証明できないため、影響し得る rule を
Missing にしない。Unknown の範囲は可能な限り item kind 単位へ限定する。

## 7. 戦灰

戦灰は held、storage、GaItem、装着中の武器を確認する。武器 GaItem record の attached Ash of War handle を
GaItem map と versioned mapping で実 Param ID へ解決し、単体 item がなくても Owned とする。

装着 handle の意味または mapping が未確認なら、その戦灰を Missing と断定しない。

### 魔術・祈祷

魔術84件と祈祷129件は、heldまたはstorageのGoods identityに加え、装備魔術14 slotのMagicParam IDを確認する。
3 sourceのいずれかにcanonical identityがあればOwnedとする。equipped spell sectionが未解析・破損しており、
containerにも対象がない場合はMissingではなくUnknownとする。

### ジェスチャー

Storage直後のGestureGameData 64枠を読み、1.17 `GestureParam`のオフライン監査で確定したversioned mappingで
save gesture IDをGoods-backed catalog identityへ変換する。対応表が取得可能ジェスチャー53件を完全被覆する場合だけ
ruleを有効化する。対応表にない数値は既知identityと一致しない独立IDとして保持し、他のジェスチャーをUnknownにはしない。
section自体が未解析・破損している場合は全GestureUnlockをUnknownとする。

## 8. Event flag と霊馬装束

event flag rule は game version ごとに curated する。flag ID だけでなく parser が使用する byte/bit address、
根拠となる Param/event、fixture 結果を検証する。

霊馬装束3種は 1.17 の ItemLotParam 取得 flag `60101`–`60103` を使用する reviewed rule を持つ。
baseline と all-attires の比較では対象3 bit を個別に確認する。all-attires fixture には他の Tarnished Pack item も
含まれるため、セーブ全体の差分を3装束だけに帰属させない。

## 9. 防具の変換判定

防具 conversion は DetectionRule の alias ではなく、exact ownership の後に別評価する collection coverage である。

```text
PhysicalOwnership(exact ParamId)
  + reviewed directed conversion graph
  -> ArmorCollectionState
```

`AlterationFamilyId` が同じだけでは変換可能としない。edge は方向ごとに明示し、逆向きを推測しない。
対象が直接入手だけ必要で、所持形態からの path がなければ `Missing`。変換関係が未確認なら `Unknown`。

`ArmorCollectionEngine`はexact ownershipをInventory / Storage / Equipmentから評価した後、
`ArmorConversionGraph`の明示edgeだけを推移的に辿る。exact sourceが所持済みなら
`CoveredByConversion`を返し、同時に対象exact IDの`PhysicalOwnershipState`も保持する。
graphに対象nodeがない場合、または`AcquisitionKind.Unknown`なら`UnverifiedArmorConversion`とし、
Missingへ丸めない。DataOnlyはgraph評価前に`Excluded`となる。

## 10. Exclusion と DataOnly

`DataPresence.Present + Obtainability.Unobtainable` は DataOnly とし、判定 rule が Matched しても最終状態は
`Excluded` のままにする。Developer Mode では物理所持の事実と exclusion reason を表示できる。

`Obtainability.Unknown` は DataOnly ではないが、通常入手可能とも断定できないため進捗分母へ入れず、
review/Unknown として扱う。既知の取得参照がないことだけで `Unobtainable` にしない。

## 11. Mode と content pack

Collection/Strict と Base/SOTE/Tarnished Pack の選択は評価後の対象集合を構成する。
選択外 item は Missing ではなく集計対象外である。状態そのものと、現在の表示・分母への inclusion を分離する。

進捗は `Owned / (Owned + Missing)`。Unknown と Excluded は分母に入れず、別に判定 coverage と Unknown 件数を表示する。

## 12. Unknown reasons

最低限、機械可読な理由を結果へ保持する。

```text
UnsupportedRule
SourceNotParsed
SourceCorrupt
UnknownItemId
UnresolvedHandle
DatabaseMismatch
UnverifiedEventFlag
UnverifiedArmorConversion
UnknownObtainability
```

同じ item に複数理由があればすべて保持し、診断 export では個人情報を含めず集計する。

## 13. 現在の状態

Domain のrule type、Tarnished Packのreviewed rule/flagに加え、CompletionEngineの三値評価を実装した。
Inventory、Storage、GaItem、equipped items、equipped spells、reviewed event flagを検証済みparser sectionから観測し、alias正規化、
unresolved kind、source未解析/破損をUnknownへ伝播する。空composite、field不足、catalog不整合もfail-closedでUnknownとなる。

実fixtureのslot 0のTarnished Pack範囲ではbaselineがOwned 0 / Missing 29 / Unknown 0、all-attiresが
Owned 23 / Missing 6 / Unknown 0となる。all-attiresは武器6、防具12、霊馬装束3をexact所持し、
防具2件を明示変換でカバーする。
装備item/spellとpersistent gesture sourceは`Complete`である。武器はcontainerまたはequipment、タリスマンは
containerまたはequipment、戦灰はcontainer、GaItemまたは装着handleの`AnyOf`で判定する。防具はexactの
container/equipment所持とreview済み有向変換を分離して判定し、魔術・祈祷はcontainerまたはequipped spell、
ジェスチャーはpersistent GestureGameDataで判定する。取得可能Goods 952件はquantity付きcontainer ruleで判定し、
取得可能2,768件が`Reviewed`、取得不能599件は`Excluded`である。完全review済みidentityをresolverへ加えたためfixture中の既知DataOnly IDは
Unknownを汚染しない。今後もDBにない同kind観測は`UnknownItemId`として安全側へ伝播する。
attached Ash of Warは
GaItem handle mapで解決し、未解決handleまたは型不一致をAshOfWar kindのUnknownへ伝播する。
content pack範囲、Owned / Missing分母、Reviewed内Unknown、判定coverageを分離した集計を実装した。
実fixtureのTarnished Pack範囲では判定coverageが両方100%となり、baselineはOwned 0 / Missing 29、
all-attiresはOwned 23 / Missing 6となる。100%だけで完了扱いせずUnknown件数とcoverageを併記する。
Collection対象1,940件ではbaselineがOwned 1,864 / Missing 76、all-attiresがOwned 1,886 / Missing 54である。
Strict対象2,768件ではbaselineがOwned 2,427 / Missing 341、all-attiresがOwned 2,510 / Missing 258で、
どちらもUnknown 0 / coverage 100%である。
同じ評価経路を `SaveDiagnostics evaluate-save` から実行でき、Reviewed各項目の状態・Unknown理由と
集計をprivacy-safe JSONで確認できる。診断中もセーブは読み取り専用で、前後SHA-256一致を検証する。
Collection/Strict modeの三値scopeは実装済みである。Collectionは既存1,816件に遺灰84件と結晶雫40件を
加えた1,940件をIncluded、残るGoods 828件をUnreviewed、DataOnly 599件をExcludedとする。
Strictは取得可能2,768件をIncludedとする。実データのdetection coverageは両modeとも100%である。
保留中GoodsのMissing判定はCollection Modeでは有効にしない。
防具有向graph、strict source/runtime validation、exact/conversion分離評価を実1.17 packへ接続済みである。
