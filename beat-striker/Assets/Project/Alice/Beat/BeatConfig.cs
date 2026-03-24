using System.Collections.Generic;
using UnityEngine;

namespace Alice {

    [System.Serializable]
    public class Track {
        public string Name;
        public AudioClip AudioClip;
        public float bpm = 110;
        public float offset = 0;
        public float[] beats;
        public AudioClip beatSound;
    }

    public class BeatConfig : MonoBehaviour {
        [SerializeField] string selectedTrackName;
        [SerializeField] List<Track> tracks;
        [SerializeField] float commandTimeOffset = 0;
        [SerializeField] float viewTimeOffset = 0.2f;
        [SerializeField] float perfectWindow = 0.1f;

        public Track SelectedTrack {
            get {
                var track = tracks.Find(t => t.Name == selectedTrackName);
                if (track != null) {
                    track.beats = CalculateBeats(track.AudioClip.length, track.bpm, track.offset);
                }
                return track;
            }
        }
        public float CommandTimeOffset => commandTimeOffset;
        public float ViewTimeOffset => viewTimeOffset;
        public float PerfectWindow => perfectWindow;

        float[] CalculateBeats(float trackLength, float bpm, float offset) {
            var beatInterval = 60f / bpm;
            var beatCount = Mathf.FloorToInt(trackLength / beatInterval);
            var beats = new float[beatCount];
            for (int i = 0; i < beatCount; i++) {
                beats[i] = i * beatInterval + offset;
            }
            return beats;
        }
    }
}
