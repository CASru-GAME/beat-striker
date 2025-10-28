using System.Collections.Generic;
using UnityEngine;

public class SingletonObject : MonoBehaviour {
        private static Dictionary<string, bool> existingObjects = new();
        [SerializeField] string SingletonId;

        private void Awake() {
            if (existingObjects.ContainsKey(SingletonId)) {
                Destroy(this.gameObject);
                return;
            }
            existingObjects[SingletonId] = true;
            DontDestroyOnLoad(this.gameObject);
        }
        
        private void OnDestroy() {
            if (existingObjects.ContainsKey(SingletonId)) {
                existingObjects.Remove(SingletonId);
            }
        }
    }