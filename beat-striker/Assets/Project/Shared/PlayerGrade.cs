using System;

namespace Alice {
    public enum PlayerGrade { Pianissimo, Piano, MezzoPiano, MezzoForte, Forte, Fortissimo }

    public static class PlayerGrades {
        public static string Label(PlayerGrade grade) => grade switch {
            PlayerGrade.Pianissimo => "ピアニッシモ級",
            PlayerGrade.Piano => "ピアノ級",
            PlayerGrade.MezzoPiano => "メゾピアノ級",
            PlayerGrade.MezzoForte => "メゾフォルテ級",
            PlayerGrade.Forte => "フォルテ級",
            PlayerGrade.Fortissimo => "フォルティッシモ級",
            _ => "階級不明",
        };

        public static PlayerGrade Cpu(int level, Striker striker) => striker == Striker.DemonKing && level == 4
            ? PlayerGrade.Fortissimo : (PlayerGrade)Math.Clamp(level, 1, 4);
    }

    [Serializable]
    public record PlayerIdentity {
        public string name = "";
        public int grade;
        public bool isCpu;
        public int cpuLevel;
        public string GradeLabel => PlayerGrades.Label((PlayerGrade)grade);
    }
}
