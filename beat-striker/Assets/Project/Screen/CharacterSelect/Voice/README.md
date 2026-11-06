# チャレンジモードの初回解説

音声: **VOICEVOX:ずんだもん**（ノーマル、話者 ID 3）。
`voice-lines.json` に3ページ分の読み上げ原稿を保存している。

VOICEVOX、Python 3、ffmpegを用意し、リポジトリのルートから生成する。

```sh
python beat-striker/Assets/Project/Battle/UI/Tutorial/Voice/generate_voice.py --manifest beat-striker/Assets/Project/Screen/CharacterSelect/Voice/voice-lines.json
```

再生成時は `--overwrite` を付ける。接続先は `--engine` で指定できる。
音声は `TrialIntroductionDialog.prefab` のナレーション用AudioSourceで再生し、ページ変更・終了時に停止する。
音量はマスター音量と効果音音量に従う。

初回説明の完了状態と最高級到達後の連覇数は、名前ごとに端末内のプレイヤー記録へ保存する。
既にチャレンジの対戦履歴を持つ名前には初回説明を表示しない。
最高級での勝利から1連覇として数え、敗北すると連覇数を0へ戻す。階級は下がらない。
既存のサーバー履歴から過去の連覇数を復元する機能や、端末間での連覇数の同期は含まない。

Unityの `Beat Striker > Authoring > Challenge presentations` は、チャレンジ用プレハブとシーン参照を再構成する。
演出時間・色・音声・UI参照は各Viewのインスペクタで編集できる。手動調整後の再構成はその調整を置き換える。
`Beat Striker > Preview > Challenge > Capture presentations` は、結果・途中姿勢・説明・昇格戦の画像を `Temp/ChallengePreview` に出力する。
