using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Alice.Editor {
    public class BeatManualEditorWindow : EditorWindow {
        const float CountdownSeconds = 3f;
        const string TimestampFormat = "yyyy-MM-dd-hh-mm-ss";
        const string TimeFormat = "F6";
        static readonly Type AudioUtilType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");

        AudioClip selectedClip;
        AudioClip beatSeClip;
        string musicFileName = string.Empty;
        float recordOffsetSec;
        float bucketWindowSec = 0.08f;
        float timelineZoomSeconds = 8f;
        bool followPlayhead = true;

        readonly List<float> capturedRawTimes = new List<float>();
        readonly List<float> capturedOffsetTimes = new List<float>();

        bool isCountingDown;
        bool isRecording;
        bool isReplaying;
        double countdownStartedAt;
        double playbackStartedAt;
        int replayBeatCursor;
        List<float> replayBeatTimes = new List<float>();

        string statusMessage = "Idle";
        Vector2 scroll;

        [MenuItem("Tools/Beat Manual Editor")]
        static void Open() {
            var window = GetWindow<BeatManualEditorWindow>("Beat Manual Editor");
            window.minSize = new Vector2(680f, 420f);
        }

        void OnEnable() {
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable() {
            EditorApplication.update -= OnEditorUpdate;
            StopPreviewAudio();
        }

        void OnGUI() {
            HandleKeyInput(Event.current);

            EditorGUILayout.LabelField("Beat Recording", EditorStyles.boldLabel);
            selectedClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", selectedClip, typeof(AudioClip), false);
            beatSeClip = (AudioClip)EditorGUILayout.ObjectField("Beat SE", beatSeClip, typeof(AudioClip), false);

            using (new EditorGUI.DisabledScope(selectedClip == null)) {
                if (GUILayout.Button("Use Clip Name", GUILayout.Width(140f)) && selectedClip != null) {
                    musicFileName = selectedClip.name;
                }
            }

            musicFileName = EditorGUILayout.TextField("Music File Name", musicFileName);
            recordOffsetSec = EditorGUILayout.FloatField("Record Offset Sec", recordOffsetSec);
            bucketWindowSec = EditorGUILayout.FloatField("Bucket Window Sec", Mathf.Max(0.0001f, bucketWindowSec));
            timelineZoomSeconds = EditorGUILayout.Slider("Timeline Zoom Sec", timelineZoomSeconds, 1f, 30f);
            followPlayhead = EditorGUILayout.Toggle("Follow Playhead", followPlayhead);

            EditorGUILayout.Space(8f);
            DrawActionButtons();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
            DrawTimeline();
            DrawCapturedTimes();
            Repaint();
        }

        void DrawActionButtons() {
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(selectedClip == null || string.IsNullOrWhiteSpace(musicFileName) || isRecording || isCountingDown || isReplaying)) {
                    if (GUILayout.Button("Record", GUILayout.Height(32f))) {
                        StartCountdown();
                    }
                }

                using (new EditorGUI.DisabledScope(!isRecording && !isCountingDown && !isReplaying)) {
                    if (GUILayout.Button("Stop", GUILayout.Height(32f))) {
                        StopCurrentAction();
                    }
                }

                using (new EditorGUI.DisabledScope(selectedClip == null || string.IsNullOrWhiteSpace(musicFileName))) {
                    if (GUILayout.Button("Export", GUILayout.Height(32f))) {
                        ExportBeats();
                    }
                }

                using (new EditorGUI.DisabledScope(selectedClip == null || capturedOffsetTimes.Count == 0 || isRecording || isCountingDown || isReplaying)) {
                    if (GUILayout.Button("Replay", GUILayout.Height(32f))) {
                        StartReplay(capturedOffsetTimes);
                    }
                }
            }
        }

        void DrawTimeline() {
            var clipLength = selectedClip != null ? selectedClip.length : 0f;
            var timelineRect = GUILayoutUtility.GetRect(1f, 86f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(timelineRect, new Color(0.13f, 0.13f, 0.13f));

            if (clipLength <= 0f) {
                EditorGUI.LabelField(timelineRect, "Select Audio Clip to show timeline.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            var playheadTime = GetCurrentPlaybackTime();
            var viewStart = 0f;
            var viewEnd = clipLength;
            if (timelineZoomSeconds < clipLength) {
                if (followPlayhead) {
                    viewStart = Mathf.Clamp(playheadTime - timelineZoomSeconds * 0.5f, 0f, clipLength - timelineZoomSeconds);
                } else {
                    viewStart = 0f;
                }
                viewEnd = Mathf.Min(clipLength, viewStart + timelineZoomSeconds);
            }

            var viewDuration = Mathf.Max(0.0001f, viewEnd - viewStart);
            var normalizedPlayhead = Mathf.Clamp01((playheadTime - viewStart) / viewDuration);
            DrawLine(timelineRect.xMin + timelineRect.width * normalizedPlayhead, timelineRect.yMin, timelineRect.yMax, Color.cyan);

            foreach (var rawTime in capturedRawTimes) {
                if (rawTime < viewStart || rawTime > viewEnd) {
                    continue;
                }
                var x = timelineRect.xMin + timelineRect.width * Mathf.Clamp01((rawTime - viewStart) / viewDuration);
                DrawDot(x, timelineRect.center.y - 12f, new Color(0.99f, 0.85f, 0.2f));
            }

            foreach (var offsetTime in capturedOffsetTimes) {
                if (offsetTime < viewStart || offsetTime > viewEnd) {
                    continue;
                }
                var x = timelineRect.xMin + timelineRect.width * Mathf.Clamp01((offsetTime - viewStart) / viewDuration);
                DrawDot(x, timelineRect.center.y + 12f, new Color(0.2f, 0.95f, 0.4f));
            }

            var labelRect = new Rect(timelineRect.x + 8f, timelineRect.y + 6f, timelineRect.width - 16f, 20f);
            EditorGUI.LabelField(labelRect, $"Playhead: {playheadTime:F3}s / {clipLength:F3}s  View: {viewStart:F2}-{viewEnd:F2}s");

            var legendRect = new Rect(timelineRect.x + 8f, timelineRect.yMax - 22f, timelineRect.width - 16f, 16f);
            EditorGUI.LabelField(legendRect, "Yellow = Raw Input, Green = Saved (Offset Applied)", EditorStyles.miniLabel);
        }

        void DrawCapturedTimes() {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"Captured: {capturedRawTimes.Count}", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(120f));
            for (var i = 0; i < capturedRawTimes.Count; i++) {
                EditorGUILayout.LabelField($"{i + 1:D3}: raw={capturedRawTimes[i].ToString(TimeFormat, CultureInfo.InvariantCulture)}  saved={capturedOffsetTimes[i].ToString(TimeFormat, CultureInfo.InvariantCulture)}");
            }
            EditorGUILayout.EndScrollView();
        }

        void HandleKeyInput(Event currentEvent) {
            if (!isRecording || selectedClip == null || currentEvent.type != EventType.KeyDown || currentEvent.keyCode != KeyCode.Space) {
                return;
            }

            var rawTime = Mathf.Clamp(GetCurrentPlaybackTime(), 0f, selectedClip.length);
            var offsetTime = Mathf.Max(0f, rawTime - recordOffsetSec);
            capturedRawTimes.Add(rawTime);
            capturedOffsetTimes.Add(offsetTime);
            PlayBeatSe();
            statusMessage = $"Captured at raw={rawTime:F3}s saved={offsetTime:F3}s";
            currentEvent.Use();
        }

        void OnEditorUpdate() {
            if (isCountingDown) {
                var remain = CountdownSeconds - (float)(EditorApplication.timeSinceStartup - countdownStartedAt);
                statusMessage = $"Recording starts in {Mathf.Max(0f, remain):F2}s";
                if (remain <= 0f) {
                    BeginPlayback();
                }
            }

            if (isRecording) {
                if (selectedClip == null || GetCurrentPlaybackTime() >= selectedClip.length) {
                    StopAndSaveManual();
                }
            }

            if (isReplaying) {
                if (selectedClip == null) {
                    StopReplay();
                    return;
                }

                var playbackTime = GetCurrentPlaybackTime();
                while (replayBeatCursor < replayBeatTimes.Count && replayBeatTimes[replayBeatCursor] <= playbackTime) {
                    PlayBeatSe();
                    replayBeatCursor++;
                }

                if (playbackTime >= selectedClip.length) {
                    StopReplay();
                }
            }
        }

        void StartCountdown() {
            capturedRawTimes.Clear();
            capturedOffsetTimes.Clear();
            isCountingDown = true;
            isRecording = false;
            countdownStartedAt = EditorApplication.timeSinceStartup;
            statusMessage = "Countdown started.";
        }

        void BeginPlayback() {
            isCountingDown = false;
            isRecording = true;
            isReplaying = false;
            playbackStartedAt = EditorApplication.timeSinceStartup;
            PlayPreviewAudio(selectedClip);
            statusMessage = "Recording... Press Space on beat.";
        }

        void StartReplay(IEnumerable<float> beatTimes) {
            replayBeatTimes = beatTimes
                .Where(t => t >= 0f)
                .OrderBy(t => t)
                .ToList();
            if (replayBeatTimes.Count == 0) {
                statusMessage = "Replay has no beat times.";
                return;
            }

            isCountingDown = false;
            isRecording = false;
            isReplaying = true;
            replayBeatCursor = 0;
            playbackStartedAt = EditorApplication.timeSinceStartup;
            PlayPreviewAudio(selectedClip);
            statusMessage = "Replaying... Beat SE will play on beat timings.";
        }

        void StopReplay() {
            isReplaying = false;
            replayBeatCursor = 0;
            playbackStartedAt = 0d;
            StopPreviewAudio();
            statusMessage = "Replay finished.";
        }

        void StopCurrentAction() {
            if (isReplaying) {
                StopReplay();
                return;
            }

            StopAndSaveManual();
        }

        void StopAndSaveManual() {
            isCountingDown = false;
            isReplaying = false;
            if (!isRecording && capturedOffsetTimes.Count == 0) {
                statusMessage = "Stopped.";
                StopPreviewAudio();
                return;
            }

            isRecording = false;
            playbackStartedAt = 0d;
            StopPreviewAudio();

            if (capturedOffsetTimes.Count == 0) {
                statusMessage = "No hits captured. Nothing saved.";
                return;
            }

            var outputDirectory = EnsureOutputDirectory();
            var timestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            var outputPath = Path.Combine(outputDirectory, $"manual-{timestamp}.beats.txt");
            SaveBeatTimes(outputPath, capturedOffsetTimes);
            statusMessage = $"Saved manual beats: {outputPath}";
            EditorUtility.RevealInFinder(outputPath);
        }

        void ExportBeats() {
            var outputDirectory = EnsureOutputDirectory();
            var manualFiles = Directory.GetFiles(outputDirectory, "manual-*.beats.txt", SearchOption.TopDirectoryOnly);
            if (manualFiles.Length == 0) {
                statusMessage = "No manual files found for export.";
                return;
            }

            var allTimes = new List<float>();
            foreach (var manualFile in manualFiles) {
                allTimes.AddRange(ReadBeatTimes(manualFile));
            }

            if (allTimes.Count == 0) {
                statusMessage = "Manual files exist but contain no beat times.";
                return;
            }

            var averaged = BuildBucketAverages(allTimes, bucketWindowSec);
            var timestamp = DateTime.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture);
            var outputPath = Path.Combine(outputDirectory, $"beats-{timestamp}.beats.txt");
            SaveBeatTimes(outputPath, averaged);
            statusMessage = $"Exported beats: {outputPath} (manual files: {manualFiles.Length})";
            EditorUtility.RevealInFinder(outputPath);
        }

        float GetCurrentPlaybackTime() {
            if ((!isRecording && !isReplaying) || selectedClip == null) {
                return 0f;
            }

            return (float)(EditorApplication.timeSinceStartup - playbackStartedAt);
        }

        void PlayBeatSe() {
            if (beatSeClip != null) {
                PlayOneShotPreviewAudio(beatSeClip);
            }
        }

        string EnsureOutputDirectory() {
            var sanitized = SanitizePathSegment(musicFileName.Trim());
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var outputDirectory = Path.Combine(projectRoot, "Dist", "Beats", sanitized);
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        static string SanitizePathSegment(string value) {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        static IEnumerable<float> ReadBeatTimes(string path) {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines) {
                var value = line.Trim();
                if (string.IsNullOrEmpty(value) || value.StartsWith("#", StringComparison.Ordinal)) {
                    continue;
                }

                if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var beatTime)) {
                    yield return beatTime;
                }
            }
        }

        static void SaveBeatTimes(string path, IEnumerable<float> values) {
            var ordered = values
                .Where(v => v >= 0f)
                .OrderBy(v => v)
                .Select(v => v.ToString(TimeFormat, CultureInfo.InvariantCulture));
            File.WriteAllLines(path, ordered);
            AssetDatabase.Refresh();
        }

        static List<float> BuildBucketAverages(List<float> values, float window) {
            var ordered = values.Where(v => v >= 0f).OrderBy(v => v).ToList();
            var clusters = new List<BeatCluster>();
            foreach (var value in ordered) {
                if (clusters.Count == 0) {
                    clusters.Add(new BeatCluster(value));
                    continue;
                }

                var tail = clusters[clusters.Count - 1];
                if (Mathf.Abs(value - tail.Mean) <= window) {
                    tail.Add(value);
                    continue;
                }

                clusters.Add(new BeatCluster(value));
            }

            return clusters.Select(c => c.Mean).OrderBy(v => v).ToList();
        }

        static void DrawLine(float x, float yMin, float yMax, Color color) {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawLine(new Vector3(x, yMin), new Vector3(x, yMax));
            Handles.EndGUI();
        }

        static void DrawDot(float x, float y, Color color) {
            Handles.BeginGUI();
            Handles.color = color;
            Handles.DrawSolidDisc(new Vector3(x, y), Vector3.forward, 3f);
            Handles.EndGUI();
        }

        static void PlayPreviewAudio(AudioClip clip) {
            if (clip == null) {
                return;
            }

            StopPreviewAudio();
            var playMethod = AudioUtilType?.GetMethod("PlayPreviewClip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            playMethod?.Invoke(null, new object[] { clip, 0, false });
        }

        static void PlayOneShotPreviewAudio(AudioClip clip) {
            if (clip == null) {
                return;
            }

            var playMethod = AudioUtilType?.GetMethod("PlayPreviewClip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
            playMethod?.Invoke(null, new object[] { clip, 0, false });
        }

        static void StopPreviewAudio() {
            var stopAllPreviewClips = AudioUtilType?.GetMethod("StopAllPreviewClips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (stopAllPreviewClips != null) {
                stopAllPreviewClips.Invoke(null, null);
                return;
            }

            var stopAllClips = AudioUtilType?.GetMethod("StopAllClips", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            if (stopAllClips != null) {
                stopAllClips.Invoke(null, null);
                return;
            }

            var stopPreviewClip = AudioUtilType?.GetMethod("StopPreviewClip", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            stopPreviewClip?.Invoke(null, null);
        }

        sealed class BeatCluster {
            float sum;
            int count;

            public BeatCluster(float initialValue) {
                sum = initialValue;
                count = 1;
            }

            public float Mean => sum / count;

            public void Add(float value) {
                sum += value;
                count++;
            }
        }
    }
}
