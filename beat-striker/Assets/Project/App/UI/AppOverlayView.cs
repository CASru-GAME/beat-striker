using Core;
using R3;
using UnityEngine;

namespace Alice {
    public class AppOverlayView : MonoBehaviour {
        readonly Subject<Unit> incomingDuelAccepted = new();
        readonly Subject<Unit> incomingDuelRejected = new();
        readonly Subject<Unit> candidateDuelInvited = new();
        readonly Subject<Unit> candidateDuelSkipped = new();

        [SerializeField] GameObject overlayRoot;
        [SerializeField] GameObject onlineIndicatorRoot;
        [SerializeField] GameObject incomingDuelDialogRoot;
        [SerializeField] ActionEmitter incomingDuelAcceptEmitter;
        [SerializeField] ActionEmitter incomingDuelRejectEmitter;
        [SerializeField] GameObject candidateDuelDialogRoot;
        [SerializeField] ActionEmitter candidateDuelInviteEmitter;
        [SerializeField] ActionEmitter candidateDuelSkipEmitter;

        [SerializeField] GameObject matchStatusRoot;
        [SerializeField] TMPro.TextMeshProUGUI matchOpponentNameText;
        [SerializeField] TMPro.TextMeshProUGUI matchTimeLimitText;
        [SerializeField] TMPro.TextMeshProUGUI matchOpponentStatusText;

        public Observable<Unit> IncomingDuelAccepted => incomingDuelAccepted;
        public Observable<Unit> IncomingDuelRejected => incomingDuelRejected;
        public Observable<Unit> CandidateDuelInvited => candidateDuelInvited;
        public Observable<Unit> CandidateDuelSkipped => candidateDuelSkipped;

        void Awake() {
            incomingDuelAcceptEmitter.OnClickEvent.Subscribe(_ => incomingDuelAccepted.OnNext(Unit.Default)).AddTo(this);
            incomingDuelRejectEmitter.OnClickEvent.Subscribe(_ => incomingDuelRejected.OnNext(Unit.Default)).AddTo(this);
            candidateDuelInviteEmitter.OnClickEvent.Subscribe(_ => candidateDuelInvited.OnNext(Unit.Default)).AddTo(this);
            candidateDuelSkipEmitter.OnClickEvent.Subscribe(_ => candidateDuelSkipped.OnNext(Unit.Default)).AddTo(this);
            SetIncomingDuelVisible(false);
            SetCandidateDuelVisible(false);
            SetMatchStatusVisible(false);
        }

        public void SetOverlayVisible(bool visible) {
            overlayRoot.SetActive(visible);
        }

        public void SetOnlineIndicatorVisible(bool visible) {
            onlineIndicatorRoot.SetActive(visible);
        }

        public void SetIncomingDuelVisible(bool visible) {
            incomingDuelDialogRoot.SetActive(visible);
        }

        public void SetCandidateDuelVisible(bool visible) {
            candidateDuelDialogRoot.SetActive(visible);
        }

        public void SetMatchStatusVisible(bool visible) {
            if (matchStatusRoot != null) {
                matchStatusRoot.SetActive(visible);
            }
        }

        public void SetMatchStatus(string opponentName, string timeLimit, string opponentStatus) {
            if (matchOpponentNameText != null) {
                matchOpponentNameText.text = opponentName;
            }
            if (matchTimeLimitText != null) {
                matchTimeLimitText.text = timeLimit;
            }
            if (matchOpponentStatusText != null) {
                matchOpponentStatusText.text = opponentStatus;
            }
        }
    }
}
