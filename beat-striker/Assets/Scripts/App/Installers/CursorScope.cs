using Core.App;
using Core.App.Interfaces;
using Core.GamePad;
using Core.GamePad.Models;
using Core.Utils;
using UnityEngine;

[RequireComponent(typeof(CursorView))]
[RequireComponent(typeof(Life))]
public class CursorScope : MonoBehaviour {
    private IAppModel appModel;
    private IGamePadInputModel gamePadInputModel;

    public void Construct(Core.App.Types.PlayerId id, IPlayerRegistry playerRegistry, IAppModel appModel, IGamePadInputModel gamePadInputModel) {
        Debug.Log("CursorScope Construct:" + id);
        this.appModel = appModel;
        this.gamePadInputModel = gamePadInputModel;
        var view = GetComponent<CursorView>();
        // Pass dependencies directly to View
        view.Construct(id, appModel, gamePadInputModel);
    }
}
