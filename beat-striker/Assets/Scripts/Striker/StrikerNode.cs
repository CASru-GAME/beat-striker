using System;
using System.Collections;
using System.Collections.Generic;
using Core.App.Interfaces;
using Core.App.Models;
using Core.App.Types;
using Core.Utils;
using Core.Battle;
using Core.GamePad.Types;
using Core.Striker.Components;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace Core.Striker {
    [AddComponentMenu(" Striker Hub", 0)]
    public abstract class StrikerNode : MonoBehaviour, IStrikerNode {
        public abstract void OnTryTransition(IStrikerNodeContext context);
    }
}