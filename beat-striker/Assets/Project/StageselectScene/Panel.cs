using Unity.Cinemachine;
using UnityEngine;

public class Panel : MonoBehaviour
{
    public Vector3 defaultPosition;
    public Vector3 rightPosition = new Vector3(100, 0, 0); // 右に動かす座標
    public Vector3 leftPosition = new Vector3(-100, 0, 0); // 左に動かす座標
    public float moveSpeed = 10f; // 動く速度
    private Vector3 targetPosition;
    void Start() {
        defaultPosition = transform.localPosition;
        targetPosition = defaultPosition;
    }
    public void MoveRight()
    {
        targetPosition = rightPosition;
    }

    public void MoveLeft()
    {
        targetPosition = leftPosition;
    }

    public void ResetPosition()
    {
        targetPosition = defaultPosition;
    }
    void Update()
    {
        transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, Time.deltaTime * moveSpeed);
    }
}
