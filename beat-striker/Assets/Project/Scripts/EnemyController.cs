using UnityEngine;

public class EnemyController : MonoBehaviour
{
    private GameObject player;

    void Start()
    {
        // プレイヤーオブジェクトを探す
        player = GameObject.Find("Player");
    }

    void Update()
    {
        transform.position = Vector3.MoveTowards(
            transform.position,
            player.transform.position,
            Time.deltaTime * 2.0f
        );
    }
}
