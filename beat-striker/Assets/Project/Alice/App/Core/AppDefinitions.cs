using UnityEngine;

namespace Alice {
    public enum AppScene {
        Title,
        CharacterSelect,
        StageSelect,
        Battle,
        ResultMenu,
    }

    public record MusicInfo(string Id, string DisplayName, AudioClip AudioClip, float Bpm, float Offset);
    public record StageInfo(string Id, string DisplayName, string SceneName);
    public record StrikerInfo(string Id, string DisplayName, Striker BattleStriker, Sprite Portrait);
    public record BeatOffsetSetting(float CommandTimeOffset, float ViewTimeOffset, float BeatTimeOffset);
    public record VolumeBalance(float MasterVolume, float BgmVolume, float SeVolume);

    public record StrikerSelectionRequest(int PlayerId, string StrikerId);
    public record StageSelectionRequest(string StageId);
    public record MusicSelectionRequest(string MusicId);
    public record PlayerStrikerSelection(int PlayerId, StrikerInfo Striker);
}
