


using System.Collections.Generic;
using App;
using R3;
using VContainer;

namespace Alice {
    public record StrikerRegistration(int PlayerId, IStrikerHub Hub);
    public record StrikerUnregistration(int PlayerId);

    public interface IStrikerRegistry {
        Option<IStrikerHub> Get(int playerId);
        IEnumerable<IStrikerHub> GetAllStrikers();
        Observable<StrikerRegistration> OnRegistered { get; }
        Observable<StrikerUnregistration> OnUnregistered { get; }
        void RequestRegister(int playerId, IStrikerHub hub);
        void RequestUnregister(int playerId);
    }

    public class StrikerRegistry : IStrikerRegistry {
        readonly Dictionary<int, IStrikerHub> strikerHubs = new Dictionary<int, IStrikerHub>();
        readonly Subject<StrikerRegistration> registeredSubject = new();
        readonly Subject<StrikerUnregistration> unregisteredSubject = new();

        public Observable<StrikerRegistration> OnRegistered => registeredSubject;
        public Observable<StrikerUnregistration> OnUnregistered => unregisteredSubject;

        [Inject]
        public StrikerRegistry() {
        }

        public Option<IStrikerHub> Get(int playerId) {
            if (strikerHubs.TryGetValue(playerId, out var hub)) {
                return hub.ToOption();
            }
            return null;
        }

        public IEnumerable<IStrikerHub> GetAllStrikers() {
            var list = new List<IStrikerHub>();
            foreach (var pair in strikerHubs) {
                list.Add(pair.Value);
            }
            return list;
        }

        public void RequestRegister(int playerId, IStrikerHub hub) {
            if (!strikerHubs.ContainsKey(playerId)) {
                strikerHubs.Add(playerId, hub);
                registeredSubject.OnNext(new StrikerRegistration(playerId, hub));
            }
        }

        public void RequestUnregister(int playerId) {
            if (strikerHubs.ContainsKey(playerId)) {
                strikerHubs.Remove(playerId);
                unregisteredSubject.OnNext(new StrikerUnregistration(playerId));
            }
        }
    }
}