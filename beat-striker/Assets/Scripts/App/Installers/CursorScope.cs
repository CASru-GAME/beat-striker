using Core.Utils;
using UnityEngine;
using VContainer;
using VContainer.Unity;

[RequireComponent(typeof(CursorView))]
[RequireComponent(typeof(Life))]
public class CursorScope : MonoBehaviour {

    public void Construct(Core.App.Types.PlayerId id, IPlayerRegistry playerRegistry) {
        Debug.Log("CursorScope Construct:" + id);
        var view = GetComponent<CursorView>();
        var life = GetComponent<Life>();
        var presenter = new CursorPresenter(view, id, playerRegistry, this.GetBus(), life);
        view.Construct(id, presenter);
    }
}
