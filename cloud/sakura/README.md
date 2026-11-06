# さくらのレンタルサーバー用API

名前・階級の共有、階級別ランキング、プレイヤー別対戦履歴、リプレイ保存・取得をPHPとSQLiteで提供します。
Composer、Node.js、Firestore、MySQLの作成、URL書き換え設定は不要です。
さくらのサーバー契約の範囲で動作し、このAPIにGoogle Cloudの利用料は発生しません。

## ローカルからデプロイ

さくらの本番反映はローカルから行います。サーバーでPHP 8.2以降と `pdo_sqlite` を有効にし、作業端末のPATHに `pwsh` と `curl` を用意してください。

`cloud/sakura/.env.local` にさくらのFTP認証情報をUTF-8の `KEY=値` 形式で記載します。このファイルはGitの管理対象外です。

```dotenv
SAKURA_FTP_USERNAME=your_ftp_username
SAKURA_FTP_PASSWORD=your_ftp_password
```

リポジトリのルートで実行します。

```powershell
pwsh -File ./cloud/sakura/Upload-SakuraApi.ps1 -EnvFile ./cloud/sakura/.env.local
```

スクリプトは明示的TLSのFTP（FTPS）で `index.php` を別名で転送し、転送完了後に公開ファイルを置き換えます。SQLiteは公開ディレクトリ外に置きます。置き換え後はAPIのヘルスチェック、対戦履歴一覧、感想の受付確認、公開感想一覧を確認し、すべて成功すると `API稼働確認完了` と表示します。

設置先が異なる場合は `-FtpHost`、`-RemoteDirectory`、`-ApiBaseUrl` で指定します。GitHub ActionsはPHPの構文とHTTP APIの検証に利用できます。本番への反映結果はローカルのコマンドと公開URLで確認してください。

## FTPで手動設置する場合

1. サーバーコントロールパネルでPHP 8.2以降を選択します。`PDO`と`pdo_sqlite`が必要です。
2. FTPクライアントでサーバーに接続し、`www/beat-striker/`を作成します。
3. このフォルダ内の **`www/beat-striker/index.php`だけ** を、サーバーの同じ場所へアップロードします。
   文字コードはUTF-8、通常のファイル権限は644、公開ディレクトリは755です。
4. `https://rintify.sakura.ne.jp/beat-striker/index.php`を開きます。
   `{"ok":true,"service":"beat-striker-api","storage":"sqlite"}`が返ればDBへの接続も成功しています。
5. `https://rintify.sakura.ne.jp/beat-striker/index.php?route=%2Fbattle-histories&limit=1`を開きます。
   初回は`{"items":[]}`です。ゲームで対戦を終えると保存され、ランキング画面に表示されます。

サーバー上の構成は以下になります。SQLiteファイルは最初のアクセス時に自動作成します。

```text
FTPログイン先/
├── www/
│   └── beat-striker/
│       └── index.php            ← アップロードするファイル
└── beat-striker-data/
    └── history.sqlite3          ← APIが自動作成する永続データ
```

`beat-striker-data`を`www`内に作らないでください。APIも公開ルート配下への保存を拒否します。
サーバーのホームディレクトリにPHPがフォルダを作れない場合は、FTPで`www`の隣に
`beat-striker-data`を作成し、所有者の書き込みを許可します（通常700）。777は不要です。
上記と異なる階層へ設置する場合は、`index.php`内の`$directory`を公開領域外の保存先へ変更してください。

## Unityの設定

`Assets/Project/App/App.prefab`の`AppNetworkSetting`にある`Cloud Api Base Url`を設定します。
現在の接続先は`https://rintify.sakura.ne.jp/beat-striker`です。末尾に`index.php`を付けません。
`BattleHistoryApiClient`が`index.php?route=...`形式のURLを作るため、`.htaccess`やPATH_INFOには依存しません。
すでに配布したゲームには接続先が反映されないため、Unityで再ビルドして配布してください。

## API

すべて`index.php`にアクセスし、クエリの`route`で処理を指定します。

| HTTP | route | 処理 |
| --- | --- | --- |
| GET | `/`（省略可） | APIとSQLiteのヘルスチェック |
| POST | `/players` | 名前でプレイヤー記録を取得。未登録ならピアニッシモ級で作成 |
| GET | `/players` | `grade=0..5`の階級別一覧。初回獲得日時・登録順で50人ずつ取得 |
| POST | `/battle-histories` | 対戦履歴・リプレイ保存 |
| GET | `/battle-histories` | 最新順の履歴。`limit`省略時50、最大100 |
| GET | `/battle-histories/{id}` | リプレイを含む詳細 |
| GET | `/feedback` | `name`を指定し、受付済みかを`submitted`で取得 |
| POST | `/feedback` | `name`と`comment`を保存。同じ名前の再送は409 |
| GET | `/feedback-list` | 公開の感想一覧。最新順に50件ずつ表示 |
| POST | `/scores` | 名前と数値スコア保存（既存API互換） |
| OPTIONS | 任意 | WebGL向けCORSプリフライト |

### 感想の受付と公開一覧

ローカル対戦の結果メニューと、チャレンジで「一旦終わる」を選んだ後のタイトル画面で、受付前の名前に感想フォームを表示します。ゲストとCPUは対象外です。同じ名前で参加したプレイヤーには1件だけ表示します。

感想は0〜200 Unicodeコードポイントです。空欄で送信した場合も受付済みになります。同じ名前で送信が重なった場合は、先に保存した1件を保持します。名前の比較はプレイヤー記録と同じです。初回送信者の認証はありません。

感想は名前とともに公開されます。デプロイ後はAPIの `index.php?route=%2Ffeedback-list` で閲覧できます。

### 名前・階級・プレイヤー別履歴

名前は前後の空白を除いた1〜10文字です。認証はなく、同じ名前は端末を問わず同じ記録を使います。
大文字・小文字、全角・半角は区別します。`POST /players`には`name`と初回使用時刻`createdAt`（UTCのISO 8601）を渡します。
階級番号はピアニッシモ、ピアノ、メゾピアノ、メゾフォルテ、フォルテ、フォルティッシモの順に0〜5です。

`GET /players`の`grade`指定と`GET /battle-histories`の`player`指定は、レスポンスの`nextCursor`を次のリクエストの`cursor`へ渡して続きを取得します。
実際のURLでは`route`をクエリに指定し、`grade`・`player`・`cursor`も別のクエリパラメータとして指定します。
プレイヤー別履歴は対象者を絞ってからページを区切ります。CPUはプレイヤー一覧へ登録しません。
プレイヤー別履歴は対戦完了時刻の降順、同時刻なら保存順の降順です。オフライン結果を後から送信しても実際の対戦時刻に並びます。
カーソルは不透明な値としてそのまま渡してください。全体履歴の`limit`指定は従来どおり保存順で取得します。

新しい対戦保存には32桁の16進数`battleId`、`mode`（`Local`・`Trial`・`Online`）、2人分の`participants`を含めます。
各参加者は`name`・`grade`・`isCpu`・`cpuLevel`を持ち、対戦開始時点の表示情報を残します。
試練は`trialStartGrade`と`trialTargetGrade`も保存し、1Pの勝利時にだけ昇級します。最高階級後の目標階級は`-1`です。
試練の対戦相手・段階別CPUレベルはUnityの`TrialSetting`で設定します。APIは段階・対象階級・参加者種別・CPUレベルの範囲を検証し、相手と段階別レベルの表は持ちません。
同じ対戦IDの再送は既存の履歴IDを返し、昇級と履歴保存は同じトランザクションで処理します。

オフラインの対戦完了時刻も初回獲得順へ反映するため、順序は端末のUTC時刻を使います。
同名の別端末から結果が届いても最高階級を保持し、再挑戦で初回獲得日時を後の時刻へ更新しません。
同じ名前の初回使用時刻が後から早まった場合は登録日時を更新し、ピアニッシモ級の獲得日時にも反映します。昇級済みの獲得日時は変更しません。
既存データベースには必要なテーブル・インデックス・対戦時刻列を追加し、保存済みの履歴から並び替え用のUTC時刻を補完します。以前のゲスト履歴は保存したまま、実名へ推定で紐付けません。

JSONのフィールドはUnityの`BattleHistoryContracts.cs`と共通です。
一覧にはリプレイ本体を含めません。本文上限は25MiB、通信タイムアウトはUnity側で30秒です。
PHP側の`post_max_size`やWebサーバーの本文上限がこれより小さい場合は、その上限が優先されます。

現在は従来のAPIと同じ公開読み書き方式です。ゲームから送られた勝敗を保存するため、
サーバーによる勝敗検証やユーザー認証は行いません。

## 更新・バックアップと移行

更新は`index.php`のみ上書きします。`beat-striker-data`はそのまま残してください。
バックアップは書き込みがない時間帯に、FTPで`beat-striker-data`全体をダウンロードします。
`history.sqlite3-journal`が存在する場合も含めて扱い、整合したバックアップにはゲームからの書き込みを止めてください。

初回のSQLiteは空です。既存のFirestoreの履歴は自動移行されません。
この変更は対戦履歴APIのみです。オンライン対戦のPhoton / Relayサーバーは別の仕組みです。
旧Cloud Run用のコードとTerraformは保守用に残しています。

## トラブルシューティング

- **403 / 404**: `www/beat-striker/index.php`の配置、権限、アクセス制限を確認します。
- **500 / storage error**: PHPのエラーログ、PDO SQLiteの有効化、保存先ディレクトリの書き込み権限を確認します。
  内部パスやSQLエラーの詳細はAPIのレスポンスには出しません。
- **413**: リプレイ本文が25MiBを超えていないか、サーバー側の制限を確認します。
- **API画面は動くがゲームが通信エラー**: AppプレハブのURLと再ビルド済みかを確認します。
  WebGLではゲームとAPIをHTTPSに統一してください。

## ローカル検証

PHP（`pdo_sqlite`有効）とPython 3をPATHで使える状態で、リポジトリのルートから実行します。
HTTPサーバーとDBは一時ディレクトリ内に作り、テスト終了時に片付けます。本番サーバーへは接続しません。

```powershell
php -l cloud/sakura/www/beat-striker/index.php
python cloud/sakura/tests/test_api.py
```

別のPHP実行ファイルやiniを使う場合は`--php`、`--php-ini`を指定します。
保存→一覧→詳細、再起動後の永続化、日本語、件数上限、リプレイ大容量、入力不正、CORS、
サイズ超過、ストレージ障害を実際のPHPとSQLiteで検証します。

さくらの公式仕様: [基本仕様（PHP・SQLite・FTPS）](https://help.sakura.ad.jp/rs/2251/)
