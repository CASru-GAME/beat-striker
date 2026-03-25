
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Alice {

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
            UnityEngine.Object.Destroy(tempGO, clip.length);

            return aSource; // これを保持しておけば、あとから Stop() できる
        }

        public static TSource MinBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> selector) where TSource : class {
            if (source == null) throw new ArgumentNullException(nameof(source));

            var comparer = Comparer<TKey>.Default;

            using (var iterator = source.GetEnumerator()) {
                if (!iterator.MoveNext()) {
                    return null;
                }

                var minElement = iterator.Current;
                var minKey = selector(minElement);

                while (iterator.MoveNext()) {
                    var currentElement = iterator.Current;
                    var currentKey = selector(currentElement);

                    if (comparer.Compare(currentKey, minKey) < 0) {
                        minElement = currentElement;
                        minKey = currentKey;
                    }
                }

                return minElement;
            }
        }
    }
}