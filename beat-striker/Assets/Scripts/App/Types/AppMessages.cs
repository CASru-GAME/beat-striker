
using Core.App.Types;
using Core.GamePad.Types;
using UnityEngine;

namespace Core.App.Presenters.Scene.Types {

    public static class AppMessages {

        public class RequireTransition {
            public readonly AppScene scene;
            public RequireTransition(AppScene scene) {
                this.scene = scene;
            }
        }

        public class RequireLoadScene { }

        public class RequireCursorDestroyed {
            private readonly bool isAll;
            private readonly PlayerId playerId;

            public RequireCursorDestroyed(PlayerId playerId) {
                this.playerId = playerId;
                isAll = false;
            }

            public RequireCursorDestroyed() {
                isAll = true;
            }

            public bool IsTarget(PlayerId playerId) {
                if (isAll) return true;
                return this.playerId.Equals(playerId);
            }
        }

        public class PlayerJoined {
            public readonly PlayerId playerId;

            public PlayerJoined(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class PlayerLeft {
            public readonly PlayerId playerId;

            public PlayerLeft(PlayerId playerId) {
                this.playerId = playerId;
            }
        }

        public class OnTransitionAnimationStarted {
            public readonly AppScene scene;

            public OnTransitionAnimationStarted(AppScene scene) {
                this.scene = scene;
            }
        }

        public class SelectStage {
            public readonly StageId stage;

            public SelectStage(StageId stage) {
                this.stage = stage;
            }
        }

        public class SelectStriker {
            public readonly StrikerId? striker;
            public readonly PlayerId playerId;

            public SelectStriker(PlayerId playerId, StrikerId? striker) {
                this.playerId = playerId;
                this.striker = striker;
            }
        }

        public class Changed_AllStrikersSelected {
            public readonly bool allSelected = true;

            public Changed_AllStrikersSelected(bool allSelected) {
                this.allSelected = allSelected;
            }
        }

        public class SelectTrack {
            public readonly TrackId track;

            public SelectTrack(TrackId track) {
                this.track = track;
            }
        }

        public class PlayBGM {
            public readonly BGMType bgmType;

            public PlayBGM(BGMType bgmType) {
                this.bgmType = bgmType;
            }
        }

        public class StopBGM { }

        public class SetCursorsActive {
            public readonly bool active;

            public SetCursorsActive(bool active) {
                this.active = active;
            }
        }

        public class SetCursorSortingOrder {
            public readonly int sortingOrder;

            public SetCursorSortingOrder(int sortingOrder) {
                this.sortingOrder = sortingOrder;
            }
        }


        public class CursorPositionUpdated {
            public readonly PlayerId playerId;
            public readonly Vector2 position;

            public CursorPositionUpdated(PlayerId playerId, Vector2 position) {
                this.playerId = playerId;
                this.position = position;
            }
        }

        /// <summary>
        /// 指定したPlayerIdでゲームパッドが参加したことを示すメッセージ
        /// </summary>
        public class JoinedWithPlayerId {
            public readonly GamePadId gamePadId;
            public readonly PlayerId playerId;

            public JoinedWithPlayerId(GamePadId gamePadId, Core.App.Types.PlayerId playerId) {
                this.gamePadId = gamePadId;
                this.playerId = playerId;
            }
        }

    }


}