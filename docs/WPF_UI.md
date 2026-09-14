# WPF / MVVM 収集一覧UI

## 1. 目的

Phase 5の製品画面は、`SaveAnalysisService`が返すimmutableな解析結果を表示・検索・
フィルターする。WPF側に別のセーブparserや独自の所持判定を持たせない。

## 2. 起動とRuntime境界

- 起動時に実行ファイルへ埋め込んだ5 pack / 16 filesを一時展開してfail-closedで検証する。
- Catalog snapshotの構築に成功した場合だけ`MainWindow`を開く。
- DB欠落・version不一致・アクセス拒否は、絶対pathやstack traceを表示せず終了する。
- Catalogのロード後に一時展開物を削除し、build/publish出力には個別JSONを配置しない。

## 3. 解析フロー

1. `%APPDATA%\EldenRing` 以下に`ER0000.sl2`が1件だけあれば候補にする。複数ある場合は自動選択しない。
2. 選択したセーブから全10 slotを読み、使用中slotだけを`スロット番号：キャラクター名`で列挙する。
3. ユーザーがキャラクターを選び、解析を実行する。
4. セーブは前後2回の読み取りsnapshotとSHA-256一致で不変を確認する。
5. CPU負荷のあるparser/判定はUI thread外で実行する。
6. 成功時だけ結果を一括差し替える。失敗時は前回の成功結果を保持する。

## 4. 表示とフィルター

- `コレクション対象` / `取得可能な全アイテム`の切り替え（英語表示はCollection / Strict All Items）
- 全体とカテゴリ別の進捗
- Owned / Missing / Unknownを分離し、Unknownを完了率の分母に入れない
- 判定coverageとCollection分類保留件数を別表示
- 日本語/英語の即時切り替え（再解析不要）
- 日本語名・英語名の両方を対象にした検索
- 状態、カテゴリ、Base Game / SOTE / Tarnished Packの絞り込み
- 列見出しクリックによる一覧ソート
- 行選択、`名称をコピー`ボタン、`Ctrl+C`で現在の表示言語のitem名だけをclipboardへコピー。
  DataGrid標準の複数セルコピーは無効にし、カテゴリやDLC列による上書きを防ぐ
- 防具のexact所持と`CoveredByConversion`の区別
- 開発者モードでのみ、判定メモ列と選択itemの詳細・Param ID・内部keyを表示

フィルターと進捗は`SaveAnalysisViewQuery`に集約し、画面コードで対象範囲や分母を
再定義しない。

## 5. DataOnlyの扱い

DataOnly 599件はデフォルト非表示であり、いずれのmodeでも進捗対象にならない。
開発者モードを有効にして`DataOnlyを表示`を選ぶと、状態フィルターを`Excluded`へ切り替え、
データ上に存在することと除外理由だけを確認できる。開発者モードを解除すると
DataOnlyを再び非表示にし、状態フィルターを`Missing`へ戻す。Param IDと内部keyも
開発者モードでのみ表示する。

## 6. 現在の検証

- WPF Release build / XAML compile成功
- STA thread上で`MainWindow.Show()`と初回layoutを実行する起動回帰テスト
- Application queryのmode、state、category、content pack、日英検索、DataOnlyのunit test
- App ViewModelの日英表示、使用中slot列挙、キャラクター名、mode別scope、DataOnlyトグルのunit test
- 言語切替後もキャラクター・状態・カテゴリのComboBox選択を保持するWPF binding回帰
- item名clipboardコピーと前回セーブパスのJSON保存・復元テスト
- baseline / all-attiresを同じ製品APIで再評価し、既定件数と読み取り前後SHA-256一致を確認

## 7. 既知の制限

- level、play time、NG周回数など、キャラクター名以外のslot metadataは未実装。
- 最後に正常読込したセーブパスだけをLocalAppDataのJSONへ原子的に保存する。
- 自動再読み、CSV/JSON export、その他の設定永続化はPhase 6で実装する。
- 実ウィンドウの目視・操作回帰はWindows実機のRelease gateに残る。
