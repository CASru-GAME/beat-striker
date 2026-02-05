
using UnityEngine;

namespace Core.Striker.Components {

    public static class Extentions {

        public static AudioSource PlayAtPoint(this AudioClip clip, Transform transform, float volume = 1.0f) {
            return clip.PlayAtPoint(transform.position, volume, transform);
        }

        public static AudioSource PlayAtPoint(this AudioClip clip, Vector3 pos, float volume = 1.0f, Transform parent = null) {
            GameObject tempGO = new("TempAudio");
            tempGO.transform.position = pos;
            tempGO.transform.parent = parent;
            AudioSource aSource = tempGO.AddComponent<AudioSource>();

            aSource.clip = clip;
            aSource.volume = volume;
            aSource.Play();

            // 再生終了後に自動破棄する設定
            Object.Destroy(tempGO, clip.length);

            return aSource; // これを保持しておけば、あとから Stop() できる
        }
    }
}