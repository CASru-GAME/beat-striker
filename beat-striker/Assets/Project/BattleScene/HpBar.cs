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
    void Update()
    {
        text.text = "HP: " + (int) GameManager.Instance.strikers[playerNumber].hp;
    }
}
