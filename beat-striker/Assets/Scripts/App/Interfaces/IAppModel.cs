using System;
using Core.App.Presenters.Scene;
using Core.App.Types;
using UnityEngine;

namespace Core.App.Interfaces {
    public interface IAppModel : ISceneStateController {
        
        // --- Subscriptions (from AppEvents) ---
        IDisposable SubscribeRequireTransition(Action<AppScene> listener);
        IDisposable SubscribeRequireLoadScene(Action listener);
        IDisposable SubscribeTransitionAnimationStarted(Action<AppScene> listener);
        
        IDisposable SubscribePlayerJoined(Action<PlayerId> listener);
        IDisposable SubscribePlayerLeft(Action<PlayerId> listener);
        
        IDisposable SubscribeSetCursorsActive(Action<bool> listener);
        IDisposable SubscribeSetCursorSortingOrder(Action<int> listener);
        IDisposable SubscribeRequireCursorDestroyed(Action<CursorDestroyRequest> listener);
        IDisposable SubscribeCursorPositionUpdated(Action<CursorPositionUpdate> listener);
        
        IDisposable SubscribeSelectStriker(Action<StrikerSelection> listener);
        IDisposable SubscribeSelectStage(Action<StageId> listener);
        IDisposable SubscribeSelectTrack(Action<TrackId> listener);
        IDisposable SubscribeAllStrikersSelectedChanged(Action<bool> listener);
        
        IDisposable SubscribePlayBGM(Action<BGMType> listener);
        IDisposable SubscribeStopBGM(Action listener);
        
        // --- Fire Events ---
        void FireRequireTransition(AppScene scene);
        void FireRequireLoadScene();
        void FireTransitionAnimationStarted(AppScene scene);
        
        void FirePlayerJoined(PlayerId playerId);
        void FirePlayerLeft(PlayerId playerId);
        
        void FireSetCursorsActive(bool active);
        void FireSetCursorSortingOrder(int order);
        void FireRequireCursorDestroyed(CursorDestroyRequest request);
        void FireCursorPositionUpdated(CursorPositionUpdate update);
        
        void FireSelectStriker(StrikerSelection selection);
        void FireSelectStage(StageId stageId);
        void FireSelectTrack(TrackId trackId);
        void FireAllStrikersSelectedChanged(bool allSelected);
        
        void FirePlayBGM(BGMType bgmType);
        void FireStopBGM();
    }
    
    // Supporting types (Moved from AppEvents)
    public struct CursorDestroyRequest {
        public readonly bool isAll;
        public readonly PlayerId playerId;
        
        public CursorDestroyRequest(PlayerId playerId) {
            this.playerId = playerId;
            this.isAll = false;
        }
        
        public static CursorDestroyRequest All() => new CursorDestroyRequest(true);
        
        private CursorDestroyRequest(bool isAll) {
            this.isAll = isAll;
            this.playerId = default;
        }
        
        public bool IsTarget(PlayerId target) => isAll || playerId.Equals(target);
    }
    
    public struct CursorPositionUpdate {
        public readonly PlayerId playerId;
        public readonly Vector2 position;
        
        public CursorPositionUpdate(PlayerId playerId, Vector2 position) {
            this.playerId = playerId;
            this.position = position;
        }
    }
    
    public struct StrikerSelection {
        public readonly PlayerId playerId;
        public readonly StrikerId? strikerId;
        
        public StrikerSelection(PlayerId playerId, StrikerId? strikerId) {
            this.playerId = playerId;
            this.strikerId = strikerId;
        }
    }
}
