using UnityEngine;

namespace Core.Striker
{
    public class StrikerStateContext
    {
        public IStrikerHub Hub { get; }
        public Rigidbody Rigidbody { get; }
        public Animator Animator { get; }

        public StrikerStateContext(IStrikerHub hub, Rigidbody rigidbody, Animator animator)
        {
            Hub = hub;
            Rigidbody = rigidbody;
            Animator = animator;
        }
    }
}
