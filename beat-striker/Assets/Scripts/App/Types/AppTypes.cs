namespace Core.App.Types {
    public enum TransitionRequire {
        LoadScene,
        StartExitAnimation,
        Next,
    }

    public enum AppScene {
        Title,
        StageSelect,
        CharacterSelect,
        Battle,
        None
    }

    public class PlayerId {
        public readonly int value;
        public PlayerId(int value) {
            this.value = value;
        }

        public override bool Equals(object obj) {
            if (obj is PlayerId other) {
                return value == other.value;
            }
            return false;
        }

        public override int GetHashCode() {
            return value.GetHashCode();
        }
    }

    public class StrikerId {
        public readonly string value;

        public StrikerId(string value) {
            this.value = value;
        }

        public override bool Equals(object obj) {
            if (obj is StrikerId other) {
                return value == other.value;
            }
            return false;
        }

        public override int GetHashCode() {
            return value?.GetHashCode() ?? 0;
        }
    }

    public class StageId {
        public readonly string value;
        public StageId(string value) {
            this.value = value;
        }

        public override bool Equals(object obj) {
            if (obj is StageId other) {
                return value == other.value;
            }
            return false;
        }

        public override int GetHashCode() {
            return value?.GetHashCode() ?? 0;
        }
    }

    public class TrackId {
        public readonly string value;
        public TrackId(string value) {
            this.value = value;
        }

        public override bool Equals(object obj) {
            if (obj is TrackId other) {
                return value == other.value;
            }
            return false;
        }

        public override int GetHashCode() {
            return value?.GetHashCode() ?? 0;
        }
    }
}