using Core.Battle;
using UnityEngine;

public class HpBarUI : MonoBehaviour {
    IStrikerModelGetter strikerModel;
    [SerializeField] Transform HpBar; // 緑のHPバー（現在のHP）
    [SerializeField] Transform DamageBar; // 赤のダメージバー（遅延して減る）
    [SerializeField] float damageBarDelay = 0.3f; // 赤いバーが消えるまでの遅延時間（秒）
    [SerializeField] float damageBarDuration = 0.3f; // 赤いバーが消えるアニメーションの長さ（秒）
    private float previousHpRatio = 1f;

    public void Construct(IStrikerModelGetter strikerModel) {
        this.strikerModel = strikerModel;
    }

    void Start() {
        // 初期状態でダメージバーも同じスケールに設定
        if (DamageBar != null) {
            DamageBar.localScale = new Vector3(1f, DamageBar.localScale.y, DamageBar.localScale.z);
        }
    }

    void Update() {
        float currentHpRatio = strikerModel.HitPoint.value / strikerModel.MaxHitPoint.value;
        
        // HPの比率が変わった時だけアニメーション
        if (Mathf.Abs(currentHpRatio - previousHpRatio) > 0.001f) {
            // 緑のバーは即座に減る（アニメーションなし）
            HpBar.localScale = new Vector3(currentHpRatio, HpBar.localScale.y, HpBar.localScale.z);
            
            // 赤いバーはダメージ前のサイズを維持してから遅延して減る
            if (DamageBar != null) {
                // ダメージバーを現在の（まだ減っていない）サイズに設定
                DamageBar.localScale = new Vector3(previousHpRatio, DamageBar.localScale.y, DamageBar.localScale.z);
                
                LeanTween.cancel(DamageBar.gameObject);
                LeanTween.scaleX(DamageBar.gameObject, currentHpRatio, damageBarDuration)
                    .setDelay(damageBarDelay)
                    .setEase(LeanTweenType.easeOutQuad);
            }
            
            previousHpRatio = currentHpRatio;
        }
    }
}
