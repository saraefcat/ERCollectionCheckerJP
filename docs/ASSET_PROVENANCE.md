# アプリ資産の来歴

この文書は、ER Collection Checker JPで配布する視覚資産の作成経緯と加工内容を記録する。

## アプリアイコン「コレクターリス」

| 項目 | 内容 |
|---|---|
| 採用日 | 2026-09-18 |
| 作成方法 | OpenAI Codexの組み込み画像生成機能（imagegen）で新規生成 |
| 参照画像 | ER Boss Tracker JPの自作アイコンを配色・輪郭・質感の参照に使用 |
| 第三者のロゴ・ゲーム画像 | 使用していない |
| 原画 | `src/ERCollectionCheckerJP.App/Assets/AppIconSource.png`（1254 x 1254、透過PNG） |
| アプリ内画像 | `src/ERCollectionCheckerJP.App/Assets/ERCollectionCheckerJP.png`（1024 x 1024、透過PNG） |
| Windowsアイコン | `src/ERCollectionCheckerJP.App/Assets/ERCollectionCheckerJP.ico`（16、20、24、32、40、48、64、128、256 px） |
| 派生処理 | `tools/New-AppIcon.ps1`による角丸背景外の透過クリップ、高品質縮小、マルチサイズICO化 |

採用した生成プロンプトは次のとおり。

```text
Use case: logo-brand
Asset type: square Windows application icon concept for ER Collection Checker JP
Input image: style reference only. Match its friendly app-icon family through the pale sky-blue rounded-square tile, very thick dark navy outlines, mint/coral/orange/yellow/teal palette, soft polished dimensional shading, and clear small-size silhouette. Do not copy the snail or its exact composition.
Primary request: Create a cute original mint-green collector squirrel mascot carrying an oversized cream-colored collection satchel. Three large abstract item cards peek from the satchel in coral, golden yellow, and teal; place one bold cream checkmark on the front of the satchel. The squirrel has a simple warm friendly face and a large curled tail that creates a strong recognizable silhouette.
Composition: centered, fills most of the square, generous safe margin, large simple shapes, minimal details suitable for 16 px reduction.
Background: pale sky-blue rounded square with genuinely transparent pixels outside the rounded corners.
Constraints: no text, no letters, no logo, no watermark, no Elden Ring symbol, no rune, no ring symbol, no recognizable game weapon, armor, character, or copyrighted design. Avoid clutter, thin lines, tiny objects, photorealism, and dark horror styling.
```

ファイルの同一性確認用SHA-256は次のとおり。

| ファイル | SHA-256 |
|---|---|
| `AppIconSource.png` | `89F3AF6FB3B329C78B496600FB41188157FA42C6495B8BAF4D727BFD8247FAD2` |
| `ERCollectionCheckerJP.png` | `B7860643CB7B32FC510B48246A1F9A530427D435F0C2AA655E330DEA2195A189` |
| `ERCollectionCheckerJP.ico` | `C40211877664A546EE839E7ECC3BA480D4136BF82CA645BEA0C07BD5786FEB2C` |

アイコンを変更した場合は、原画、生成プロンプト、参照画像、派生処理およびSHA-256をこの文書で更新する。
