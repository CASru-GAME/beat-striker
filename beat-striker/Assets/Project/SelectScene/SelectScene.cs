using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Botan))]
public class SelectScene : MonoBehaviour {
    Botan botan;
    public Striker prefab;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        botan = GetComponent<Botan>();
        botan.onClick += SelectCharacter;
    }

    // Update is called once per frame
    void Update() {

    }

    public void SelectCharacter(HumanPlayer player) {
        if (player == null) return;
        player.strikerPrefab = prefab;
        Debug.Log("Player " + (player.playerNumber + 1) + " selected " + prefab.name);
    }

    public void GotoBattleScene() {
        SceneManager.LoadScene("BattleScene");
    }

    public void Test() {
        Debug.Log("Test");
    }
}
