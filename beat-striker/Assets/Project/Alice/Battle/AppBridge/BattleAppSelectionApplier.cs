using System.Collections.Generic;

namespace Alice {
    public interface IBattleAppSelectionApplier {
        void Apply();
    }

    public class BattleAppSelectionApplier : IBattleAppSelectionApplier {
        readonly IAppSettingsModel appSettingsModel;
        readonly IPlayerSettingsModel playerSettingsModel;
        readonly BattleConfig battleConfig;
        readonly BeatConfig beatConfig;

        public BattleAppSelectionApplier(IAppSettingsModel appSettingsModel, IPlayerSettingsModel playerSettingsModel, BattleConfig battleConfig, BeatConfig beatConfig) {
            this.appSettingsModel = appSettingsModel;
            this.playerSettingsModel = playerSettingsModel;
            this.battleConfig = battleConfig;
            this.beatConfig = beatConfig;
        }

        public void Apply() {
            var selectedStrikers = new List<Striker>();
            for (var i = 0; i < battleConfig.PlayerTransforms.Count; i++) {
                selectedStrikers.Add(playerSettingsModel.GetStriker(i).BattleStriker);
            }

            battleConfig.ApplyStrikers(selectedStrikers);
            beatConfig.ApplyTrackSelection(appSettingsModel.SelectedMusic.CurrentValue.Id);
            beatConfig.ApplyBeatOffsets(appSettingsModel.BeatOffset.CurrentValue);
        }
    }
}
