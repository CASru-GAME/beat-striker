# App フォルダ内の非同期処理完全廃止 + SceneView シーン切り替え検知実装

実施日: 2025年10月27日

## 実施内容

### 1. ✅ SceneView にシーン切り替え検知イベントを追加

**ファイル**: `Assets/Scripts/App/Views/SceneView.cs`

```csharp
public event Action<AppScene> OnSceneLoadStarted;  // 読み込み開始時
public event Action<AppScene> OnSceneLoadCompleted; // 読み込み完了時

public void LoadScene(AppScene scene)  // 同期メソッド
```

**機能**:
- シーン読み込み前に `OnSceneLoadStarted` イベント発火
- シーン読み込み後に `OnSceneLoadCompleted` イベント発火
- 安全性: シーン名がない場合はエラーログを出力して処理を止める

---

### 2. ✅ ISceneView インターフェースの同期化

**ファイル**: `Assets/Scripts/App/Views/ISceneView.cs`

**変更**:
- `Task LoadSceneAsync()` → `void LoadScene()`
- イベント定義を追加:
  ```csharp
  event Action<AppScene> OnSceneLoadStarted;
  event Action<AppScene> OnSceneLoadCompleted;
  ```

---

### 3. ✅ ISceneState インターフェースを同期に戻す

**ファイル**: `Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneState.cs`

```csharp
// before
public interface ISceneState {
    Task Enter();
    Task Exit();
}

// after
public interface ISceneState {
    void Enter();
    void Exit();
}
```

---

### 4. ✅ ISceneStateController インターフェースを同期に戻す

**ファイル**: `Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneStateController.cs`

```csharp
// before
Task ChangeState(ISceneState newState);

// after
void ChangeState(ISceneState newState);
```

---

### 5. ✅ SceneStatePresenter を同期化

**ファイル**: `Assets/Scripts/App/Presenters/Scene/ScenePresenter.cs`

```csharp
public void ChangeState(ISceneState newState) {
    currentState?.Exit();
    currentState = newState;
    currentState?.Enter();
}
```

**特徴**:
- シンプルな同期処理
- Exit → Enter の順序を守る
- Context に controller/factory を設定（循環依存解消）

---

### 6-10. ✅ 全 State クラスを同期化

#### TitleState, StageSelectState, CharacterSelectState, BattleState
- `async Task Enter()` → `void Enter()`
- `async Task Exit()` → `void Exit()`
- `await Task.CompletedTask` を削除
- `_ =` 破棄を削除

#### TransitionState
```csharp
public void Enter() {
    context.bus.Subscribe<RequireTransitionMessage>(OnAppFlowMessage);
    context.bus.Publish(new TransitionStartedMessage(nextScene));
    context.view.LoadScene(nextScene);  // 同期呼び出し
}

public void Exit() {
    context.bus.Unsubscribe<RequireTransitionMessage>(OnAppFlowMessage);
}
```

---

## 修正前後の比較

### 修正前の問題
```
async void / Task による複雑性
     ↓
await による待機
     ↓
SceneLoadAsync() の Task チェーン
     ↓
シーン切り替えのタイミング不明確
```

### 修正後の動作フロー
```
State.Enter()
  ↓
Subscribe + Publish
  ↓
view.LoadScene()  (同期)
  ↓
OnSceneLoadStarted イベント発火
  ↓
SceneManager.LoadScene() 実行
  ↓
OnSceneLoadCompleted イベント発火
  ↓
(必要ならここでリスナーが処理)
```

---

## イベント利用例

```csharp
// SceneView のイベントをリッスンして、画面フェード等を制御
sceneView.OnSceneLoadStarted += (scene) => {
    Debug.Log($"Loading scene: {scene}");
    // フェードアウト開始
};

sceneView.OnSceneLoadCompleted += (scene) => {
    Debug.Log($"Scene loaded: {scene}");
    // フェードイン開始
};
```

---

## 修正ファイル一覧

```
Assets/Scripts/App/Views/ISceneView.cs
Assets/Scripts/App/Views/SceneView.cs
Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneState.cs
Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneStateController.cs
Assets/Scripts/App/Presenters/Scene/ScenePresenter.cs
Assets/Scripts/App/Presenters/Scene/States/TitleState.cs
Assets/Scripts/App/Presenters/Scene/States/StageSelectState.cs
Assets/Scripts/App/Presenters/Scene/States/CharacterSelectState.cs
Assets/Scripts/App/Presenters/Scene/States/BattleState.cs
Assets/Scripts/App/Presenters/Scene/States/TransitionState.cs
```

---

## メリット

✅ **シンプル**: async/await が無いため読みやすい
✅ **デバッグ容易**: 同期処理なのでスタックトレースが明確
✅ **イベント駆動**: シーン切り替えのタイミングが検知可能
✅ **状態確実**: Subscribe/Unsubscribe が確実に実行される
✅ **リーク防止**: Task による隠れた非同期処理がない
