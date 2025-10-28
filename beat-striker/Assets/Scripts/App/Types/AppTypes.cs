namespace Core.App.Types {

    public enum AppScene {
        Title,
        StageSelect,
        CharacterSelect,
        Battle,
        Battle_Stage,
        Battle_Street,
        None
    }

    public readonly struct PlayerId {
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

        public static bool operator ==(PlayerId left, PlayerId right) {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right) {
            return !left.Equals(right);
        }

        public override readonly string ToString() {
            return $"PlayerId({value})";
        }
    }

    public readonly struct StrikerId {
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

        public static bool operator ==(StrikerId left, StrikerId right) {
            return left.Equals(right);
        }

        public static bool operator !=(StrikerId left, StrikerId right) {
            return !left.Equals(right);
        }

        public override readonly string ToString() {
            return $"StrikerId({value})";
        }
    }

    public readonly struct StageId {
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

        public static bool operator ==(StageId left, StageId right) {
            return left.Equals(right);
        }

        public static bool operator !=(StageId left, StageId right) {
            return !left.Equals(right);
        }

        public override readonly string ToString() {
            return $"StageId({value})";
        }
    }

    public readonly struct TrackId {
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

        public static bool operator ==(TrackId left, TrackId right) {
            return left.Equals(right);
        }

        public static bool operator !=(TrackId left, TrackId right) {
            return !left.Equals(right);
        }

        public override readonly string ToString() {
            return $"TrackId({value})";
        }
    }
}