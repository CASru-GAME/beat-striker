# 魔王 3Dモデル

キャラクター選択画面の右端にある立ち絵（`Assets/Project/Screen/CharacterSelect/a.png`）を参考に、Blender 5.1で制作したデフォルメキャラクターです。赤い肌と角、黒いボブヘア、金縁のロングコート、腰のチェーン、マイクを立体化しています。画像に写っていない背面は、前面のデザインに合わせて補っています。

## ファイル

- `DemonKing.blend`：編集用のBlenderシーン。パーツ、マテリアル、骨、カメラ、照明を含みます。参照画像はプロジェクト内の画像を相対パスで参照し、初期状態では非表示です。
- `DemonKing.fbx`：骨とマテリアルを含むニュートラルポーズのモデル。
- `DemonKing.glb`：骨とマテリアルを含むニュートラルポーズのモデル。
- `DemonKing_preview.png`：マイクを持つポーズのレンダリング。
- `DemonKing_front.png`、`DemonKing_back.png`：ニュートラルポーズの正面・背面。
- `create_demon_king.py`：モデルと書き出しファイルの生成スクリプト。

## 編集

`.blend` を開くと、フレーム40のポーズを表示します。タイムラインをフレーム1に移すとニュートラルポーズになります。アウトライナーの `DemonKing_Rig` を選んでポーズモードに切り替えると骨を操作できます。マイクも手の骨に追従します。FBXとGLBには、この確認用ポーズアニメーションは含めていません。

キャラクターの高さは角を含めて約3.47 Blender単位です。全身は約150の独立メッシュからなり、ウェイトは主にパーツ単位で割り当てています。大きく動かす場合は関節やコートのウェイト・干渉調整が必要です。ゲームでは `../TutorialDemon/TutorialDemon.prefab` の表示モデルとして使用します。Humanoid の骨割り当てを設定し、魔王専用に複製した状態スクリプトと戦士の Humanoid モーションを使用します。描画負荷を抑えるためのメッシュ統合は行っていません。

## 再生成

リポジトリのルートから実行します。`blender` コマンドがPATHにない場合は、利用するBlenderの実行ファイルを指定してください。

```powershell
blender --background --factory-startup --python-exit-code 13 --python 'beat-striker\Assets\Project\Strikers\DarkSatan\create_demon_king.py'
```
