using Core.Battle;
using TMPro;
using UnityEngine;

public class ComboUI : MonoBehaviour {
    IStrikerModelGetter strikerModel;
    [SerializeField] TextMeshProUGUI Combo; // 子のComboテキスト
    private int previousComboCount = 0;

    public void Construct(IStrikerModelGetter strikerModel) {
        this.strikerModel = strikerModel;
    }

    void Update() {
        if (strikerModel == null) {
            return;
        }
        
        int comboCount = strikerModel.ComboCount;
        
        // コンボ数が1以上の時は表示、0の時は非表示
        if (comboCount > 0) {
            if (!Combo.gameObject.activeSelf) {
                Combo.gameObject.SetActive(true);
            }
            
            // 数字が増えたときにアニメーション
            if (comboCount > previousComboCount) {
                PlayComboAnimation();
            }
            
            Combo.text = comboCount.ToString();
        } else {
            if (Combo.gameObject.activeSelf) {
                Combo.gameObject.SetActive(false);
            }
        }
        
        previousComboCount = comboCount;
    }
    
    void PlayComboAnimation() {
        // 既存のアニメーションをキャンセル
        LeanTween.cancel(Combo.gameObject);
        
        // 拡大
        LeanTween.scale(Combo.gameObject, Vector3.one * 1.15f, 0.1f)
            .setEase(LeanTweenType.easeOutQuad)
            .setOnComplete(() => {
                // 縮小して元に戻る
                LeanTween.scale(Combo.gameObject, Vector3.one, 0.1f)
                    .setEase(LeanTweenType.easeInQuad);
            });
    }
}
