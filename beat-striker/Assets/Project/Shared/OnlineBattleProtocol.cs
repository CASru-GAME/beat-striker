using Fusion.Sockets;

namespace Alice {
    public static class OnlineBattleProtocol {
        public static readonly ReliableKey PhaseKey = ReliableKey.FromInts(0x4253, 2, 1);
        public static readonly ReliableKey OutcomeKey = ReliableKey.FromInts(0x4253, 2, 2);
        public static readonly ReliableKey PauseRequestKey = ReliableKey.FromInts(0x4253, 2, 3);
        public static readonly ReliableKey ResumeRequestKey = ReliableKey.FromInts(0x4253, 2, 4);
        public static readonly ReliableKey SuspendFinishRequestKey = ReliableKey.FromInts(0x4253, 2, 5);
        public static readonly ReliableKey RoundResolutionRequestKey = ReliableKey.FromInts(0x4253, 2, 6);
        public static readonly ReliableKey BeatCommandKey = ReliableKey.FromInts(0x4253, 2, 7);
        public static readonly ReliableKey RoundStartReadyKey = ReliableKey.FromInts(0x4253, 2, 8);
        public static readonly ReliableKey RoundStartScheduleKey = ReliableKey.FromInts(0x4253, 2, 9);
        public static readonly ReliableKey BeatSyncResumeKey = ReliableKey.FromInts(0x4253, 2, 10);
        public static readonly ReliableKey StrikerPreCommandSnapshotKey = ReliableKey.FromInts(0x4253, 2, 11);
        // バトルフロー用バリア: クライアント→リレーサーバー→相手へ転送し、双方が同じゲートに到達したか判定する。
        public static readonly ReliableKey FlowGateKey = ReliableKey.FromInts(0x4253, 2, 12);
        // サスペンド解除の「解除した」合図を双方向で揃え、対称な resumeNetworkTime 合意の前提にする。
        public static readonly ReliableKey ResumeAckKey = ReliableKey.FromInts(0x4253, 2, 13);
        // ポーズメニュー要求を「適用ビート」に紐づけ、BeatJudge のオンライン拍処理と同じ拍で両者がサスペンドに入る。
        public static readonly ReliableKey SuspendMenuBeatKey = ReliableKey.FromInts(0x4253, 2, 14);

        public static bool IsRelayKey(ReliableKey key) {
            return key == PhaseKey
                   || key == OutcomeKey
                   || key == PauseRequestKey
                   || key == ResumeRequestKey
                   || key == SuspendFinishRequestKey
                   || key == RoundResolutionRequestKey
                   || key == BeatCommandKey
                   || key == RoundStartReadyKey
                   || key == RoundStartScheduleKey
                   || key == BeatSyncResumeKey
                   || key == StrikerPreCommandSnapshotKey
                   || key == FlowGateKey
                   || key == ResumeAckKey
                   || key == SuspendMenuBeatKey;
        }
    }
}
