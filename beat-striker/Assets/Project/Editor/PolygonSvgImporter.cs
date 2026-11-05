using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Alice.Editor {
    // Flat UI artwork uses filled polygons only. Keep the SVG as the editable source;
    // the texture and Sprite are import sub-assets, never separate exported PNG files.
    [ScriptedImporter(1, "svg")]
    public class PolygonSvgImporter : ScriptedImporter {
        [SerializeField, Range(64, 1024)] int resolution = 256;

        public override void OnImportAsset(AssetImportContext context) {
            var svg = XDocument.Load(context.assetPath).Root;
            var box = Numbers((string)svg.Attribute("viewBox"));
            if (box.Length != 4 || box[2] <= 0 || box[3] <= 0) throw new FormatException("SVG requires a valid viewBox.");
            var width = resolution;
            var height = Mathf.RoundToInt(resolution * box[3] / box[2]);
            const int samples = 2;
            var sampleWidth = width * samples;
            var sampleHeight = height * samples;
            var pixels = new Color32[sampleWidth * sampleHeight];
            foreach (var element in svg.Elements()) {
                if (element.Name.LocalName is "title" or "desc") continue;
                if (element.Name.LocalName != "polygon" || element.Attributes().Any(a => a.Name.LocalName is not ("points" or "fill")))
                    throw new FormatException("UI SVG supports only polygons with points and solid fill. Convert curves/strokes to polygons before importing.");
                var coordinates = Numbers((string)element.Attribute("points"));
                if (coordinates.Length < 6 || coordinates.Length % 2 != 0) throw new FormatException("Invalid polygon points.");
                var points = Enumerable.Range(0, coordinates.Length / 2).Select(i => new Vector2(
                    (coordinates[i * 2] - box[0]) / box[2] * sampleWidth,
                    (coordinates[i * 2 + 1] - box[1]) / box[3] * sampleHeight)).ToArray();
                if (!ColorUtility.TryParseHtmlString((string)element.Attribute("fill"), out var color)) throw new FormatException("Invalid polygon fill.");
                // Scanline fill also supports concave polygons (the musical note and crown).
                var crossings = new System.Collections.Generic.List<float>();
                for (var y = Mathf.Max(0, Mathf.FloorToInt(points.Min(p => p.y))); y < Mathf.Min(sampleHeight, Mathf.CeilToInt(points.Max(p => p.y))); y++) {
                    crossings.Clear();
                    for (int i = 0, j = points.Length - 1; i < points.Length; j = i++) {
                        var a = points[i]; var b = points[j]; var scan = y + .5f;
                        if ((a.y > scan) != (b.y > scan)) crossings.Add(a.x + (scan - a.y) * (b.x - a.x) / (b.y - a.y));
                    }
                    crossings.Sort();
                    for (var i = 0; i + 1 < crossings.Count; i += 2)
                        for (var x = Mathf.Max(0, Mathf.CeilToInt(crossings[i] - .5f)); x < Mathf.Min(sampleWidth, Mathf.CeilToInt(crossings[i + 1] - .5f)); x++)
                            pixels[(sampleHeight - 1 - y) * sampleWidth + x] = color;
                }
            }
            var output = new Color32[width * height];
            for (var y = 0; y < height; y++) for (var x = 0; x < width; x++) {
                var sum = Color.clear;
                for (var dy = 0; dy < samples; dy++) for (var dx = 0; dx < samples; dx++) {
                    Color pixel = pixels[(y * samples + dy) * sampleWidth + x * samples + dx];
                    sum += new Color(pixel.r * pixel.a, pixel.g * pixel.a, pixel.b * pixel.a, pixel.a);
                }
                output[y * width + x] = sum.a > 0 ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a / (samples * samples)) : Color.clear;
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
                name = Path.GetFileNameWithoutExtension(context.assetPath), filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(output); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            context.AddObjectToAsset("texture", texture);
            context.AddObjectToAsset("sprite", sprite);
            context.SetMainObject(sprite);
        }

        static float[] Numbers(string text) => text.Split(new[] { ' ', ',', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
    }
}
