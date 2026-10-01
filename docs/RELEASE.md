# リリース手順

## 前提

- サポート中の最新.NET 10 SDKを使用する
- `data/1.17/runtime/`に検証済み非公開Runtimeデータ16ファイルがある
- Git作業ツリーがクリーンで、公開対象コミットを指している
- 全テストと実セーブの読み取り専用回帰が成功している
- `.\tools\Test-WithCoverage.ps1`が有効なカバレッジを生成する

## 生成

```powershell
.\tools\Publish-Release.ps1 -Version 0.1.1
```

スクリプトはrestore、Releaseテスト、自己完結型win-x64 publish、許可ファイル検査、ZIP作成、
SHA-256生成を行う。配布物には`SOURCE_COMMIT.txt`と、非公開データを公開せず同一性を確認するための
`RUNTIME_DATA.txt`を含める。同ファイルには同梱.NET Runtimeのバージョンも記録し、古いRuntimeを
誤って配布しないよう確認する。

## 公開前確認

- クリーンなWindows環境で`ERCollectionCheckerJP.exe`が起動する
- ZIP直下にJSON、PDB、セーブデータ、ゲーム原本が存在しない
- `SHA256SUMS.txt`とZIPの`.sha256.txt`が一致する
- EXEのファイル／製品バージョン、アイコン、製品名が正しい
- セーブ解析前後のSHA-256が一致する
- README、CHANGELOG、リリースノートの対応バージョンと既知の制限が一致する

## コード署名

現時点の配布物はコード署名なしである。署名証明書を導入する場合は、publish後かつハッシュ生成前に
`signtool`でEXEへ署名し、タイムスタンプと署名検証をリリースgateへ追加する。証明書や秘密鍵は
リポジトリへ保存しない。
