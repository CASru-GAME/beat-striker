using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    [SerializeField] private CharacterController playerPrefab;
    [SerializeField] private Transform spawnPosition1;
    public CharacterController player1;

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
        player1 = Instantiate(playerPrefab, spawnPosition1.position, Quaternion.Euler(0, 90, 0));
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
