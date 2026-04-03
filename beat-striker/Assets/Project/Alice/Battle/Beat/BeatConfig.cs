using System.Collections.Generic;
using UnityEngine;

namespace Alice {

    [System.Serializable]
    public class Track {
        public string Name;
        public AudioClip AudioClip;
        public float bpm = 110;
        [Tooltip("ビートの時間オフセット（秒）。全ビート時刻に加算されます。値を大きくするとビートが後方（遅れ）に移動し、負にすると前方（早く）に移動します。曲の先頭の空白や音声ファイルのタイミング補正に使用します。")]
        public float offset = 0;
        public float[] beats;
        public AudioClip beatSound;
    }

    public class BeatConfig : MonoBehaviour {
        [SerializeField] string selectedTrackName;
        [SerializeField] List<Track> tracks;
        [Tooltip("入力判定の時間補正（秒）。評価時に「評価用入力時刻 = 入力時刻 + この値」で比較します。正の値は入力を遅めに、負の値は早めに評価します（過去の入力を書き換えるわけではありません）。例: 入力が約0.05秒遅れて登録される環境では -0.05 を設定して補正できます。")]
        [SerializeField] float commandTimeOffset = 0;
        [Tooltip("ノーツ描画の視覚補正（秒）。正の値を大きくするとノーツがヒット時刻より先に表示され（先行表示）、負にすると遅れて表示されます。画面遅延やノーツ移動時間の調整に使用します。")]
        [SerializeField] float viewTimeOffset = 0f;
        [Tooltip("ビート時刻を全体的に遅らせるオフセット（秒）。CalculateBeatsで生成される全ビートに加算されます。Track.offset はトラック個別の補正、beatTimeOffset はグローバル補正です。正の値でビートが遅れ、負の値で早まります。")]
        [SerializeField] float beatTimeOffset = 0;
        [SerializeField] float perfectWindow = 0.1f;

        public Track SelectedTrack {
            get {
                var track = tracks.Find(t => t.Name == selectedTrackName);
                if (track != null) {
                    track.beats = CalculateBeats(track.AudioClip.length, track.bpm, track.offset + beatTimeOffset);
                }
                return track;
            }
        }
        public float CommandTimeOffset => commandTimeOffset;
        public float ViewTimeOffset => viewTimeOffset;
        public float BeatTimeOffset => beatTimeOffset;
        public float PerfectWindow => perfectWindow;

        public void ApplyTrackSelection(string trackId) {
            selectedTrackName = trackId;
        }

        public void ApplyBeatOffsets(BeatOffsetSetting beatOffsetSetting) {
            commandTimeOffset = beatOffsetSetting.CommandTimeOffset;
            viewTimeOffset = beatOffsetSetting.ViewTimeOffset;
            beatTimeOffset = beatOffsetSetting.BeatTimeOffset;
        }

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
