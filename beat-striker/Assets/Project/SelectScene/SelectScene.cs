using UnityEngine;
using UnityEngine.SceneManagement;

public class SelectScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    public void SelectCharacter(Striker prefab)
    {
        Common.Instance.players.ForEach(p => p.strikerPrefab = prefab);
    }

    public void GotoBattleScene()
    {
        SceneManager.LoadScene("BattleScene");
    }
}
