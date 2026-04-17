using System.Collections.Generic;
using UnityEngine;

public static class ParticleTextureAnimationSetup {
    private static readonly AnimationCurve CustomDataLinearCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public static void Apply(ParticleSystem particleSystem) {
        var particleRenderer = particleSystem.GetComponent<ParticleSystemRenderer>();
        var vertexStreams = new List<ParticleSystemVertexStream>();
        particleRenderer.GetActiveVertexStreams(vertexStreams);

        // Ensure UV1(TEXCOORD1) path is available before writing Custom1.x.
        // Without UV2 stream reservation, Custom1.x may be packed into TEXCOORD0.z.
        vertexStreams.RemoveAll(static stream =>
            stream == ParticleSystemVertexStream.Custom1X ||
            stream == ParticleSystemVertexStream.Custom1XYZW);

        if (!vertexStreams.Contains(ParticleSystemVertexStream.UV2)) {
            vertexStreams.Add(ParticleSystemVertexStream.UV2);
        }

        vertexStreams.Add(ParticleSystemVertexStream.Custom1X);
        particleRenderer.SetActiveVertexStreams(vertexStreams);

        var customData = particleSystem.customData;
        customData.enabled = true;
        customData.SetMode(ParticleSystemCustomData.Custom1, ParticleSystemCustomDataMode.Vector);
        customData.SetVector(ParticleSystemCustomData.Custom1, 0, new ParticleSystem.MinMaxCurve(1f, CustomDataLinearCurve));
    }

    public static void ApplyRecursively(ParticleSystem rootParticleSystem) {
        var particleSystems = rootParticleSystem.GetComponentsInChildren<ParticleSystem>(true);
        for (var i = 0; i < particleSystems.Length; i++) {
            Apply(particleSystems[i]);
        }
    }
}
