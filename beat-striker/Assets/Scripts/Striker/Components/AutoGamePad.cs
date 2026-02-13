
using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;
using UnityEngine;

/// <summary>
/// 指定した秒数間隔で指定したプレイヤーIDで指定したボタンを押し続けるコンポーネント
/// </summary>
public class AutoGamePad : MonoBehaviour {
    
    [Header("Settings")]
    [SerializeField] private GamePadId gamePadId = new GamePadId(114514);
    [SerializeField] private PlayerId playerId = new PlayerId(1);
    [SerializeField] private GamePadButton button = GamePadButton.East;
    [SerializeField] private float interval = 1.0f;
    [SerializeField] private float pressDuration = 0.1f;
    [SerializeField] private bool autoStart = true;
    [SerializeField] private bool registerToPlayerRegistry = true;

    private IBus bus;
    private float timer;
    private float pressTimer;
    private bool isRunning;
    private bool isPressed;
    private bool isRegistered;

    private void Start() {
        bus = this.GetBus();
        
        if (registerToPlayerRegistry) {
            Register();
        }
        
        if (autoStart) {
            StartAutoInput();
        }
    }

    private void OnDestroy() {
        if (isRegistered) {
            Unregister();
        }
    }

    private void Update() {
        if (!isRunning) return;

        if (isPressed) {
            pressTimer += Time.deltaTime;
            if (pressTimer >= pressDuration) {
                ReleaseButton();
            }
        }
        else {
            timer += Time.deltaTime;
            if (timer >= interval) {
                timer = 0f;
                PressButton();
            }
        }
    }

    /// <summary>
    /// PlayerRegistryに指定したPlayerIdで登録する
    /// </summary>
    public void Register() {
        if (isRegistered) return;
        bus.Publish(new AppMessages.JoinedWithPlayerId(gamePadId, playerId));
        isRegistered = true;
    }

    /// <summary>
    /// PlayerRegistryから登録解除する
    /// </summary>
    public void Unregister() {
        if (!isRegistered) return;
        bus.Publish(new GamePadMessages.Left(gamePadId));
        isRegistered = false;
    }

    /// <summary>
    /// 自動入力を開始する
    /// </summary>
    public void StartAutoInput() {
        isRunning = true;
        timer = 0f;
        pressTimer = 0f;
        isPressed = false;
    }

    /// <summary>
    /// 自動入力を停止する
    /// </summary>
    public void StopAutoInput() {
        isRunning = false;
        if (isPressed) {
            ReleaseButton();
        }
    }

    /// <summary>
    /// ボタンを押す（Down）
    /// </summary>
    private void PressButton() {
        bus.Publish(new GamePadMessages.Inputed(gamePadId, button, GamePadAction.Down));
        isPressed = true;
        pressTimer = 0f;
    }

    /// <summary>
    /// ボタンを離す（Up）
    /// </summary>
    private void ReleaseButton() {
        bus.Publish(new GamePadMessages.Inputed(gamePadId, button, GamePadAction.Up));
        isPressed = false;
    }

    /// <summary>
    /// 設定を変更する
    /// </summary>
    public void Configure(GamePadId gamePadId, PlayerId playerId, GamePadButton button, float interval, float pressDuration) {
        this.gamePadId = gamePadId;
        this.playerId = playerId;
        this.button = button;
        this.interval = interval;
        this.pressDuration = pressDuration;
    }
}