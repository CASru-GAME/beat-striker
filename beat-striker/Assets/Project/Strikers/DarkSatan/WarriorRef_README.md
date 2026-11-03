# ラージウォーリア参照版：モーションと遷移

ラージウォーリアのアニメーションを参照した魔王モデルのモーションと遷移を説明します。

## 成果物

- `DemonKing_WarriorRef.blend`：骨、メッシュ、および4つのアクションを編集できます。アクションエディターで `WarriorRef_Idle`、`WarriorRef_JumpUp`、`WarriorRef_Fall`、`WarriorRef_Recovery` を選択してください。ソースに合わせて24fpsです。
- `Assets/Project/Strikers/DarkSatan/DemonKing_WarriorRef_All.fbx`：待機・上ジャンプ・落下・復帰をまとめたモデル。
- `DemonKing_WarriorRef_Preview.blend`：4つのアクションに加え、遷移と上下移動を確認する `WarriorRef_TransitionPreview` を収録しています。タイムラインのマーカーが状態切り替え位置です。
- `DemonKing_WarriorRef_transition.mp4`：遷移をつないだ確認動画。状態名を表示しています。
- `DemonKing_WarriorRef_comparison.png`：元モデルと魔王の待機・上昇開始・上昇末尾・落下姿勢の比較。姿勢を見やすくするため元モデルの槍を非表示にし、各画像のカメラ位置を調整しています。

個別クリップは、ゲーム側で上下移動を制御するためのアクションです。確認用の `TransitionPreview` は、後述の速度と重力から上下移動を近似して重ねたもので、ゲーム本体の実行結果ではありません。

## 実際に確認した参照元

`Assets/Project/Strikers/LargeSatan/MLSatan.prefab` が参照するのは、`Warrior.fbx` 内の次のクリップです。同じ名前と範囲を持つ `Warrior.blend` のモデルを表示して姿勢を確認し、アクションの各フレームから骨の向きを取得しました。FBXをBlenderに読み込んだ際にも対応する骨の向きがおおむね一致することを確認しています。

| 状態 | FBX内クリップ | フレーム範囲 | フェード |
|---|---|---:|---:|
| 待機 | `待機戦士ループ` | 0–25、ループ | 0.3秒 |
| 上ジャンプ | `ジャンプ上戦士` | 0–8、非ループ | 0.2秒 |
| 落下 | `落下戦士ループ` | 0–12、ループ | 0.3秒 |
| 接地後の復帰 | `待機臨戦戦士ループ` | 0–25、ループ | 0.3秒 |

元のBlenderシーンとFBXの時間設定は24fpsです。元の待機は体を横に構えながら顔を前へ向け、膝と腕を小さく動かします。上ジャンプでは腕を上に伸ばすのではなく、体を前傾させて膝を引き上げます。落下はほぼ姿勢を保持します。キャラクターの体格と衣装が異なるため、見た目が完全に同一になるわけではありません。

## ゲーム側で確認した状態遷移

1. `NormalGroup` のダッシュ要求から `DashNode` に入り、接地中かつ入力なし、または上方向入力が閾値を超えると `JumpState` に入ります。
2. `JumpState` はローカル入力の上方向が0.96を超えると上ジャンプを再生します。開始時の速度は38、重力を一時的に無効にし、0.4秒の間に速度を開始時の1%まで指数減衰させた後、`FallState` への遷移を試みます。退場時に元の重力設定を戻します。
3. `FallState` は落下クリップを再生し、`GroundChecker.IsGrounded` が真になると `LocomotionState` に遷移します。参照先は名前上 `landNode` ですが、実体は専用の着地アニメ状態ではありません。
4. `LocomotionState` は臨戦姿勢を0.3秒再生し、`LocomotionNode` が横入力に応じて待機・前歩行・後ろ歩行を選びます。入力がなければ `IdleState` に戻ります。

空中のダッシュ要求には別の分岐があり、下入力なら急降下、それ以外は空中ジャンプと空中スーパージャンプが交互に選ばれます。接地時に空中ジャンプのカウンターはリセットされます。同梱のジャンプモーションは上ジャンプに対応しています。

この挙動は `States/Dash/JumpState.cs`、`States/Dash/FallState.cs`、`States/Dash/DashNode.cs`、`States/Locomotion/LocomotionState.cs`、`States/Locomotion/LocomotionNode.cs`、および `Battle/Striker/Util/AnimationPlayer.cs` に基づきます。プロジェクト設定の重力はY方向に−50です。

## 検証と制約

待機・上ジャンプ・落下・復帰について、それぞれ3フレームずつ、体幹・頭・腕・脚・足の17ボーンの向きをFBXの元クリップと比較しました。サンプルでの最大差は約4.1度です。統合FBXをBlenderに読み戻し、4つのアクションが含まれることも確認しています。

肘・膝付近にはウェイトの混合を加えましたが、モデル自体は多数の分割メッシュで構成されているため、強い曲げやコートとの接触では継ぎ目や貫通が残る可能性があります。UnityでのHumanoid設定、ゲーム内への組み込み、実機での接地と描画負荷の確認は行っていません。
