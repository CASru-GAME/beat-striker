


using UnityEngine;

public interface ICursorView {
    void OnMoveEnd();
    void OnMove(Vector2 direction);
    void OnClick();
    void Destroy();
}