# チュートリアル音声

音声: **VOICEVOX:ずんだもん**（ノーマル、話者 ID 3）。

`voice-lines.json` の `Clips` が読み上げ原稿、`Lines` が各ページの再生順を表す。
`$East`、`$South`、`$West`、`$North`、`$Start` は現在の入力環境とボタン配線に対応する操作名の音声に置き換わる。
`$Walk`、`$Direction` はキーボードとスティックの移動案内を切り替える。
練習画面では音声を再生しない。

VOICEVOX を起動し、リポジトリのルートから実行する（Python 3 と ffmpeg が必要）。

```sh
python beat-striker/Assets/Project/Battle/UI/Tutorial/Voice/generate_voice.py
```

音声は約2倍に増幅し、ピークを抑えてからMP3に変換する。既存の音声を再生成する場合は `--overwrite` を付ける。
エンジンの接続先は `--engine http://localhost:50021` で指定できる。
生成後、Unity の `Tools > Battle > Configure Tutorial Narration` を実行すると、
MP3 のインポート設定、既存の Default Local Group への Addressables 登録、
音声ライブラリと Battle プレハブの参照を更新する。

実行時は Addressables で必要な音声を読み込み、Battle の破棄時に解放する。
プレハブに配置したナレーション用 AudioSource で再生し、ページ切り替えと練習成功時に停止する。
音量はマスター音量と効果音音量に従う。
