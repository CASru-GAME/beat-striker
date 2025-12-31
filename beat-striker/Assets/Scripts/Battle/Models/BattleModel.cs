
using System;
using System.Collections.Generic;
using System.Linq;
using Core.App.Types;
using Core.Utils;

namespace Core.Battle {
    public class BattleModel : IBattleModel {

        private readonly List<PlayerId>[] deadPlayers;
        private readonly List<PlayerId> allPlayers;
        private int currentRound = 0;
        private readonly Dictionary<int, StrikerId> strikers = new();

        // State Machine
        private IBattleState currentState;

        // Observable events
        private readonly Subject<int> onRoundChanged = new();
        private readonly Subject<PlayerId> onLoserAdded = new();
        private readonly Subject<PlayerId> onBattleFinished = new();

        // Flow Events (Replacing BattleEvents)
        private readonly Subject<PlayerId> onRequireIntroPose = new();
        private readonly Subject<PlayerId> onRequireVictoryPose = new();
        private readonly Subject<IBattlemodelGetter> onBattleStarted = new(); // Used as Round Start signal in some contexts? No, strict Battle Start.
        private readonly Subject<IBattlemodelGetter> onRoundStarted = new();
        private readonly Subject<IBattlemodelGetter> onRoundFinished = new();
        private readonly Subject<IBattlemodelGetter> onBattleEnded = new(); // onBattleFinished from IBattleModel
        private readonly Subject<IBattlemodelGetter> onOutroStarted = new();
        private readonly Subject<IBattlemodelGetter> onResultStarted = new();
        private readonly Subject<BeatInfo> onBeat = new();

        // Note: IBattleModel has SubscribeBattleFinished (Action<PlayerId>).
        // But BattleEvents had SubscribeBattleFinished (Action<IBattlemodelGetter>) and SubscribePlayerDead.
        // I should align names. 
        // IBattleModel.SubscribeBattleFinished uses PlayerId (Winner).
        // BattleEvents.SubscribeBattleFinished used IBattlemodelGetter (Round End?).
        // Let's look at interfaces:
        // IBattleModel: SubscribeBattleFinished(Action<PlayerId>) -> Winner.
        // BattleEvents: SubscribeBattleFinished(Action<IBattlemodelGetter>) -> Likely Round End or Match End.
        // In Planner, I added SubscribeBattleFinished(Action<PlayerId>) to IBattleModel.
        // I also added SubscribeOutroStarted.

        // Re-implementing based on Interface definition

        public BattleModel(int playerCount) {
            this.allPlayers = Enumerable.Range(0, playerCount).Select(i => new PlayerId(i)).ToList();
            deadPlayers = new List<PlayerId>[10];
            for (int i = 0; i < deadPlayers.Length; i++) {
                deadPlayers[i] = new List<PlayerId>(this.allPlayers.Count);
            }
        }

        public void OnUpdate(float deltaTime) {
            currentState?.OnUpdate(deltaTime);
        }

        public void ChangeState(IBattleState newState) {
            currentState?.Exit();
            currentState = newState;
            currentState?.Enter();
        }

        // Subscription methods
        public IDisposable SubscribeRoundChanged(Action<int> listener) => onRoundChanged.Subscribe(listener);
        public IDisposable SubscribeLoserAdded(Action<PlayerId> listener) => onLoserAdded.Subscribe(listener);
        public IDisposable SubscribeBattleFinished(Action<PlayerId> listener) => onBattleFinished.Subscribe(listener);

        public IDisposable SubscribeRequireIntroPose(Action<PlayerId> listener) => onRequireIntroPose.Subscribe(listener);
        public IDisposable SubscribeRequireVictoryPose(Action<PlayerId> listener) => onRequireVictoryPose.Subscribe(listener);
        public IDisposable SubscribeBattleStarted(Action<IBattlemodelGetter> listener) => onBattleStarted.Subscribe(listener);
        public IDisposable SubscribeRoundStarted(Action<IBattlemodelGetter> listener) => onRoundStarted.Subscribe(listener);
        public IDisposable SubscribeRoundFinished(Action<IBattlemodelGetter> listener) => onRoundFinished.Subscribe(listener);
        public IDisposable SubscribeOutroStarted(Action<IBattlemodelGetter> listener) => onOutroStarted.Subscribe(listener);
        public IDisposable SubscribeResultStarted(Action<IBattlemodelGetter> listener) => onResultStarted.Subscribe(listener);
        public IDisposable SubscribeBeat(Action<BeatInfo> listener) => onBeat.Subscribe(listener);

        // Fire methods
        public void FireRequireIntroPose(PlayerId playerId) => onRequireIntroPose.Fire(playerId);
        public void FireRequireVictoryPose(PlayerId playerId) => onRequireVictoryPose.Fire(playerId);
        public void FireBattleStarted() => onBattleStarted.Fire(this);
        public void FireRoundStarted() => onRoundStarted.Fire(this);
        public void FireRoundFinished() => onRoundFinished.Fire(this);
        public void FireOutroStarted() => onOutroStarted.Fire(this);
        public void FireResultStarted() => onResultStarted.Fire(this);
        public void FireBeat(PlayerId playerId, BeatResult result) => onBeat.Fire(new BeatInfo(playerId, result));

        // Callbacks from View


        // Wait, I need to store dependencies in BattleModel if I want to instantiate States.
        // Dependencies required by States:
        // - IBattleModel (this)
        // - IRythmTrackModel
        // - IBattleResetter (Model needs to hold this?)
        // - IBattleView (I decided to remove this dependency from States)
        // - TrackId
        // - List<IStrikerView> (I decided to remove this)

        // I will add necessary fields to BattleModel.
        private IRythmTrackModel rythmTrackModel;
        private Action resetAction; // Replaces IBattleResetter
        private TrackId trackId;

        public void InitializeDependencies(IRythmTrackModel rythmTrackModel, Action resetAction, TrackId trackId) {
            this.rythmTrackModel = rythmTrackModel;
            this.resetAction = resetAction;
            this.trackId = trackId;
        }

        public void StartBattle() {
            ChangeState(new IntroState(this, this.trackId)); // Updated State constructor signature
        }

        public void OnIntroAnimationFinished() {
            ChangeState(new RoundStartState(this));
        }

        public void OnRoundStartAnimationFinished() {
            ChangeState(new RoundState(this, this.trackId));
        }

        public void OnRoundFinishAnimationFinished() {
            ChangeState(new RoundStartState(this));
        }

        public void OnOutroAnimationFinished() {
            ChangeState(new ResultState(this));
        }

        // Domain Logic
        public PlayerId GetWinner(int round) {
            var dead = deadPlayers[round];
            var res = allPlayers.FirstOrDefault(p => !dead.Contains(p));
            return res == null ? allPlayers[0] : res;
        }

        public int GetCurrentRound() {
            return currentRound;
        }

        public void NextRound() {
            currentRound++;
            onRoundChanged.Fire(currentRound);
        }

        public bool IsFinished() {
            var winCounts = GetWinCounts();
            foreach (var kvp in winCounts) {
                if (kvp.Value >= 2) return true;
            }
            return false;
        }

        public PlayerId GetFinalWinner() {
            var winCounts = GetWinCounts();
            foreach (var kvp in winCounts) {
                if (kvp.Value >= 2) return kvp.Key;
            }
            return GetWinner(currentRound);
        }

        private Dictionary<PlayerId, int> GetWinCounts() {
            var winCounts = new Dictionary<PlayerId, int>();
            for (int r = 0; r <= currentRound; r++) {
                if (deadPlayers[r].Count == 0) continue;
                PlayerId winner = GetWinner(r);
                if (!winCounts.ContainsKey(winner)) winCounts[winner] = 0;
                winCounts[winner]++;
            }
            return winCounts;
        }

        public void AddLoser(PlayerId playerId) {
            if (!deadPlayers[currentRound].Contains(playerId)) {
                deadPlayers[currentRound].Add(playerId);
                onLoserAdded.Fire(playerId);

                if (IsFinished()) {
                    onBattleFinished.Fire(GetFinalWinner()); // Observable
                    // Trigger State Transition?
                    // Presenter did: ChangeState(OutroState) or RoundFinishState
                    // Model handles loop?
                    if (IsFinished()) {
                        ChangeState(new OutroState(this));
                    }
                    else {
                        ChangeState(new RoundFinishState(this));
                    }
                }
                else {
                    ChangeState(new RoundFinishState(this));
                }
            }
        }

        // Deprecated/Modified methods
        // AddLoser handles logic formerly in OnPlayerDead

        public int GetWinCount(PlayerId playerId) {
            var winCounts = GetWinCounts();
            return winCounts.ContainsKey(playerId) ? winCounts[playerId] : 0;
        }

        public StrikerId? GetStriker(PlayerId playerId) {
            return strikers.TryGetValue(playerId.value, out var id) ? id : null;
        }

        public void SetStriker(PlayerId playerId, StrikerId? striker) {
            if (striker.HasValue) strikers[playerId.value] = striker.Value;
            else strikers.Remove(playerId.value);
        }

        public void ResetBattle() {
            resetAction?.Invoke();
        }
    }
}
