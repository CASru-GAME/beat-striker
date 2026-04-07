using System.Collections.Generic;
using UnityEngine;

namespace Core {
    /// <summary>
    /// このオブジェクトをシーン切り替え時に破棄されないようにするコンポーネント
    /// </summary>
    [AddComponentMenu(" 🟠Persistent Object")]
    public class PersistentObject : MonoBehaviour {
        private static Dictionary<string, bool> existingObjects = new();
        [SerializeField] bool EnableSingleton = false;
        [SerializeField] string SingletonId;

        private void Awake() {
            DontDestroyOnLoad(this.gameObject);
            if(!EnableSingleton) return;

            if (existingObjects.ContainsKey(SingletonId)) {
                Destroy(this.gameObject);
                return;
            }
            existingObjects[SingletonId] = true;
        }
        
        private void OnDestroy() {
            if (existingObjects.ContainsKey(SingletonId)) {
                existingObjects.Remove(SingletonId);
            }
        }
    }

}