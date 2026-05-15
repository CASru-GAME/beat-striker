using System;
using VContainer;

namespace Alice {
    public interface IOnlineDuelIdentity {
        string DuelSessionId { get; }
    }

    public class OnlineDuelIdentity : IOnlineDuelIdentity {
        public string DuelSessionId { get; }

        [Inject]
        public OnlineDuelIdentity() {
            DuelSessionId = Guid.NewGuid().ToString("N");
        }
    }
}
