using System;
using System.IO;
using System.Numerics;
using UnityEditor;
using UnityEngine;

public static class AudioSpectrumBakedDataGenerator {
    private const uint MAGIC = 0x33425341; // ASB3 (raw packed, no deflate)
    private const byte QUANTIZE_BITS = 4;
    private const int FFT_SIZE = 512;
    private const int FRAME_RATE = 60;

    [MenuItem("Assets/スペクトラムを生成", false, 2100)]
    private static void GenerateBakedData() {
        AudioClip clip = Selection.activeObject as AudioClip;
        string clipPath = AssetDatabase.GetAssetPath(clip);

        float[] monoSamples = GetMonoSamples(clip);
        int hopSize = Mathf.Max(1, clip.frequency / FRAME_RATE);
        int frameCount = Mathf.Max(1, Mathf.CeilToInt((float)monoSamples.Length / hopSize));
        float[] flattenedSpectrum = new float[frameCount * FFT_SIZE];

        float[] frameBuffer = new float[FFT_SIZE];
        Complex[] fftBuffer = new Complex[FFT_SIZE];

        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {
            int sampleStart = frameIndex * hopSize;
            FillFrameBuffer(monoSamples, sampleStart, frameBuffer);
            ApplyHannWindow(frameBuffer);
            PerformFft(frameBuffer, fftBuffer);

            int offset = frameIndex * FFT_SIZE;
            for (int bin = 0; bin < FFT_SIZE; bin++) {
                flattenedSpectrum[offset + bin] = (float)fftBuffer[bin].Magnitude;
            }
        }

        string directory = Path.GetDirectoryName(clipPath);
        string assetName = $"{clip.name}_AudioSpectrum.bytes";
        string rawPath = Path.Combine(directory, assetName);
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(rawPath.Replace("\\", "/"));

        byte[] payload = BuildBinaryPayload(FFT_SIZE, FRAME_RATE, frameCount, flattenedSpectrum);
        File.WriteAllBytes(assetPath, payload);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        TextAsset bakedText = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
        Selection.activeObject = bakedText;
        EditorGUIUtility.PingObject(bakedText);
    }

    [MenuItem("Assets/スペクトラムを生成", true)]
    private static bool ValidateGenerateBakedData() {
        return Selection.activeObject is AudioClip;
    }

    private static float[] GetMonoSamples(AudioClip clip) {
        int sampleCount = clip.samples;
        int channels = clip.channels;
        float[] allSamples = new float[sampleCount * channels];
        clip.GetData(allSamples, 0);

        float[] mono = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
            float sum = 0f;
            int baseIndex = i * channels;
            for (int ch = 0; ch < channels; ch++) {
                sum += allSamples[baseIndex + ch];
            }
            mono[i] = sum / channels;
        }

        return mono;
    }

    private static void FillFrameBuffer(float[] monoSamples, int sampleStart, float[] frameBuffer) {
        for (int i = 0; i < frameBuffer.Length; i++) {
            int sampleIndex = sampleStart + i;
            frameBuffer[i] = sampleIndex < monoSamples.Length ? monoSamples[sampleIndex] : 0f;
        }
    }

    private static void ApplyHannWindow(float[] frameBuffer) {
        int n = frameBuffer.Length;
        for (int i = 0; i < n; i++) {
            float w = 0.5f * (1f - Mathf.Cos((2f * Mathf.PI * i) / (n - 1f)));
            frameBuffer[i] *= w;
        }
    }

    private static void PerformFft(float[] input, Complex[] output) {
        int n = input.Length;
        for (int i = 0; i < n; i++) {
            output[i] = new Complex(input[i], 0.0);
        }

        int j = 0;
        for (int i = 1; i < n; i++) {
            int bit = n >> 1;
            while ((j & bit) != 0) {
                j ^= bit;
                bit >>= 1;
            }
            j |= bit;

            if (i < j) {
                Complex temp = output[i];
                output[i] = output[j];
                output[j] = temp;
            }
        }

        for (int len = 2; len <= n; len <<= 1) {
            double angle = -2.0 * Math.PI / len;
            Complex wLen = new Complex(Math.Cos(angle), Math.Sin(angle));

            for (int i = 0; i < n; i += len) {
                Complex w = Complex.One;
                int halfLen = len >> 1;
                for (int k = 0; k < halfLen; k++) {
                    Complex u = output[i + k];
                    Complex v = output[i + k + halfLen] * w;
                    output[i + k] = u + v;
                    output[i + k + halfLen] = u - v;
                    w *= wLen;
                }
            }
        }
    }

    private static byte[] BuildBinaryPayload(int fftSize, int frameRate, int frameCount, float[] flattenedSpectrum) {
        float maxMagnitude = 0f;
        for (int i = 0; i < flattenedSpectrum.Length; i++) {
            if (flattenedSpectrum[i] > maxMagnitude) {
                maxMagnitude = flattenedSpectrum[i];
            }
        }

        if (maxMagnitude <= 0f) {
            maxMagnitude = 1f;
        }

        byte[] packedQuantized = Pack4BitQuantized(flattenedSpectrum, maxMagnitude);

        using (MemoryStream stream = new MemoryStream(29 + packedQuantized.Length)) {
            using (BinaryWriter writer = new BinaryWriter(stream)) {
                writer.Write(MAGIC);
                writer.Write(fftSize);
                writer.Write(frameRate);
                writer.Write(frameCount);
                writer.Write(maxMagnitude);
                writer.Write(QUANTIZE_BITS);
                writer.Write(packedQuantized.Length);
                writer.Write(packedQuantized.Length);
                writer.Write(packedQuantized);
            }

            return stream.ToArray();
        }
    }

    private static byte[] Pack4BitQuantized(float[] flattenedSpectrum, float maxMagnitude) {
        int packedLength = (flattenedSpectrum.Length + 1) / 2;
        byte[] packed = new byte[packedLength];

        for (int i = 0; i < flattenedSpectrum.Length; i++) {
            float normalized = Mathf.Clamp01(flattenedSpectrum[i] / maxMagnitude);
            float compressed = Mathf.Sqrt(normalized);
            int q4 = Mathf.Clamp(Mathf.RoundToInt(compressed * 15f), 0, 15);

            int byteIndex = i >> 1;
            if ((i & 1) == 0) {
                packed[byteIndex] = (byte)(q4 << 4);
            }
            else {
                packed[byteIndex] |= (byte)q4;
            }
        }

        return packed;
    }
}
