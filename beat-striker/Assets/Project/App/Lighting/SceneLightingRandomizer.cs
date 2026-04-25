using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Alice {
    [AddComponentMenu("Alice/Lighting/Scene Lighting Randomizer")]
    public class SceneLightingRandomizer : MonoBehaviour {
        [Serializable]
        class LightingPreset {
            [SerializeField] string name;

            [Header("Environment")]
            [SerializeField] Material skyboxMaterial;
            [SerializeField] Light sunSource;
            [SerializeField] Color shadowColor = Color.black;

            [Header("Sun Light")]
            [SerializeField] bool sunLightEnabled = true;
            [SerializeField] LightType sunLightType = LightType.Directional;
            [SerializeField] Color sunLightColor = Color.white;
            [SerializeField, Min(0f)] float sunLightIntensity = 1f;
            [SerializeField, Min(0f)] float sunLightBounceIntensity = 1f;
            [SerializeField, Min(0f)] float sunLightRange = 10f;
            [SerializeField, Range(1f, 179f)] float sunLightSpotAngle = 30f;
            [SerializeField] LightShadows sunLightShadows = LightShadows.None;
            [SerializeField, Range(0f, 1f)] float sunLightShadowStrength = 1f;
            [SerializeField] LightShadowResolution sunLightShadowResolution = LightShadowResolution.FromQualitySettings;
            [SerializeField] float sunLightShadowBias = 0.05f;
            [SerializeField] float sunLightShadowNormalBias = 0.4f;
            [SerializeField] float sunLightShadowNearPlane = 0.2f;
            [SerializeField] LightRenderMode sunLightRenderMode = LightRenderMode.Auto;
            [SerializeField] int sunLightCullingMask = -1;
            [SerializeField] Vector3 sunLightPosition;
            [SerializeField] Quaternion sunLightRotation = Quaternion.identity;

            [Header("Ambient Lighting")]
            [SerializeField] AmbientMode ambientMode = AmbientMode.Skybox;
            [SerializeField] Color ambientColor = Color.gray;
            [SerializeField] Color ambientSkyColor = Color.gray;
            [SerializeField] Color ambientEquatorColor = Color.gray;
            [SerializeField] Color ambientGroundColor = Color.gray;
            [SerializeField, Min(0f)] float ambientIntensity = 1f;

            [Header("Reflection")]
            [SerializeField] DefaultReflectionMode reflectionMode = DefaultReflectionMode.Skybox;
            [SerializeField, Min(16)] int reflectionResolution = 128;
            [SerializeField] Texture customReflection;
            [SerializeField, Min(0f)] float reflectionIntensity = 1f;
            [SerializeField, Min(0)] int reflectionBounces = 1;

            [Header("Fog")]
            [SerializeField] bool fog;
            [SerializeField] Color fogColor = Color.gray;
            [SerializeField] FogMode fogMode = FogMode.ExponentialSquared;
            [SerializeField, Min(0f)] float fogDensity = 0.01f;
            [SerializeField] float fogStartDistance;
            [SerializeField, Min(0f)] float fogEndDistance = 300f;

            public override string ToString() {
                return string.IsNullOrEmpty(name) ? nameof(LightingPreset) : name;
            }

            public static LightingPreset CaptureCurrent(string presetName) {
                var currentReflectionMode = RenderSettings.defaultReflectionMode;
                var currentSun = RenderSettings.sun;

                return new LightingPreset {
                    name = presetName,
                    skyboxMaterial = RenderSettings.skybox,
                    sunSource = currentSun,
                    shadowColor = RenderSettings.subtractiveShadowColor,
                    sunLightEnabled = currentSun != null && currentSun.enabled,
                    sunLightType = currentSun != null ? currentSun.type : LightType.Directional,
                    sunLightColor = currentSun != null ? currentSun.color : Color.white,
                    sunLightIntensity = currentSun != null ? currentSun.intensity : 1f,
                    sunLightBounceIntensity = currentSun != null ? currentSun.bounceIntensity : 1f,
                    sunLightRange = currentSun != null ? currentSun.range : 10f,
                    sunLightSpotAngle = currentSun != null ? currentSun.spotAngle : 30f,
                    sunLightShadows = currentSun != null ? currentSun.shadows : LightShadows.None,
                    sunLightShadowStrength = currentSun != null ? currentSun.shadowStrength : 1f,
                    sunLightShadowResolution = currentSun != null ? currentSun.shadowResolution : LightShadowResolution.FromQualitySettings,
                    sunLightShadowBias = currentSun != null ? currentSun.shadowBias : 0.05f,
                    sunLightShadowNormalBias = currentSun != null ? currentSun.shadowNormalBias : 0.4f,
                    sunLightShadowNearPlane = currentSun != null ? currentSun.shadowNearPlane : 0.2f,
                    sunLightRenderMode = currentSun != null ? currentSun.renderMode : LightRenderMode.Auto,
                    sunLightCullingMask = currentSun != null ? currentSun.cullingMask : -1,
                    sunLightPosition = currentSun != null ? currentSun.transform.position : Vector3.zero,
                    sunLightRotation = currentSun != null ? currentSun.transform.rotation : Quaternion.identity,
                    ambientMode = RenderSettings.ambientMode,
                    ambientColor = RenderSettings.ambientLight,
                    ambientSkyColor = RenderSettings.ambientSkyColor,
                    ambientEquatorColor = RenderSettings.ambientEquatorColor,
                    ambientGroundColor = RenderSettings.ambientGroundColor,
                    ambientIntensity = RenderSettings.ambientIntensity,
                    reflectionMode = currentReflectionMode,
                    reflectionResolution = RenderSettings.defaultReflectionResolution,
                    customReflection = currentReflectionMode == DefaultReflectionMode.Custom ? RenderSettings.customReflectionTexture : null,
                    reflectionIntensity = RenderSettings.reflectionIntensity,
                    reflectionBounces = RenderSettings.reflectionBounces,
                    fog = RenderSettings.fog,
                    fogColor = RenderSettings.fogColor,
                    fogMode = RenderSettings.fogMode,
                    fogDensity = RenderSettings.fogDensity,
                    fogStartDistance = RenderSettings.fogStartDistance,
                    fogEndDistance = RenderSettings.fogEndDistance,
                };
            }

            public void Apply() {
                RenderSettings.skybox = skyboxMaterial;
                RenderSettings.sun = sunSource;
                RenderSettings.subtractiveShadowColor = shadowColor;
                ApplySunLightSettings();

                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientColor;
                RenderSettings.ambientSkyColor = ambientSkyColor;
                RenderSettings.ambientEquatorColor = ambientEquatorColor;
                RenderSettings.ambientGroundColor = ambientGroundColor;
                RenderSettings.ambientIntensity = Mathf.Max(0f, ambientIntensity);

                RenderSettings.defaultReflectionMode = reflectionMode;
                RenderSettings.defaultReflectionResolution = Mathf.Max(16, reflectionResolution);
                if (reflectionMode == DefaultReflectionMode.Custom) {
                    RenderSettings.customReflectionTexture = customReflection;
                }
                RenderSettings.reflectionIntensity = Mathf.Max(0f, reflectionIntensity);
                RenderSettings.reflectionBounces = Mathf.Max(0, reflectionBounces);

                RenderSettings.fog = fog;
                RenderSettings.fogColor = fogColor;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogDensity = Mathf.Max(0f, fogDensity);
                RenderSettings.fogStartDistance = fogStartDistance;
                RenderSettings.fogEndDistance = Mathf.Max(fogStartDistance, fogEndDistance);
            }

            void ApplySunLightSettings() {
                if (sunSource == null) {
                    return;
                }

                sunSource.enabled = sunLightEnabled;
                sunSource.type = sunLightType;
                sunSource.color = sunLightColor;
                sunSource.intensity = Mathf.Max(0f, sunLightIntensity);
                sunSource.bounceIntensity = Mathf.Max(0f, sunLightBounceIntensity);
                sunSource.range = Mathf.Max(0f, sunLightRange);
                sunSource.spotAngle = Mathf.Clamp(sunLightSpotAngle, 1f, 179f);
                sunSource.shadows = sunLightShadows;
                sunSource.shadowStrength = Mathf.Clamp01(sunLightShadowStrength);
                sunSource.shadowResolution = sunLightShadowResolution;
                sunSource.shadowBias = sunLightShadowBias;
                sunSource.shadowNormalBias = sunLightShadowNormalBias;
                sunSource.shadowNearPlane = sunLightShadowNearPlane;
                sunSource.renderMode = sunLightRenderMode;
                sunSource.cullingMask = sunLightCullingMask;
                sunSource.transform.SetPositionAndRotation(sunLightPosition, sunLightRotation);
            }
        }

        [SerializeField] bool applyOnStart = true;
        [SerializeField] List<LightingPreset> presets = new();

        // ReSharper disable once UnusedMember.Local
        void Start() {
            if (!applyOnStart) {
                return;
            }

            ApplyRandomPreset();
        }

        public void ApplyRandomPreset() {
            if (presets.Count == 0) {
                Debug.LogWarning($"{nameof(SceneLightingRandomizer)} has no lighting presets.", this);
                return;
            }

            ApplyPreset(UnityEngine.Random.Range(0, presets.Count));
        }

        public void AddCurrentSceneLightingAsPreset(string presetName) {
            presets.Add(LightingPreset.CaptureCurrent(presetName));
        }

        public void ApplyPreset(int presetIndex) {
            if (presetIndex < 0 || presetIndex >= presets.Count) {
                Debug.LogWarning($"{nameof(SceneLightingRandomizer)} preset index is out of range: {presetIndex}", this);
                return;
            }

            presets[presetIndex].Apply();
        }
    }
}
