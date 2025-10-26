namespace Core.App.Types {
    public enum TransitionCommand {
        End,
        Back,
        Next,
    }

    public enum AppScene {
        Title,
        StageSelect,
        CharacterSelect,
        Battle,
        None
    }

    public enum Strikers {
        None,
        Hero,
        Warrior,
        Wizard,
        Satan,
    }

    public enum Stages {
        Street,
        Stage
    }

    public struct Track {
        public string title;
        public string description;
    }

    public class PlayerId {
        public readonly int value;
        public PlayerId(int value) {
            this.value = value;
        }
    }
}