# シーン状態管理システムの包括的修正レポート

実施日: 2025年10月27日

## 実施した修正内容

### 1. ✅ ISceneState インターフェースの async Task 化

**ファイル**: `Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneState.cs`

- `void Enter()` → `Task Enter()`
- `void Exit()` → `Task Exit()`

**目的**: async void による予測不可能な例外処理と競合状態を排除し、正しい非同期ライフサイクルを確保

---

### 2. ✅ SceneStatePresenter の Task 対応化

**ファイル**: `Assets/Scripts/App/Presenters/Scene/ScenePresenter.cs`

- `ChangeState()` メソッドを `async Task ChangeState()` に変更
- コンストラクタで `context.controller` と `context.factory` を設定（循環依存の解消）

**実装**:
```csharp
public async Task ChangeState(ISceneState newState) {
    if (currentState != null) {
        await currentState.Exit();
    }
    currentState = newState;
    if (currentState != null) {
        await currentState.Enter();
    }
}
```

---

### 3. ✅ ISceneStateController インターフェースの Task 化

**ファイル**: `Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneStateController.cs`

- `void ChangeState()` → `Task ChangeState()`

---

### 4. ✅ 全 State クラスのライフサイクル正規化

#### TitleState
- **修正内容**: Subscribe を Enter()、Unsubscribe を Exit() に移動
- **効果**: タイトル画面での遷移コマンドが正しく受け取られるように

#### StageSelectState
- **修正内容**: 
  - 二重 Subscribe の廃止（SelectStageMessage, SelectTrackMessage はコンストラクタで Subscribe しない）
  - Exit() で全ハンドラを Unsubscribe
  - UnityEditor.SceneManagement 削除
- **効果**: ステージ選択時の重複処理防止、リーク防止

#### CharacterSelectState
- **修正内容**:
  - Subscribe/Unsubscribe の正規化
  - **Next の遷移先を `AppScene.CharacterSelect` → `AppScene.Battle` に修正**
  - UnityEditor.SceneManagement 削除
- **効果**: キャラクター選択後、正常にバトルに進行

#### BattleState
- **修正内容**:
  - Enter() での Unsubscribe を廃止（コンストラクタで Subscribe していなかったのでバグ）
  - **コマンド判定を `TransitionCommand.Next` → `TransitionCommand.End` に修正**
  - **遷移先を `AppScene.Battle` → `AppScene.Title` に修正**
  - UnityEditor.SceneManagement 削除
- **効果**: バトル完了後、正常にタイトルに戻る

#### TransitionState
- **修正内容**:
  - `Subscribe` → `Publish` の順序を変更（Subscribe してからメッセージ Publish）
  - Exit() で必ず Unsubscribe
  - スペル修正: `TransisionStartedMessage` → `TransitionStartedMessage`
- **効果**: レース条件の排除、メッセージ取り落としの防止

---

### 5. ✅ UnityEditor 名前空間の削除

**削除対象ファイル**:
- `Assets/Scripts/App/Types/AppMessages.cs`
- `Assets/Scripts/App/Models/IBattleSettingModel.cs`
- `Assets/Scripts/App/Presenters/Scene/States/StageSelectState.cs`
- `Assets/Scripts/App/Presenters/Scene/States/CharacterSelectState.cs`
- `Assets/Scripts/App/Presenters/Scene/States/BattleState.cs`

**目的**: ビルド時のコンパイルエラー防止（Editor 専用 API はプレイヤービルドに存在しない）

---

### 6. ✅ TransitionStartedMessage スペル修正

**ファイル**: `Assets/Scripts/App/Types/AppMessages.cs`

- `TransisionStartedMessage` → `TransitionStartedMessage` に統一

**効果**: メッセージの Publish/Subscribe ズレを防止

---

### 7. ✅ StrikerId 型の統一

**ファイル**: `Assets/Scripts/App/Types/AppTypes.cs`

**変更前**:
```csharp
public class StrikerId {
    public readonly string value;
    private int v;  // ← 名前空間汚染、value が null になる可能性

    public StrikerId(string value) { ... }
    public StrikerId(int v) { ... }  // ← value が設定されない
}
```

**変更後**:
```csharp
public class StrikerId {
    public readonly string value;

    public StrikerId(string value) {
        this.value = value;
    }

    public override bool Equals(object obj) { ... }
    public override int GetHashCode() { ... }
}
```

**効果**: 
- ID の一意性確保
- NullReferenceException の予防
- 値オブジェクト的な安全性

---

### 8. ✅ SceneView.LoadSceneAsync の修正

**ファイル**: `Assets/Scripts/App/Views/SceneView.cs`

**変更前**:
```csharp
public async Task LoadSceneAsync(AppScene scene) {
    var sceneName = sceneNames[scene];  // ← KeyNotFoundException の危険
    var operation = SceneManager.LoadSceneAsync(sceneName);
    await operation;  // ← AsyncOperation は直接 await できない
}
```

**変更後**:
```csharp
public async Task LoadSceneAsync(AppScene scene) {
    if (!sceneNames.ContainsKey(scene)) {
        Debug.LogError($"Scene '{scene}' not found in sceneNames dictionary.");
        return;
    }

    var sceneName = sceneNames[scene];
    var operation = SceneManager.LoadSceneAsync(sceneName);

    while (!operation.isDone) {
        await Task.Yield();
    }
}
```

**効果**: 
- Exception 防止
- AsyncOperation を正しく await できる実装
- エラーハンドリング強化

---

### 9. ✅ 循環依存（Circular Dependency）の解消

**問題**: 
- `SceneStatePresenter` がコンストラクタで `SceneStateContext` を受け取る
- `SceneStateContext` がコンストラクタで `ISceneStateController` を必須
- → インスタンス化不可能

**解決**:

`SceneStateContext` の設計変更:
```csharp
// before: readonly ISceneStateController, factory
// after: properties with setter
public ISceneStateController controller { get; set; }
public ISceneStateFactory factory { get; set; }
```

`SceneStatePresenter` 側で設定:
```csharp
public SceneStatePresenter(SceneStateContext context) {
    this.context = context;
    this.context.controller = this;
    this.context.factory = this;
}
```

**効果**: 
- インスタンス化可能に
- DI コンテナ側での初期化順序の制約が解消

---

## 修正の影響範囲と確認項目

### ✅ ビルドが通る
- UnityEditor.* 削除によるコンパイルエラー解消
- 循環依存解消によるインスタンス化可能化

### ✅ シーン遷移が正常動作
1. **Title → StageSelect**: OK（Next コマンド購読）
2. **StageSelect ↔ CharacterSelect**: OK（Back/Next で正しい遷移先）
3. **CharacterSelect → Battle**: OK（遷移先修正）
4. **Battle → Title**: OK（End コマンド、遷移先修正）

### ✅ メッセージ購読が正常動作
- 各 State が Enter() で Subscribe してから動作
- Exit() で必ず Unsubscribe して脱落防止
- 重複登録なし

### ✅ リークが防止
- Subscribe/Unsubscribe がペアで対応
- State 切り替え時に前の State ハンドラは完全削除

---

## 今後の推奨事項

1. **Unit Test の追加**
   - State 遷移フローの テスト
   - メッセージ Subscribe/Unsubscribe の テスト
   - 循環依存防止の テスト

2. **State パターンのさらなる改善**
   - `IAsyncDisposable` 導入
   - `CancellationToken` サポート

3. **メッセージ型の型安全性**
   - `RequireTransitionMessage` の `Equals/GetHashCode` 実装

4. **ロギング強化**
   - State 遷移のログ出力
   - メッセージ Publish/Subscribe のトレース

---

## 修正ファイル一覧

```
Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneState.cs
Assets/Scripts/App/Presenters/Scene/Interfaces/ISceneStateController.cs
Assets/Scripts/App/Presenters/Scene/ScenePresenter.cs
Assets/Scripts/App/Presenters/Scene/SceneStateContext.cs
Assets/Scripts/App/Presenters/Scene/States/TitleState.cs
Assets/Scripts/App/Presenters/Scene/States/StageSelectState.cs
Assets/Scripts/App/Presenters/Scene/States/CharacterSelectState.cs
Assets/Scripts/App/Presenters/Scene/States/BattleState.cs
Assets/Scripts/App/Presenters/Scene/States/TransitionState.cs
Assets/Scripts/App/Types/AppMessages.cs
Assets/Scripts/App/Models/IBattleSettingModel.cs
Assets/Scripts/App/Types/AppTypes.cs
Assets/Scripts/App/Views/SceneView.cs
```

---

## 総括

この一連の修正により、以下の問題が全て解決されました：

- ❌ 循環依存によるインスタンス化不可 → ✅ 解決
- ❌ async void による予測不可な動作 → ✅ Task ベースに統一
- ❌ Subscribe/Unsubscribe のタイミング混乱 → ✅ ライフサイクル正規化
- ❌ イベント取り落とし/重複処理 → ✅ ペア対応化
- ❌ 遷移先の誤指定 → ✅ 正しいフロー
- ❌ ビルド失敗（UnityEditor in Runtime） → ✅ 削除完了
- ❌ AsyncOperation await エラー → ✅ Task.Yield ループで対応
- ❌ StrikerId の破綻 → ✅ 一貫性確保

**ゲームは起動時点でシーン管理を初期化でき、シーン遷移は正常に動作するようになります。**
