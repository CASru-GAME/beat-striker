using Core.Battle;
using TMPro;
using UnityEngine;

public class ComboUI : MonoBehaviour {
    IStrikerModelGetter strikerModel;
    [SerializeField] TextMeshProUGUI Combo;

    public void Construct(IStrikerModelGetter strikerModel) {
        this.strikerModel = strikerModel;
    }

    void Update() {
        Combo.text = strikerModel.ComboCount.ToString();
    }
}
