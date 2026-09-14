# ELDEN RING 1.17 セーブ形式ノート

## 1. 適用範囲と証拠方針

この文書は Steam 版 ELDEN RING App/Regulation 1.17 の `ER0000.sl2` を読み取るための、
確認済み構造と未確認領域を記録する。公開仕様は構造理解の参考にし、最終的な採用値は
手元の 1.17 fixture、bounds/invariant、negative test と照合する。

未知バージョンや不一致を offset の微調整で読み進めない。検証できない section は parser error または
Unknown とし、その不在をアイテムの `Missing` 判定へ使わない。

## 2. Read-only invariant

- 入力は `FileMode.Open`、`FileAccess.Read`、`FileShare.ReadWrite | FileShare.Delete` で開く。
- 全バイトを memory snapshot へ読み、元ファイルを閉じてから解析する。
- 読み込み前後の length と `LastWriteTimeUtc` を比較し、変化時は最大3回再試行する。
- fixture は解析前後の SHA-256 が一致することを検査する。
- SaveParser に serializer、checksum write-back、repair、backup restore API を実装しない。

## 3. Steam BND4 container

1.17 fixture は非暗号化 BND4 である。現在の reader は次をすべて検証する。

| 項目 | 確認値 |
|---|---:|
| binder header size | `0x40` |
| entry header size | `0x20` |
| headers end / first data offset | `0x300` |
| entry count | 12 |
| entry flag | `0x50` |
| entry checksum | payload 直前の16-byte MD5 |
| names | UTF-16LE `USER_DATA000`–`USER_DATA011` |

entry は隙間なく連続し、最後の entry 末尾がファイル末尾と一致しなければ拒否する。
checksum は検査だけに使い、再計算値を保存しない。

```text
USER_DATA000–009  character slot
USER_DATA010      account/global data
USER_DATA011      regulation data
```

1.17 fixture の character payload は各 `0x280000` bytes。これは対応判定の invariant には使用できるが、
将来版へ無条件に適用しない。

## 4. Character slot と version

slot 先頭の little-endian `uint32` を現在の slot structure version として読む。

- `0`: fixture 上の空 slot
- `1..81`: GaItem record count 5118 の既知 layout
- `82` 以上の既知 1.17 layout: GaItem record count 5120

上記 count 分岐は現行コードの対応範囲であり、未知の将来 version を自動的に 5120 件として受理してはならない。
製品用slot enumeratorは全10 slotのversionを読み、`0`を空slotとして除外する。使用中slotの
character nameは、可変長GaItem sectionの検証済み末尾を基準にしたPlayerGameData `+0x94`から、
終端付きUTF-16LE（最大16文字）として取得する。固定ファイルoffsetには依存しない。1.17 fixtureの
5つの使用中slotで位置と文字列を確認済みである。level、play time、NG周回数は未実装である。

## 5. GaItem section

開始 offset は slot 先頭から `0x20`。各 record は可変長である。

| raw item ID 上位 nibble | ItemKind | record bytes | handle 上位 nibble |
|---:|---|---:|---:|
| `0x0` | Weapon | 21 | `0x8` |
| `0x1` | Armor | 16 | `0x9` |
| `0x2` | Accessory | 8 | `0xA` |
| `0x4` | Goods | 8 | `0xB` |
| `0x8` | AshOfWar | 8 | `0xC` |

空 record は raw item ID `0xFFFFFFFF` で8 bytes。handle は `0` または `0xFFFFFFFF` だけを許可する。
非空 record の共通部は handle と raw item ID であり、Param ID は raw item ID の下位28 bit。
武器・防具には追加 field があり、武器の offset `+0x10` には装着戦灰 handle、`+0x14` には1 byte の
追加値がある。意味未確定 field は `Unknown2` 等のまま保持し、推測した名前を付けない。

検証:

- record が slot 範囲内に収まること
- raw item ID と handle の種別が一致すること
- 非空 handle が重複しないこと
- 未知 nibble は既知種別へ丸めず拒否すること

## 6. PlayerGameData 境界と held inventory

GaItem section の可変長末尾から PlayerGameData の既知サイズ `0x1B0` を加え、その直前位置から
既知の SP-effect boundary marker を検証する。marker が一致した場合だけ、marker から `505` bytes 後を
held common inventory の先頭として採用する。

| 領域 | capacity | record size |
|---|---:|---:|
| Held Common | `0xA80` (2688) | 12 |
| Held Key | `0x180` (384) | 12 |

各 record は little-endian の `handle`, `quantity`, `acquisitionIndex` で構成する。
quantity の上位 bit は除外し、`quantity & 0x7FFFFFFF` を観測数量とする。

- Weapon / Armor / AshOfWar の instance handle は GaItem map で解決する。
- Accessory / Goods は、GaItem map にない direct handle の下位28 bitを Param ID として解決できる。
- 解決できない instance handle は `Unresolved` とし、似た ID へ変換しない。

common/key の count header と、末尾の `NextEquipIndex`、`NextAcquisitionSortId` は診断用に保持する。
header 値の意味と許容範囲を追加 fixture で確定するまでは、所有数として使用しない。

## 7. Equipped items

PlayerGameData末尾から次の固定領域を順にたどると、22個の装備item ID tableと対応する22個の
GaItem handle tableが得られる。

```text
PlayerGameData end
  + 0xD0  SP effects
  + 0x58  equipped item indices
  + 0x1C  active weapon slots
  -> equipped item IDs (22 * uint32 = 0x58)
  -> equipped GaItem handles (22 * uint32 = 0x58)
  -> held common inventory count header (uint32)
```

table末尾がheld common inventory先頭の4 bytes前と一致することを必須invariantとする。確認済みのitem-bearing
slotは0–9がWeapon、12–15がArmor、17–21がAccessoryである。10、11、16は用途を確定できていないため、
所有判定へ流さず推測した名前も付けない。

既知slotではitem IDとhandleの空状態が一致し、handle上位nibbleが期待kindと一致することを検査する。
Weapon/Armor/AshOfWarのinstance handleはGaItem mapで解決し、Accessoryはdirect handleを解決する。
解決後Param IDはitem IDと一致しなければならない。Weapon/Armorのitem IDは1.17 fixtureで確認したbare Param IDと、
公開仕様にあるbit 31付き表現の両方だけを許可する。GaItem mapで解決できないinstanceは既知IDへ推測せず、
該当kindの未解決観測としてCompletionEngineへ渡す。

共有fixtureのslot 0ではbaseline 14件、all-attires 10件の装備を読み、未解決handleは0件だった。
両fixtureともsection境界、kind、item ID/handle対応が一致した。

### Equipped spells

Held inventoryの直後には固定`0x74` bytesの装備魔術領域がある。14 slotを8 bytesずつ読み、末尾4 bytesを
選択中slot indexとして読む。

```text
held inventory end
  -> 14 * { MagicParam ID (uint32), occupied sentinel (uint32) }
  -> selected slot index (int32)
```

空slotは`SpellId = 0xFFFFFFFF`かつsentinel `0`、使用中slotは0でも`0xFFFFFFFF`でもないSpell IDと
sentinel `0xFFFFFFFF`だけを許可する。選択indexは`-1`または0–13で、0以上なら対応slotが使用中でなければならない。
Spell IDはGaItem handleではなくGoods source identityとして魔術・祈祷のcanonical resolverへ渡す。

共有fixtureのslot 0ではbaselineが5件（選択index 0）、all-attiresが0件（選択index -1）であり、全使用中slotの
sentinelとreviewed catalogへの解決を確認した。魔術・祈祷はheld、storage、equipped spellsのいずれかでOwnedとなる。

## 8. Storage offset resolution

Storage の位置は held inventory の固定末尾から可変長 section をたどって求める。

```text
held inventory end
  + equipped spells 0x74
  + 0x8C
  + 0x18
  -> acquired projectile count (uint32)
  -> count * 8 bytes
  + 0x9C
  + 0x0C
  + 0x12F
  -> storage common count header
```

projectile count は 4096 以下だけを許可する。Storage record の解釈と handle 解決は held inventory と同じ。

| 領域 | capacity | record size |
|---|---:|---:|
| Storage Common | `0x780` (1920) | 12 |
| Storage Key | `0x80` (128) | 12 |

## 9. Persistent gestures

Storage直後の固定`0x100` bytesはGestureGameDataであり、64個の`uint32` save gesture IDを持つ。
空slotは`0xFFFFFFFE`である。重複IDと`0xFFFFFFFF`は構造不正として拒否し、対応表にない数値は
未知の明示IDとして保持する。

catalogのジェスチャーはGoods Param IDをidentityに使うため、save IDを直接Param IDとして扱わない。
オフライン監査で確定した`gesture-param.json`のGestureParam row IDから`saveGestureId = rowId * 2 + 1`を生成し、
同rowの`itemId`へ対応付けたversioned Runtime packを使用する。source 57 rowのうち、review済み53 itemを
54 save IDが被覆する。

共有fixtureのslot 0ではbaseline/all-attiresとも57件を読む。review済み範囲は両方ともOwned 50 / Missing 3で、
未知値には旧エディタ形式と一致するeven ID `110`が含まれる。この値は既知53 itemのsave IDとは一致しないため、
他項目のMissing判定を曖昧にしない。セーブは変更も正規化も行わない。

## 10. Event flags offset resolution

Storage 末尾のGestureGameData `0x100` bytes後に unlocked-region count がある。count は 4096 以下だけを許可し、
`count * 4` bytes の entry を読み飛ばす。その後、確認済みの固定 section 長を加えて event flag block を得る。

```text
after unlocked regions
  + 0x29
  + 0x4C
  + 0x103C
  + 0x1B588
  + 0x40B
  + 0x1A
  -> event flags (`0x1BF99F` bytes + 1-byte terminator)
```

flag ID から byte/bit への一般変換は誤りが収集判定へ直結するため、検証済み address を versioned curation として
入力する。現行 reader は `(ByteIndex, BitIndex)` の bounds を検査して bit を読む。

1.17 の霊馬装束は ItemLotParam の取得フラグ `60101`–`60103` と結び、baseline fixture では3 bitが未設定、
all-attires fixture の slot 0 では3 bitとも設定されることを確認済みである。

## 11. 未実装または未確定の領域

次は SaveParser の Phase 1 完了前に調査・実装・テストが必要である。

- PlayerGameData の level、play time、NG count
- 将来version向けsupported version allowlistの確定
- equipped gesture（quick-access 6枠。collection判定はpersistent GestureGameDataを使用）
- Torrent/Ride の flag 以外に必要な persistent state
- malformed/unknown record を部分 Unknown として保持する `SaveSnapshot`
- 実際のゲーム保存中に更新されたケースの integration test

これらが未実装の間、CompletionEngine は対応 source を必要とする項目を `Missing` と判定してはならない。

weapon record内の装着戦灰handleはGaItem handle mapでAshOfWarへ解決する。0 / `uint.MaxValue`は未装着、
解決済みAshOfWar handleだけを観測へ追加し、handle欠落や型不一致はAshOfWar kindの未解決観測として
CompletionEngineへ渡す。

## 12. 既知 fixture の取扱い

fixture は外側の次の場所へ置き、Git へ追加しない。

```text
<local-fixture-root>\1.17\baseline\ER0000.sl2
<local-fixture-root>\1.17\all-attires\ER0000.sl2
```

`all-attires` は霊馬装束だけでなく Tarnished Pack の別アイテムも取得したセーブである。
したがって比較テストは「全差分が霊馬装束由来」と仮定せず、curated item と flag の一致だけを個別に検査する。
