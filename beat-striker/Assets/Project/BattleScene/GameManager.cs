using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] Transform spawnPosition1, spawnPosition2;
    [NonSerialized] public Striker player1, player2;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        player1 = Instantiate(Common.Instance.player0.strikerPrefab, spawnPosition1.position, Quaternion.Euler(0, 90, 0));
        player2 = Instantiate(Common.Instance.player1.strikerPrefab, spawnPosition2.position, Quaternion.Euler(0, -90, 0));
    }

    // Update is called once per frame
    void Update()
    {
        if (player1.hp <= 0)
        {
            SceneManager.LoadScene("ResultScene");
        }
    }
}
