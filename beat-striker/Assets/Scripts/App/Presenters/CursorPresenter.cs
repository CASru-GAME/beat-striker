

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
    }

    private void OnEnable() {
        bus.Subscribe<GamePadMessages.Inputed>(OnGamePadInputed);
        bus.Subscribe<GamePadMessages.DirectionChanged>(OnDirectionChanged);
    }

    public void OnDirectionChanged(GamePadMessages.DirectionChanged mes) {
        if (!registry.ToPlayerId(mes.gamePadId).Equals(playerId)) return;
        view.OnMove(mes.direction);
        if (!registry.ToPlayerId(mes.gamePadId).Equals(playerId)) return;
    }

    public void OnGamePadInputed(   GamePadMessages.Inputed mes) {
        if (!registry.ToPlayerId(mes.gamePadId).Equals(playerId)) return;

        if (mes.button == GamePadButton.Direction && mes.action == GamePadAction.Up) {
            view.OnMoveEnd();
            return;
        }

        if (mes.button != GamePadButton.East) {
            view.OnClick();
        }
    }
}