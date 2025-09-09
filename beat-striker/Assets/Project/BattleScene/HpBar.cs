using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class HpBar : MonoBehaviour
{
    TextMeshProUGUI text;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        text.text = "HP: " + (int) GameManager.Instance.player1.hp;
    }
}
