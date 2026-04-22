using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public interface IBattleSetting {
        List<Transform> PlayerTransforms { get; }
        AudioClip SpecialUnavailableSound { get; }
        float SpecialUnavailableSoundVolume { get; }
        bool IsTestMode { get; }
    }

    public class BattleSetting : MonoBehaviour, IBattleSetting {
        [SerializeField] List<Transform> playerTransforms;
        [SerializeField] AudioClip specialUnavailableSound;
        [SerializeField] float specialUnavailableSoundVolume = 1f;
        [SerializeField] bool isTestMode;

        public List<Transform> PlayerTransforms => playerTransforms;
        public AudioClip SpecialUnavailableSound => specialUnavailableSound;
        public float SpecialUnavailableSoundVolume => specialUnavailableSoundVolume;
        public bool IsTestMode => isTestMode;
    }
}
