using System.Collections.Generic;
using UnityEngine;

namespace Alice {
    public interface IBattleSetting {
        List<Transform> PlayerTransforms { get; }
        AudioClip SpecialUnavailableSound { get; }
        float SpecialUnavailableSoundVolume { get; }
        bool IsTestMode { get; }
        Bounds MovementBounds { get; }
    }

    public class BattleSetting : MonoBehaviour, IBattleSetting {
        [SerializeField] List<Transform> playerTransforms;
        [SerializeField] AudioClip specialUnavailableSound;
        [SerializeField] float specialUnavailableSoundVolume = 1f;
        [SerializeField] bool isTestMode;

        [Header("Stage movement boundary")]
        [SerializeField] BoxCollider floor;
        [SerializeField] BoxCollider leftWall;
        [SerializeField] BoxCollider rightWall;
        [SerializeField] BoxCollider ceiling;

        public List<Transform> PlayerTransforms => playerTransforms;
        public AudioClip SpecialUnavailableSound => specialUnavailableSound;
        public float SpecialUnavailableSoundVolume => specialUnavailableSoundVolume;
        public bool IsTestMode => isTestMode;

        public Bounds MovementBounds {
            get {
                var floorBounds = floor.bounds;
                var bounds = new Bounds();
                bounds.SetMinMax(
                    new Vector3(Mathf.Max(floorBounds.min.x, leftWall.bounds.max.x), floorBounds.max.y, floorBounds.min.z),
                    new Vector3(Mathf.Min(floorBounds.max.x, rightWall.bounds.min.x), ceiling.bounds.min.y, floorBounds.max.z));
                return bounds;
            }
        }
    }
}
