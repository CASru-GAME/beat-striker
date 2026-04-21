using UnityEngine;

namespace Core.LargeSatan {
    static class StrikerWarpSafetyUtility {
        const float MIN_DISTANCE_EPSILON = 0.01f;
        const float HIT_MARGIN = 0.02f;
        const float OPPONENT_MARGIN = 0.001f;

        public static Vector3 ComputeSafeDestination(IStrikerContext context, Vector3 startPos, Vector3 targetPos, LayerMask wallMask, float fallbackClearanceRadius, float minDistanceFromOpponent) {
            return ComputeSafeLinearMoveDestination(context, startPos, targetPos - startPos, wallMask, fallbackClearanceRadius, minDistanceFromOpponent);
        }

        public static Vector3 ComputeSafeLinearMoveDestination(IStrikerContext context, Vector3 startPos, Vector3 desiredDelta, LayerMask wallMask, float fallbackClearanceRadius, float minDistanceFromOpponent) {
            float desiredDistance = desiredDelta.magnitude;
            if (desiredDistance <= MIN_DISTANCE_EPSILON) {
                return startPos;
            }

            var moveDirection = desiredDelta / desiredDistance;
            GetClearanceShape(context, fallbackClearanceRadius, out var radius);

            var safeDist = ComputeWallSafeDistance(context, startPos, moveDirection, desiredDistance, radius, wallMask);
            if (safeDist <= MIN_DISTANCE_EPSILON) {
                return startPos;
            }

            safeDist = ClampDistanceByOpponent(startPos, moveDirection, safeDist, context.GetOpponent().CenterPosition.CurrentValue, minDistanceFromOpponent);
            safeDist = ComputeWallSafeDistance(context, startPos, moveDirection, safeDist, radius, wallMask);

            return startPos + moveDirection * safeDist;
        }

        static void GetClearanceShape(IStrikerContext context, float fallbackClearanceRadius, out float radius) {
            radius = fallbackClearanceRadius;

            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var capsule)) {
                radius = capsule.radius;
            }
        }

        static float ComputeWallSafeDistance(IStrikerContext context, Vector3 startPos, Vector3 moveDirection, float maxDistance, float radius, LayerMask wallMask) {
            if (maxDistance <= 0f) {
                return 0f;
            }

            float safeDist = maxDistance;

            if (context.Rigidbody.TryGetComponent<CapsuleCollider>(out var cap)) {
                var transform = cap.transform;
                var startCenter = startPos + transform.TransformVector(cap.center);

                var axisLocal = GetCapsuleAxisLocalDirection(cap.direction);
                var axisWorld = transform.TransformDirection(axisLocal).normalized;

                var axisScale = transform.TransformVector(axisLocal).magnitude;
                var perpScaleA = transform.TransformVector(GetFirstPerpendicularAxis(axisLocal)).magnitude;
                var perpScaleB = transform.TransformVector(GetSecondPerpendicularAxis(axisLocal)).magnitude;

                var scaledRadius = cap.radius * Mathf.Max(perpScaleA, perpScaleB);
                var scaledHeight = cap.height * axisScale;
                var scaledHalfSegment = Mathf.Max(0f, scaledHeight * 0.5f - scaledRadius);

                Vector3 p1 = startCenter + axisWorld * scaledHalfSegment;
                Vector3 p2 = startCenter - axisWorld * scaledHalfSegment;

                if (Physics.CapsuleCast(p1, p2, scaledRadius, moveDirection, out var hit, maxDistance, wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance - HIT_MARGIN;
                }
            }
            else {
                Vector3 castStart = startPos + Vector3.up * radius;
                if (Physics.SphereCast(castStart, radius, moveDirection, out var hit, maxDistance, wallMask, QueryTriggerInteraction.Ignore)) {
                    safeDist = hit.distance - HIT_MARGIN;
                }
            }

            return Mathf.Max(0f, safeDist);
        }

        static Vector3 GetCapsuleAxisLocalDirection(int direction) {
            if (direction == 0) {
                return Vector3.right;
            }

            if (direction == 2) {
                return Vector3.forward;
            }

            return Vector3.up;
        }

        static Vector3 GetFirstPerpendicularAxis(Vector3 axisLocal) {
            if (axisLocal == Vector3.right) {
                return Vector3.up;
            }

            return Vector3.right;
        }

        static Vector3 GetSecondPerpendicularAxis(Vector3 axisLocal) {
            if (axisLocal == Vector3.forward) {
                return Vector3.up;
            }

            return Vector3.forward;
        }

        static float ClampDistanceByOpponent(Vector3 startPos, Vector3 moveDirection, float desiredDistance, Vector3 opponentPos, float minDistance) {
            if (minDistance <= 0f || desiredDistance <= 0f) {
                return Mathf.Max(0f, desiredDistance);
            }

            var minDistanceSq = minDistance * minDistance;
            var endPos = startPos + moveDirection * desiredDistance;
            if ((endPos - opponentPos).sqrMagnitude >= minDistanceSq) {
                return desiredDistance;
            }

            var startToOpponent = startPos - opponentPos;
            var startDistanceSq = startToOpponent.sqrMagnitude;
            if (startDistanceSq <= minDistanceSq) {
                return 0f;
            }

            var b = 2f * Vector3.Dot(startToOpponent, moveDirection);
            var c = startDistanceSq - minDistanceSq;
            var discriminant = b * b - 4f * c;
            if (discriminant <= 0f) {
                return 0f;
            }

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            var t1 = (-b - sqrtDiscriminant) * 0.5f;
            var t2 = (-b + sqrtDiscriminant) * 0.5f;
            var enter = Mathf.Min(t1, t2);
            var exit = Mathf.Max(t1, t2);

            if (desiredDistance <= enter || desiredDistance >= exit) {
                return desiredDistance;
            }

            return Mathf.Max(0f, enter - OPPONENT_MARGIN);
        }
    }
}