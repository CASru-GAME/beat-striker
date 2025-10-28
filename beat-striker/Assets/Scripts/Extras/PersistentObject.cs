using UnityEngine;

namespace Core {
    /// <summary>
    /// このオブジェクトをシーン切り替え時に破棄されないようにするコンポーネント
    /// </summary>
    public class PersistentObject : MonoBehaviour {
        private void Awake() {
            DontDestroyOnLoad(this.gameObject);
        }
    }
}