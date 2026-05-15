namespace Alice {
    /// <summary>
    /// オンライン対戦で「フロー上の足並み」を揃えるための同期ゲート（7種）。
    /// 各値は BattleOnlineSync で (gate, round, subIndex) 単位に双方到達（ビットマスク 0b11）まで待つバリアとして使う。
    /// subIndex は同一ゲート内の段階分け（例: RoundStart のアニメ前後）やビート番号などに用いる。
    /// </summary>
    public enum BattleFlowSyncGate {
        /// <summary>ラウンド1のみ、開始合図アニメの直前。</summary>
        Round1BeforeStartCue = 1,
        /// <summary>ラウンド開始相当（subIndex 0: アニメ前、1: 再生開始直前など）。</summary>
        RoundStart = 2,
        /// <summary>ラウンド解決〜次ラウンドの playable 開始手前まで。</summary>
        RoundEndToNextRound = 3,
        /// <summary>バトル終了時の結果同期（勝者演出・タイトル戻りの双方で同一ゲート ID）。</summary>
        BattleEndOutcomeSynced = 4,
        /// <summary>サスペンドメニュー表示をオンラインでは同一ビートで適用した後の相互確認。</summary>
        SuspendMenuBeatBarrier = 5,
        /// <summary>サスペンド解除（再開合意・BeatSyncResume 後）のクリア確認。</summary>
        SuspendMenuResumeClear = 6,
        /// <summary>バトルシーンへのシーン遷移終了後（暗転明け直前）の相互確認。遅い側の遷移が完了するまでオープニング演出を開始しない。</summary>
        SceneTransitionEnd = 7,
    }
}
