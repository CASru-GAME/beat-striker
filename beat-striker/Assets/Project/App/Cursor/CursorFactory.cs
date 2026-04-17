using UnityEngine;

namespace Alice {
    public interface ICursorFactory {
        ICursor Create(int playerId);
    }

    public class CursorFactory : MonoBehaviour, ICursorFactory {
        [SerializeField] Cursor cursorPrefab;
        [SerializeField] RectTransform cursorParent;

        public ICursor Create(int playerId) {
            var instance = Object.Instantiate(cursorPrefab, cursorParent);
            instance.Construct(playerId);
            return instance;
        }
    }
}