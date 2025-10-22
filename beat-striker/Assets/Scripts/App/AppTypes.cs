

namespace Core.App {


    /// <summary>
    /// プレイヤーID
    /// アプリケーション内のカーソル移動やキャラクター選択に使用される
    /// </summary>
    public struct PlayerId {
        public int value;
        public PlayerId(int value) {
            this.value = value;
        }
    }   
}