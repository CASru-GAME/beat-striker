# WindowsでのUnity ML-Agents 完全版導入・応用手順書（トラブルシューティング付き）

このドキュメントは、Windows環境においてゼロからUnity ML-Agentsを導入し、公式サンプル（3DBall）で学習の基礎を習得したのち、最終的に自身のオリジナルプロジェクト（格闘ゲーム）へAIを組み込むまでのあらゆる手順・仕様・エラー対処法を網羅した完全版マニュアルです。

チャットログの試行錯誤のプロセスをすべて反映し、**「なぜその操作が必要なのか」「エラーが出た際に裏で何が起きているのか」「どのようにリカバリーするか」** を詳細に解説しています。

---

## 1. 動作環境と必須ツールの準備

ML-Agentsはバージョン間における依存関係の要求が非常に厳しいため、正しい環境を構築することが成功の第一歩となります。

### 1-1. 要求されるバージョン
- **Unity Editor**: Unity 6 (6000.0.x) または 2022.3 LTS 以上
- **Python**: **3.10.x（例: 3.10.12 が最も安定）**
  - **【重要】** ML-Agents Release 21〜23 の `mlagents-envs` パッケージは、Pythonのバージョンが `3.10.1 以上 3.10.12 以下` であることを厳密に要求します。Python 3.11 以上がインストールされている場合、後述のインストールステップで確実に弾かれます。
- **Git**: GitHubからML-Agentsのリポジトリを直接クローンするために必要です。

### 1-2. Python 3.10.12のインストール時の注意
- 公式サイト（python.org）から `Windows installer (64-bit)` をダウンロードします。
- インストーラの最初の画面で、画面下部にある **「Add Python 3.10 to PATH」** のチェックボックスに必ずチェックを入れてからインストールを開始してください。
- インストール後、コマンドプロンプトまたはPowerShellを開き、`py -0` を実行してインストールされているPythonのリストを確認します。一覧の中に `-3.10-64` が存在していれば準備完了です。※Windows環境では `python` コマンドがMicrosoft Storeアプリに紐付いてしまうトラブルが頻発するため、Python Launcherである `py` コマンドを使用するのが確実です。

---

## 2. ML-AgentsのダウンロードとPython仮想環境（venv）の構築

Python標準機能の `venv` を使用して、ML-Agents専用の独立した仮想環境を構築します。これにより、他のPythonプロジェクトとのパッケージ競合を防ぎます。

### 2-1. 公式リポジトリのクローン
作業用のディレクトリ（例: `C:\Users\rinty\Projects\`）を開き、公式リポジトリをダウンロードします。
```powershell
git clone https://github.com/Unity-Technologies/ml-agents.git
cd ml-agents
```

### 2-2. 仮想環境の作成と有効化
Python 3.10系を明示的に指定して、仮想環境 `mlagents-env` を作成します。
```powershell
# 古い環境が残っている場合は確実に削除する
# rmdir /s /q mlagents-env

# Python 3.10を指定して仮想環境を作成
py -3.10 -m venv mlagents-env

# 仮想環境を有効化（Windows PowerShellの場合）
.\mlagents-env\Scripts\activate
```
※プロンプトの行頭に `(mlagents-env)` が表示されていれば、以降の `python` や `pip` コマンドはこの仮想環境内に対して実行されます。

### 2-3. パッケージとPyTorchのインストール
GPU（NVIDIA）を利用するための `cu121`（CUDA 12.1用）PyTorchと、ML-Agents本体をインストールします。
**【重要】** ここでは `pip install` ではなく、**`python -m pip install`** の形式を使うことを強く推奨します。

```powershell
# pipを最新にアップグレード
python -m pip install --upgrade pip

# PyTorchのインストール（PyTorch 2.1.1以上を推奨）
python -m pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

# ML-Agents環境用と本体パッケージをローカルからインストール
python -m pip install -e .\ml-agents-envs
python -m pip install -e .\ml-agents
```

### 2-4. セットアップ時のよくあるエラーと完全解決法

#### ❌ エラー1: `Fatal error in launcher: Unable to create process...`
- **症状**: `pip install -e .\ml-agents-envs` を実行した瞬間に発生する。
- **原因**: Windowsの `pip.exe` がPython本体へのパスを見失い、迷子になっている状態です。仮想環境のパスと現在の実行場所がズレている場合などに起こります。
- **解決策**: `pip` を直接叩かず、`python -m pip install -e .\ml-agents-envs` のように「Pythonモジュールとしてのpip」を呼び出すことで回避できます。

#### ❌ エラー2: `Package 'mlagents-envs' requires a different Python: 3.11.9 not in '<=3.10.12,>=3.10.1'`
- **症状**: metadataのビルド中に発生し、インストールがブロックされる。
- **原因**: 仮想環境がサポート対象外のPython 3.11系で作成されています。
- **解決策**: Python 3.10.x をインストールし、`deactivate` で仮想環境を抜けたのち、`Remove-Item -Recurse -Force .\mlagents-env` またはエクスプローラーから仮想環境フォルダを削除します。その後、必ず `py -3.10 -m venv mlagents-env` のように `-3.10` オプションを付けて作り直してください。

#### ❌ エラー3: `ERROR: .\ml-agents-envs is not a valid editable requirement...`
- **症状**: `-e .\ml-agents-envs` を指定した際に見つからないと言われる。
- **原因**: コマンドを実行しているディレクトリが間違っています（例: `C:\Users\rinty>` の直下で実行しているなど）。
- **解決策**: `cd C:\Users\rinty\ml-agents` のように、クローンした `ml-agents` フォルダのルートへ移動してからコマンドを実行してください。

---

## 3. Unity側のセットアップとML-Agentsパッケージの導入

UnityプロジェクトがPython側のAIと通信するための「耳と目」をインストールします。

### 3-1. Package Managerからの導入
1. Unity Editorで対象のプロジェクトを開きます。
2. 上部メニューから `Window > Package Manager` を開きます。
3. 左上の `+` ボタンをクリックし、**`Add package by name...`** を選択します。
4. 以下の名前を入力してインストールします。
   - **`com.unity.ml-agents`**: 基本パッケージ
5. **【仕様変更の注意】**: 以前のバージョンで利用されていた推論エンジン「Barracuda」は段階的に廃止され、最新のML-Agents（Release 22 / 4.0以降）では **「Sentis」** が標準インファレンスエンジンとなっています。コンパイルエラー（`The type or namespace name 'Sentis' does not exist...`）が発生する場合は、同様に `com.unity.sentis` (1.2.0以降) を追加インストールしてください。

---

## 4. 学習の基本を理解する：3DBall サンプルの実行手順

オリジナルゲームへ実装する前に、公式サンプルを用いて「PythonとUnityが正しく通信するか」「学習がどのように進むのか」を確認します。

### 4-1. Unityの準備
1. `Assets > ML-Agents > Examples > 3DBall > Scenes` にある `3DBall` シーンを開きます。
2. Hierarchyウィンドウから `Ball3DAgent`（またはそれに類するAgentオブジェクト）を選択します。
3. Inspectorウィンドウの **`Behavior Parameters`** コンポーネントを確認します。
   - `Behavior Name`: **3DBall** （ここの文字列が後のyaml設定と一致している必要があります）
   - `Model`: **空 (None)** （学習中はモデルを持たないためです）
   - `Behavior Type`: **Default** （Pythonと通信して学習を行うための設定です）

### 4-2. Pythonからの学習コマンド実行
仮想環境が有効なコマンドプロンプトで、リポジトリルート（例:`C:\Users\rinty\ml-agents`）へ移動し、学習コマンドを打ちます。
```powershell
python -m mlagents.trainers.learn .\config\ppo\3DBall.yaml --run-id=3DBallTest_01
```

### 4-3. 実行直後の挙動と通信の確立
コマンドを叩くと、巨大なUnityロゴのアスキーアートが表示され、以下のログが出力されて一時停止します。
```text
[INFO] Listening on port 5004. Start training by pressing the Play button in the Unity Editor.
```
この状態は **「Pythonトレーナーが起動し、Unityからの接続を待っている」** 状態です。
ここで **Unity Editorの「Play（再生）」ボタンを押す** と通信が確立し、学習が実際にスタートします。

### 4-4. 学習ログの読み方
通信が確立すると、ボールが落ちるごとに再試行が繰り返され、ターミナルに以下のようなログが流れます。
```text
[INFO] Connected to Unity environment with package version 4.0.2...
[INFO] 3DBall. Step: 12000. Time Elapsed: 40.689 s. Mean Reward: 1.188. Std of Reward: 0.670. Training.
[INFO] 3DBall. Step: 24000. Time Elapsed: 47.145 s. Mean Reward: 1.379. Std of Reward: 0.812. Training.
...
[INFO] 3DBall. Step: 96000. Time Elapsed: 83.926 s. Mean Reward: 74.756. Std of Reward: 30.753. Training.
```
- **Step**: AIが意思決定を行った回数。
- **Mean Reward**: 直近の平均報酬。3DBall環境ではボールを落とさずにバランスを保つ時間が長いほどプラス報酬が与えられるため、この数値が右肩上がりに成長していれば「AIが賢くなっている（学習が成功している）」証拠です。100.0に近づけばほぼ完璧です。

### 4-5. 予期せぬエラー（タイムアウト等）への対処
#### ❌ エラー: `mlagents_envs.exception.UnityTimeOutException`
- **症状**: Unity環境が応答せず、タイムアウトでPythonが強制終了する。
- **原因と対処法**:
  1. Pythonコマンド実行後、**Unity側でPlayボタンを押していない**。
  2. Agentの `Behavior Type` が **`Default` になっていない**（`Inference Only` 等になっていると通信しません）。
  3. Unityの構成にエラー（赤色エラー、NullReferenceなど）があり、再生してもゲームが正常に進行していない。Unityの `Console` を確認してください。

### 4-6. 学習の終了とファイルの抽出
- **中断方法**: Unity側のPlayボタンをもう一度押して再生を停止し、その後コマンドプロンプト上で `Ctrl + C` を押してPythonスクリプトを終了させます。（学習の途中でもそれまでの成果物は保存されます）
- **出力される主なファイル群の違いと用途 (`results` フォルダ内)**:
  - **`.onnx` (例: 3DBall.onnx, 3DBall-500911.onnx)**: これが真の目的ファイルである **推論用完成モデル** です。Unityの `Assets` に取り込み、本番環境でAIを動かすために使います。複数ある場合は数字が大きい方（学習ステップが進んだ方）を選びます。
  - **`.pt` (例: 3DBall-500911.pt)**: PyTorchの重みファイルです。Unityには入れず、Python側で独自に解析や追加操作を行う際に使います。
  - **`checkpoint.pt`**: 学習再開用データです。`--resume` フラグを付けて学習を再開する際に読み込まれます。
  - **`events.out.tfevents...`**: TensorBoardで学習グラフの推移を描画するためのログデータです。

### 4-7. 学習済みモデルをUnityに適用して遊ぶ
1. `results\3DBallTest_01\3DBall.onnx` を Unity Editor の `Assets/Models/` のような任意のフォルダにドラッグして取り込みます。
2. Unityの `Ball3DAgent` を選択し、`Behavior Parameters` の **`Model`** にこのONNXファイルをアタッチします。
3. **`Behavior Type` を「Inference Only」に変更** します。
4. Unity で Play すると、Python環境を起動していなくても、AIが自律的にバランスを取り続ける様子が確認できます。

---

## 5. オリジナル格闘ゲーム（beat-striker）学習環境の設計と構築

公式サンプルの仕組みを理解したら、いよいよ自身のプロジェクトに適用します。ここでの大原則は、**「Gitで落としたフォルダを全て自分のプロジェクトにコピーしてはいけない」** ことです。
Unity側はPackage Managerを使って必要なコア機能のみを入れ、Gitのフォルダは「外部の学習実行ツール」として切り分けて運用します。

また、いきなり「全操作可能な格闘ゲーム本編」を学習させようとすると、入力と選択肢が膨大すぎてAIが収束しません。必ず、狭いステージ・短い制限時間・限定されたアクションの **「学習専用のミニシーン」** を作成して段階的にアプローチしてください。

### 5-1. Agentスクリプトの実装（C#側の要件）
AIの頭脳となるスクリプト（例: `FighterAgent.cs`）を作成し、`Unity.MLAgents.Agent` クラスを継承させます。以下のメソッドをオーバーライドして実装を埋めていきます。

#### A: `OnEpisodeBegin()`
1ラウンド（1エピソード）が始まった瞬間に呼ばれます。キャラクターの位置、速度、HPを初期状態にリセットします。
```csharp
public override void OnEpisodeBegin() {
    rb.linearVelocity = Vector3.zero;
    transform.localPosition = new Vector3(-2f, 0, 0); // 自分の位置初期化
    opponent.localPosition = new Vector3(2f, 0, 0);   // 相手の位置初期化
    myHP = 100; opponentHP = 100;
}
```

#### B: `CollectObservations(VectorSensor sensor)` -> 【観測（目）】
AIが状況判断するための数値を渡します。ここで渡した数値の総計が、後述の `Space Size` と一致している必要があります。
```csharp
public override void CollectObservations(VectorSensor sensor) {
    sensor.AddObservation(transform.localPosition); // Vector3 なので 3
    sensor.AddObservation(opponent.localPosition);  // Vector3 なので 3
    sensor.AddObservation(Vector3.Distance(transform.localPosition, opponent.localPosition)); // float なので 1
    sensor.AddObservation(myHP / 100f);       // 正規化したHP: 1
    sensor.AddObservation(opponentHP / 100f); // 正規化した相手HP: 1
}
// この場合、3 + 3 + 1 + 1 + 1 で合計「9」の Space Size を要求します。
```

#### C: `OnActionReceived(ActionBuffers actions)` -> 【行動（手足）】
AIが選択した行動番号を受け取り、実際のゲームロジック（移動や攻撃）を適用します。また、結果に対する **報酬 (Reward)** もここで管理します。
```csharp
public override void OnActionReceived(ActionBuffers actions) {
    int moveAction = actions.DiscreteActions[0];   // 移動系の枝 (Branch 0)
    int attackAction = actions.DiscreteActions[1]; // 攻撃系の枝 (Branch 1)

    // 移動処理
    if (moveAction == 1) transform.position += Vector3.left * speed * Time.deltaTime;
    if (moveAction == 2) transform.position += Vector3.right * speed * Time.deltaTime;
    
    // 攻撃処理
    if (attackAction == 1) TryAttack();

    // 生存（タイムオーバー）を防ぐための微小な毎フレームペナルティ（消極的行動の防止）
    AddReward(-0.001f);
}
```

#### D: `AddReward(float value)` -> 【報酬設計（学習のキモ）】
格闘ゲームにおける報酬設計は極端にしてはいけません。「勝ったら+100、負けたら-100」だけでは、そこに至るまでの過程が分からずAIは混乱します。
- 相手に近づいた: `+0.01`
- 攻撃を当てた: `+0.5`
- ダメージを受けた: `-0.1`
- 攻撃を空振りした: `-0.02` (空振りループを抑制)
- 勝利（KO）: `+3.0`
- 敗北: `-3.0`
細かく報酬の階段を作ってあげることが格ゲーAI育成の最大のコツです。

#### E: `Heuristic(in ActionBuffers actionsOut)` -> 【テストプレイ用】
学習前に、「C#側のロジックが正常に動くか」「観測値や報酬がおかしくないか」を人間がキーボードで操作してテストするためのメソッドです。

### 5-2. Inspector コンポーネントの正しい設定方法

学習させる対象のGameObjectに以下のコンポーネントをアタッチし、正確に数値を設定します。

#### ① Behavior Parameters
- **Behavior Name**: `Satan` （などの任意の文字列。あとで作る `.yaml` の `behaviors:` 配下の名前と完全一致させること）
- **Vector Observation -> Space Size**: `9` （上記 `CollectObservations` で追加した項目の合計数）
- **Actions -> Continuous Actions**: `0` （今回は離散行動のみを使うため0）
- **Actions -> Discrete Branches**: `2` （移動と攻撃で2つの独立した指示系統を作るため）
  - **Branch 0 Size**: `3` （例: 0=何もしない, 1=左, 2=右）
  - **Branch 1 Size**: `2` （例: 0=攻撃しない, 1=攻撃する）
- **Behavior Type**: `Default`

#### ② Decision Requester
AIに対して「何フレームごとに意思決定を行わせるか」を管理します。
- 必須コンポーネントなので Add Component から追加してください。
- **Decision Period**: `1` 〜 `5` （格ゲーのような即応性が求められるものは数値を小さくします。5フレームに1回判断などが一般的です）。

---

## 6. YAML設定ファイルの作成と独自トレーニングの実行

### 6-1. yamlファイルの作成
Python側が「どういったハイパーパラメータで学習を回すか」を定義します。クローンした `ml-agents` フォルダの `config` 以下に `Satan.yaml` などの名前で作成してください。

```yaml
behaviors:
  Satan:  # 【重要】Unity側の「Behavior Name」と必ず同じ名前にする。さもないとエラー。
    trainer_type: ppo
    hyperparameters:
      batch_size: 128    # 最初は小さめ(64〜128)がおすすめ。大きすぎると学習が重い。
      buffer_size: 4096
      learning_rate: 3.0e-4
      beta: 5.0e-4       # 探索の強さ。大きすぎるとランダム行動が多くなる。
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
    network_settings:
      normalize: true    # HP(0〜100)や速度などスケールが違う値を入れる場合はtrue推奨
      hidden_units: 128  # 128〜256程度で十分
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 2000000   # 学習の最大ステップ数。10,000,000などは長すぎるため最初は減らす。
    time_horizon: 128
    summary_freq: 50000  # ターミナル等にログを出力する間隔
```

### 6-2. トレーニングの実行と `--run-id` エラーの注意

設定ファイルが準備できたら、コマンドプロンプト（仮想環境有効化済み）から学習を開始します。
```powershell
mlagents-learn config/Satan.yaml --run-id=SatanTrain_01
```

#### ❌ エラー: `Previous data from this run ID was found.`
- **原因と仕組み**: `--run-id` は「学習データの保存ディレクトリ名」としても扱われます。以前に `SatanTrain_01` で学習したフォルダが `results/` 直下に存在している場合、誤って上書きしないよう保護機能が働き、このエラーを出して停止します。
- **解決手段の使い分け**:
  1. **別名で新規スタート**: `--run-id=SatanTrain_02` のように名前を変えるのが最も安全で一般的です。
  2. **学習の続きから再開する**: 途中で停止した学習を同じモデルから再訓練したい場合は `--resume` フラグをコマンドの末尾に付与します。
  3. **強制上書き破棄**: 前回の結果がゴミで完全に上書きして良い場合は `--force` フラグを付与します。

コマンド実行後、再度待ち受け状態になったら、Unityへ戻り `Play` を押すことで、自身の格闘ゲームのAIが学習を開始します。モデルができあがったら、前述のサンプル同様に `.onnx` ファイルをエクスポートして Unity の `Inference Only` で実践させましょう。
