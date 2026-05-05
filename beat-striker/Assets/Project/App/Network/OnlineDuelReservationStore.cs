namespace Alice {
    public interface IOnlineDuelReservationStore {
        bool HasReservation { get; }
        string ReservationId { get; }
        void SetReservation(string reservationId);
        void ClearReservation();
    }

    public class OnlineDuelReservationStore : IOnlineDuelReservationStore {
        public bool HasReservation => !string.IsNullOrWhiteSpace(ReservationId);
        public string ReservationId { get; private set; } = "";

        public void SetReservation(string reservationId) {
            ReservationId = reservationId ?? "";
        }

        public void ClearReservation() {
            ReservationId = "";
        }
    }
}
