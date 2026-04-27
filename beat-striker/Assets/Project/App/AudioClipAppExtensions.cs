using UnityEngine;

public static class AudioClipAppExtensions {
    public static void PlayAtApp(this AudioClip clip) {
        var appScope = Alice.AppScope.Instance;
        if (!appScope) {
            return;
        }

        appScope.AppAudioPlayer?.Play(clip);
    }

    public static void PlayAtApp(this AudioClip clip, Vector3 worldPosition) {
        var appScope = Alice.AppScope.Instance;
        if (!appScope) {
            return;
        }

        appScope.AppAudioPlayer?.Play(clip, worldPosition);
    }
}
