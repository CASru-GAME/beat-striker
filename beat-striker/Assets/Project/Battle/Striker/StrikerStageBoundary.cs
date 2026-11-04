using UnityEngine;

/// <summary>Keeps the striker's capsule inside the stage even after direct position changes.</summary>
public sealed class StrikerStageBoundary {
    readonly Rigidbody body;
    readonly CapsuleCollider capsule;
    readonly Bounds bounds;
    readonly float deployWorldZ;

    public Bounds MovementBounds => bounds;

    // Collider.bounds is empty while the warrior's dodge disables its collider.
    // Calculate from the capsule and physics pose so observations stay valid then.
    public Bounds BodyBounds {
        get {
            var scale = capsule.transform.lossyScale;
            var absoluteScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            var axis = capsule.direction == 0 ? Vector3.right : capsule.direction == 2 ? Vector3.forward : Vector3.up;
            var axisScale = capsule.direction == 0 ? absoluteScale.x : capsule.direction == 2 ? absoluteScale.z : absoluteScale.y;
            var radiusScale = capsule.direction == 0 ? Mathf.Max(absoluteScale.y, absoluteScale.z)
                : capsule.direction == 2 ? Mathf.Max(absoluteScale.x, absoluteScale.y) : Mathf.Max(absoluteScale.x, absoluteScale.z);
            var radius = capsule.radius * radiusScale;
            var halfSegment = Mathf.Max(0f, capsule.height * axisScale * 0.5f - radius);
            var worldAxis = body.rotation * axis;
            var extents = new Vector3(Mathf.Abs(worldAxis.x), Mathf.Abs(worldAxis.y), Mathf.Abs(worldAxis.z)) * halfSegment
                + Vector3.one * radius;
            var centerOffset = body.rotation * Vector3.Scale(capsule.center, scale);
            return new Bounds(body.position + centerOffset, extents * 2f);
        }
    }

    public StrikerStageBoundary(Rigidbody body, CapsuleCollider capsule, Bounds bounds) {
        this.body = body;
        this.capsule = capsule;
        this.bounds = bounds;
        deployWorldZ = body.position.z;
    }

    public void Constrain() {
        // Compute from the physics pose: Collider.bounds can still reflect the old
        // transform immediately after a warp or an online position correction.
        var geometry = BodyBounds;
        var extents = geometry.extents;
        var centerOffset = geometry.center - body.position;
        var min = bounds.min + extents - centerOffset;
        var max = bounds.max - extents - centerOffset;
        var position = body.position;
        var constrained = new Vector3(
            Mathf.Clamp(position.x, min.x, max.x),
            Mathf.Clamp(position.y, min.y, max.y),
            Mathf.Clamp(deployWorldZ, min.z, max.z));

        if (!position.Equals(constrained)) {
            body.position = constrained;
        }

        if (body.isKinematic) {
            return;
        }

        var velocity = body.linearVelocity;
        if ((constrained.x <= min.x && velocity.x < 0f) || (constrained.x >= max.x && velocity.x > 0f)) {
            velocity.x = 0f;
        }
        if ((constrained.y <= min.y && velocity.y < 0f) || (constrained.y >= max.y && velocity.y > 0f)) {
            velocity.y = 0f;
        }
        velocity.z = 0f;
        body.linearVelocity = velocity;
    }
}
