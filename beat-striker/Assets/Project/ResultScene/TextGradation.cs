using System;
using System.Collections.Generic;
using System.Linq;
using Extensions;
using TMPro;
using UnityEngine;

namespace TMPGradation
{
    [ExecuteAlways]
    public class TMPGradation : MonoBehaviour
    {
        private const int GradientNum = 4;

        private TMP_Text textMeshProText;
        private bool isChange;

        private void OnEnable()
        {
            if (textMeshProText == null)
            {
                textMeshProText = GetComponent<TMP_Text>();
            }

            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(TMPChangeEvent);
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(TMPChangeEvent);
        }

        private void TMPChangeEvent(object obj)
        {
            if ((TMP_Text)obj != textMeshProText)
            {
                return;
            }

            isChange = true;
        }

        private void Update()
        {
            if (!isChange)
            {
                return;
            }

            isChange = false;
            UpdateGradient();
        }

        private void UpdateGradient()
        {
            if (!textMeshProText.enableVertexGradient)
            {
                return;
            }

            var colorMode = GetColorMode();

            if (colorMode is ColorMode.Single or ColorMode.VerticalGradient)
            {
                return;
            }

            // 通常の処理時間の前に、テキストの再生成を強制する関数
            textMeshProText.ForceMeshUpdate();

            var textInfo = textMeshProText.textInfo;
            var characterCount = textInfo.characterCount;

            var gradients = GetVertexGradients(textMeshProText.colorGradient, characterCount, colorMode);

            for (var i = 0; i < characterCount; i++)
            {
                var materialIndex = textInfo.characterInfo[i].materialReferenceIndex;
                var colors = textInfo.meshInfo[materialIndex].colors32;
                var vertexIndex = textInfo.characterInfo[i].vertexIndex;

                if (!textInfo.characterInfo[i].isVisible)
                {
                    continue;
                }

                colors[vertexIndex] = gradients[i].bottomLeft;
                colors[vertexIndex + 1] = gradients[i].topLeft;
                colors[vertexIndex + 2] = gradients[i].bottomRight;
                colors[vertexIndex + 3] = gradients[i].topRight;
            }

            // 変更した頂点の更新
            textMeshProText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }

        private ColorMode GetColorMode()
        {
            return textMeshProText.colorGradient switch
            {
                { topLeft: var topLeft, topRight: var topRight, bottomLeft: var bottomLeft, bottomRight: var bottomRight }
                    when topLeft == topRight && topLeft == bottomLeft && topLeft == bottomRight => ColorMode.Single,
                { topLeft: var topLeft, topRight: var topRight, bottomLeft: var bottomLeft, bottomRight: var bottomRight }
                    when topLeft == bottomLeft && topRight == bottomRight => ColorMode.HorizontalGradient,
                { topLeft: var topLeft, topRight: var topRight, bottomLeft: var bottomLeft, bottomRight: var bottomRight }
                    when topLeft == topRight && bottomLeft == bottomRight => ColorMode.VerticalGradient,
                _ => ColorMode.FourCornersGradient
            };
        }

        private VertexGradient[] GetVertexGradients(VertexGradient vertexGradient, int characterCount, ColorMode colorMode)
        {
            var vertexColors = colorMode switch
            {
                ColorMode.HorizontalGradient => GetHorizontalColors(vertexGradient, characterCount),
                ColorMode.FourCornersGradient => GetFourCornersColors(vertexGradient, characterCount),
                _ => throw new ArgumentOutOfRangeException(nameof(colorMode), colorMode, null)
            };

            var gradients = vertexColors.Chunk(GradientNum).Select(x =>
            {
                var colors = x.ToArray();
                return new VertexGradient(colors[0], colors[1], colors[2], colors[3]);
            });

            return gradients.ToArray();
        }

        private IReadOnlyCollection<Color> GetHorizontalColors(VertexGradient vertexGradient, int characterCount)
        {
            var topLeft = vertexGradient.topLeft;
            var topRight = vertexGradient.topRight;
            var topLeftRatio = (topRight - topLeft) / characterCount;
            var colors = new List<Color>();

            for (var i = 0; i < characterCount; i++)
            {
                colors.Add(topLeft + topLeftRatio * i);
                colors.Add(topLeft + topLeftRatio * (i + 1));
                colors.Add(topLeft + topLeftRatio * i);
                colors.Add(topLeft + topLeftRatio * (i + 1));
            }

            return colors;
        }

        private IReadOnlyCollection<Color> GetFourCornersColors(VertexGradient vertexGradient, int characterCount)
        {
            var step = characterCount * GradientNum;

            var topLeft = vertexGradient.topLeft;
            var topRight = vertexGradient.topRight;
            var bottomLeft = vertexGradient.bottomLeft;
            var bottomRight = vertexGradient.bottomRight;

            var topLeftRatio = (topRight - topLeft) / step;
            var bottomLeftRatio = (bottomRight - bottomLeft) / step;

            var colors = new List<Color>();

            for (var i = 0; i < step; i += GradientNum)
            {
                colors.Add(topLeft + topLeftRatio * i);
                colors.Add(bottomLeft + bottomLeftRatio * (i + 1));
                colors.Add(bottomLeft + bottomLeftRatio * (i + 2));
                colors.Add(topLeft + topLeftRatio * (i + 3));
            }

            return colors;
        }
    }
}

