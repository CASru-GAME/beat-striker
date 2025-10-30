using Core.Battle;
using UnityEngine;

public class SpecialBarUI : MonoBehaviour {
    IStrikerModelGetter strikerModel;
    [SerializeField] Transform SpecialBar;

    public void Construct(IStrikerModelGetter strikerModel) {
        this.strikerModel = strikerModel;
    }

    void Update() {
        SpecialBar.localScale = new Vector3(
            strikerModel.SpecialPoint.value / strikerModel.MaxSpecialPoint.value,
            SpecialBar.localScale.y,
            SpecialBar.localScale.z
        );
    }
}
