# さくらのレンタルサーバー用API

対戦履歴の保存、ランキング画面の履歴一覧、リプレイ詳細取得をPHPとSQLiteで提供します。
Composer、Node.js、Firestore、MySQLの作成、URL書き換え設定は不要です。
さくらのサーバー契約の範囲で動作し、このAPIにGoogle Cloudの利用料は発生しません。

## GitHub Actionsで自動デプロイ

`.github/workflows/deploy.yml`の **Deploy Sakura API** が、`main`へのpush時に動作します。
対象は`cloud/sakura/`またはワークフローの変更です。Pull Requestではテストのみ実行します。
Actionsの「Run workflow」から`main`を指定して手動実行することもできます。

初回はリポジトリの **Settings → Secrets and variables → Actions → Secrets** に以下を登録します。

| Repository secret | 値 |
| --- | --- |
| `SAKURA_FTP_USERNAME` | さくらのFTPアカウント名 |
| `SAKURA_FTP_PASSWORD` | FTP接続パスワード |

[このリポジトリのActions Secrets設定](https://github.com/CASru-GAME/beat-striker/settings/secrets/actions)
で登録できます。パスワードをソースコードやチャットに書く必要はありません。

接続先には以下の既定値を使います。変更する場合のみ、同じ設定画面の **Variables** に登録します。

| Repository variable | 既定値 |
| --- | --- |
| `SAKURA_FTP_HOST` | `rintify.sakura.ne.jp` |
| `SAKURA_FTP_DIRECTORY` | `/www/beat-striker` |
| `SAKURA_API_BASE_URL` | `https://rintify.sakura.ne.jp/beat-striker` |

サーバー側ではPHP 8.2以降と`pdo_sqlite`を有効にしてください。
テスト成功後、明示的TLSのFTP（FTPS）で`index.php`をアップロードし、
公開URLのヘルスチェックと一覧取得が成功することまで確認します。
認証情報が未登録の場合や疎通確認に失敗した場合は、Actionsを失敗として終了します。
同時デプロイは直列化し、データベースはアップロードや削除の対象に含めません。

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

### PowerShellからアップロード

リポジトリのルートで次を実行すると、FTPアカウントの入力画面が開きます。
パスワードはスクリプトや設定ファイルに保存しません。FTP接続は明示的TLS（FTPS）を使います。

```powershell
pwsh -File ./cloud/sakura/Upload-SakuraApi.ps1
```

FTPの接続先やログイン後のパスが異なる場合は引数で指定します。

```powershell
pwsh -File ./cloud/sakura/Upload-SakuraApi.ps1 -FtpHost example.sakura.ne.jp -RemoteDirectory /www/beat-striker -ApiBaseUrl https://example.sakura.ne.jp/beat-striker
```

スクリプトは`index.php`を上書きし、その後にヘルスチェックと一覧取得を行います。
既存サイトやデータベースは削除しません。手動のFTPクライアントでも同じ1ファイルを更新できます。

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
| POST | `/battle-histories` | 対戦履歴・リプレイ保存 |
| GET | `/battle-histories` | 最新順の履歴。`limit`省略時50、最大100 |
| GET | `/battle-histories/{id}` | リプレイを含む詳細 |
| POST | `/scores` | 名前と数値スコア保存（既存API互換） |
| OPTIONS | 任意 | WebGL向けCORSプリフライト |

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
GitHub Actionsのデプロイ先はさくらへ置き換えており、Cloud Runへのデプロイは実行しません。

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
