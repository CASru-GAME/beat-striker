using System;
using Fusion;
using UnityEngine;

namespace Alice {
    public readonly struct OnlineStrikerPreBeatStateSnapshotMessage {
        public readonly long Sequence;
        public readonly int ApplyBeatIndex;
        public readonly int PlayerId;
        public readonly float HitPoint;
        public readonly float SpecialPoint;
        public readonly Vector3 Position;
        public readonly string StatePathId;
        public readonly float SentNetworkTime;

        public OnlineStrikerPreBeatStateSnapshotMessage(
            long sequence,
            int applyBeatIndex,
            int playerId,
            float hitPoint,
            float specialPoint,
            Vector3 position,
            string statePathId,
            float sentNetworkTime) {
            Sequence = sequence;
            ApplyBeatIndex = applyBeatIndex;
            PlayerId = playerId;
            HitPoint = hitPoint;
            SpecialPoint = specialPoint;
            Position = position;
            StatePathId = statePathId ?? string.Empty;
            SentNetworkTime = sentNetworkTime;
        }
    }

    public sealed class OnlineStrikerPreBeatStateSnapshotUnreliableRpc : SimulationBehaviour {
        public static event Action<NetworkRunner, OnlineStrikerPreBeatStateSnapshotMessage> OnSnapshotReceived;

        public static void Publish(NetworkRunner runner, OnlineStrikerPreBeatStateSnapshotMessage message) {
            RPC_StrikerPreBeatStateSnapshot(
                runner,
                message.Sequence,
                message.ApplyBeatIndex,
                message.PlayerId,
                message.HitPoint,
                message.SpecialPoint,
                message.Position,
                message.StatePathId,
                message.SentNetworkTime);
        }

        [Rpc(RpcSources.All, RpcTargets.All, InvokeLocal = false, Channel = RpcChannel.Unreliable, TickAligned = false)]
        public static void RPC_StrikerPreBeatStateSnapshot(
            NetworkRunner runner,
            long sequence,
            int applyBeatIndex,
            int playerId,
            float hitPoint,
            float specialPoint,
            Vector3 position,
            string statePathId,
            float sentNetworkTime,
            RpcInfo info = default) {
            OnSnapshotReceived?.Invoke(
                runner,
                new OnlineStrikerPreBeatStateSnapshotMessage(
                    sequence,
                    applyBeatIndex,
                    playerId,
                    hitPoint,
                    specialPoint,
                    position,
                    statePathId,
                    sentNetworkTime));
        }
    }
}
