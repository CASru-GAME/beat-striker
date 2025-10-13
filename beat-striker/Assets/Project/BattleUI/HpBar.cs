using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class HpBar : MonoBehaviour
{
    TextMeshProUGUI text;
    [SerializeField] int playerNumber;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
        void Update() {
        var striker = Battle.Instance.strikers.Get(playerNumber);
        text.text = "HP: " + (int)striker.Striker.hp;
    }
}
