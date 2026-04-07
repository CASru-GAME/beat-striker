using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Core;
using R3;

namespace Alice {

    [AddComponentMenu(" Button", 0)]
    public abstract class ActionEmitter : MonoBehaviour {
        public abstract Observable<BotanEventData> OnClickEvent { get; }
        public abstract Observable<BotanEventData> OnHoverEvent { get; }
        public abstract Observable<BotanEventData> OnHoverExitEvent { get; }
    }
}