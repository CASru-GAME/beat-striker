using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public interface IBattleSetting {
        List<Transform> PlayerTransforms { get; }
    }

    public class BattleSetting : MonoBehaviour, IBattleSetting {
        [SerializeField] List<Transform> playerTransforms;

        public List<Transform> PlayerTransforms => playerTransforms;
    }
}
