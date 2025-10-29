public class Beat {
    public readonly float time;
    internal Beat(float time) {
        this.time = time;
    }
}

public class BeatResult {
    public readonly Status status;

    internal BeatResult(Status status) {
        this.status = status;
    }

    public static implicit operator bool(BeatResult result) => result.status != Status.MISS;

    public enum Status {
        PERFECT,
        GOOD,
        MISS,
    }
}