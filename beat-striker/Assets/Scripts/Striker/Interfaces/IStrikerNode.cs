using System;
using Core.Battle;

namespace Core.Striker
{
    public interface IStrikerNode
    {
        void OnTryTransition(IStrikerNodeContext context);
    }
}