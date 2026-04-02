using UnityEngine;

namespace Alice {
    public class AliceSpecialBarView : MonoBehaviour {
        [SerializeField] Transform specialBar;

        public void SetSpecialRatio(float ratio) {
            var clampedRatio = Mathf.Clamp01(ratio);
            specialBar.localScale = new Vector3(
                clampedRatio,
                specialBar.localScale.y,
                specialBar.localScale.z
            );
        }
    }
}
