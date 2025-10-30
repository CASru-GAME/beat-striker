using Core.Battle;
using UnityEngine;

public class HpBarUI : MonoBehaviour {
    IStrikerModelGetter strikerModel;
    [SerializeField] Transform HpBar;

    public void Construct(IStrikerModelGetter strikerModel) {
        this.strikerModel = strikerModel;
    }

    void Update() {
        HpBar.localScale = new Vector3(
            strikerModel.HitPoint.value / strikerModel.MaxHitPoint.value,
            HpBar.localScale.y,
            HpBar.localScale.z
        );
    }
}
