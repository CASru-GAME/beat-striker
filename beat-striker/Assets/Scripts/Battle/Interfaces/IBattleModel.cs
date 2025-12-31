
using System;
using Core.App.Types;
using Core.Utils;

namespace Core.Battle {
    public interface IBattleModel : IBattlemodelGetter {
        void NextRound();
        void AddLoser(PlayerId playerId);
        StrikerId? GetStriker(PlayerId playerId);
        void SetStriker(PlayerId playerId, StrikerId? striker);

        // State machine / Update
        void OnUpdate(float deltaTime);
        void ChangeState(IBattleState newState);

        // Observable subscriptions (replacing BattleEvents)
        IDisposable SubscribeRoundChanged(Action<int> listener);
        IDisposable SubscribeLoserAdded(Action<PlayerId> listener);
        IDisposable SubscribeBattleFinished(Action<PlayerId> listener);

        // New Subscriptions from BattleEvents
        IDisposable SubscribeRequireIntroPose(Action<PlayerId> listener);
        IDisposable SubscribeRequireVictoryPose(Action<PlayerId> listener);
        IDisposable SubscribeBattleStarted(Action<IBattlemodelGetter> listener);
        IDisposable SubscribeRoundStarted(Action<IBattlemodelGetter> listener);
        IDisposable SubscribeRoundFinished(Action<IBattlemodelGetter> listener);
        IDisposable SubscribeOutroStarted(Action<IBattlemodelGetter> listener);
        IDisposable SubscribeResultStarted(Action<IBattlemodelGetter> listener);
        IDisposable SubscribeBeat(Action<BeatInfo> listener);

        // Trigger methods (replacing Fire...)
        void FireRequireIntroPose(PlayerId playerId);
        void FireRequireVictoryPose(PlayerId playerId);
        void FireBattleStarted();
        void FireRoundStarted();
        void FireRoundFinished();
        void FireOutroStarted();
        void FireResultStarted();
        void FireBeat(PlayerId playerId, BeatResult result);

        // Callbacks from View
        void OnIntroAnimationFinished();
        void OnRoundStartAnimationFinished();
        void OnRoundFinishAnimationFinished();
        void OnOutroAnimationFinished();
    }

    public interface IBattlemodelGetter {
        PlayerId GetWinner(int round);
        int GetWinCount(PlayerId playerId);
        int GetCurrentRound();
        bool IsFinished();
        PlayerId GetFinalWinner();
    }

    public readonly struct BeatInfo {
        public readonly PlayerId PlayerId;
        public readonly BeatResult Result;
        public BeatInfo(PlayerId playerId, BeatResult result) {
            PlayerId = playerId;
            Result = result;
        }
    }
}
