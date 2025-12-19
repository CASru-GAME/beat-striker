namespace Core.Striker
{
    public enum TransitionType
    {
        None,
        Idle,
        Walk,
        Dash,
        Attack,
        Guard,
        Charge,
        ChargeEnd,
        Special,
        Dead
    }

    public interface IStrikerTransitionRequest
    {
        TransitionType Type { get; }
    }

    public readonly struct StrikerTransitionRequest : IStrikerTransitionRequest
    {
        public TransitionType Type { get; }

        public StrikerTransitionRequest(TransitionType type)
        {
             Type = type;
        }
    }
}
