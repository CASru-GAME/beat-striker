using TMPro;
using UnityEngine;

public class BattleText : MonoBehaviour
{
    public TextMeshProUGUI fight, gameSet;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start() {
        Battle.Instance.playingState.OnEnter += () => {
            fight.gameObject.SetActive(true);
            fight.Delay(() => fight.gameObject.SetActive(false), 1f);
        };
        
        Battle.Instance.playingState.OnExit += () => {
            gameSet.gameObject.SetActive(true);
            gameSet.Delay(() => gameSet.gameObject.SetActive(false), 1f);
        };
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
