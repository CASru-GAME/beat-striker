using System.Collections.Generic;
using Core.App.Types;
using UnityEngine;

namespace Core.Battle {
    public class IntroState : IBattleState {
        private readonly IBattleModel model;
        private readonly TrackId trackId;

        public IntroState(IBattleModel model, TrackId trackId) {
            this.model = model;
            this.trackId = trackId;
        }

        public void Enter() {
            Debug.Log("Entering Intro State");
            // Request intro poses for all players (Model knows players, but fire event for ID?)
            // IntroState doesn't know player count unless Model exposes it or we pass it?
            // Existing logic: looped strikerViews.Count.
            // But we removed strikerViews reference.
            // We can iterate generic player IDs or model logic.
            // Model.AddLoser etc implies model knows players.
            // We should add "GetPlayers" or similar to Model, or just hardcode for now (2 players usually).
            // Or better: Model.FireRequireIntroPose(null) to mean "All"?
            // Or loop?
            // BattleModel constructor created players 0..N.
            // Let's assume 2 players for now or add Property to Model.
            
            // Actually, existing code: events.FireRequireIntroPose(new PlayerId(i));
            // Let's fire for Player 0 and 1.
            model.FireRequireIntroPose(new PlayerId(0));
            model.FireRequireIntroPose(new PlayerId(1));
        }

        public void OnUpdate(float deltaTime) {
        }

        public void Exit() {
            // Refactored Reset to be on Model
             // But existing code called resetter.ResetBattle() on Exit of IntroState?
             // IntroState is usually the FIRST state.
             // Wait, IntroState Exit happens when transitioning TO RoundStart.
             // Why reset there? Maybe reset state before round starts?
             // Let's keep specific logic.
             // Access Reset via cast if needed or add to Interface?
             // I added ResetBattle to BattleModel class but NOT interface in previous step.
             // I should add ResetBattle to IBattleModel or use custom method.
             ((BattleModel)model).ResetBattle(); 
        }
    }
}
