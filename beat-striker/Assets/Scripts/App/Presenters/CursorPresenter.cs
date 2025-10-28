

using Core.App.Presenters.Scene.Types;
using Core.App.Types;
using Core.GamePad.Types;
using Core.Utils;

public class CursorPresenter: ICursorPresenter {
    private readonly ICursorView view;
    readonly PlayerId playerId;
    readonly IPlayerRegistry registry;
    readonly IBus bus;

    public CursorPresenter(ICursorView view, PlayerId playerId, IPlayerRegistry registry, IBus bus, ILife life) {
        this.view = view;
        this.playerId = playerId;
        this.registry = registry;
        this.bus = bus;
        life.Link(OnEnable, OnDisable);
    }

    private void OnDisable() {
        bus.Unsubscribe<GamePadMessages.Inputed>(OnGamePadInputed);
        bus.Unsubscribe<GamePadMessages.DirectionChanged>(OnDirectionChanged);
        bus.Unsubscribe<AppMessages.RequireCursorDestroyed>(OnCursorSceneExited);
    }

    private void OnEnable() {
        bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
        bus.Subscribe<GamePadMessages.DirectionChanged>(OnDirectionChanged);
        bus.Subscribe<AppMessages.RequireCursorDestroyed>(OnCursorSceneExited);
    }

     void OnDirectionChanged(GamePadMessages.DirectionChanged mes) {
        var pId = registry.ToPlayerId(mes.gamePadId);
        if (pId == null || !pId.Value.Equals(playerId)) return;
        view.OnMove(mes.direction);
    }

     void OnGamePadInputed(GamePadMessages.Inputed mes) {
        var pId = registry.ToPlayerId(mes.gamePadId);
        if (pId == null || !pId.Value.Equals(playerId)) return;

        if (mes.button == GamePadButton.Direction && mes.action == GamePadAction.Up) {
            view.OnMoveEnd();
            return;
        }

        if (mes.button == GamePadButton.East) {
            view.OnClick();
        }
    }

    private void OnCursorSceneExited(AppMessages.RequireCursorDestroyed message) {
        if (!message.IsTarget(playerId)) return;
        view.Destroy();
    }
    
}