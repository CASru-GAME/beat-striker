/**
 * 白透過変換 共通モジュール
 * 画像のピクセルデータに対して白透過処理を適用する。
 * - 境界の平均輝度を基準にアルファを計算
 * - 全ピクセルを白(RGB=255)に変換し、輝度に応じたアルファ値を設定
 */

/**
 * ImageDataに対して白透過処理を適用する（インプレース変更）
 * @param {ImageData} imageData - 処理対象のImageData
 * @param {number} thresholdAdjustment - 背景レベル調整値（デフォルト: 0）
 */
function applyWhiteTransparency(imageData, thresholdAdjustment = 0) {
  const data = imageData.data;
  const width = imageData.width;
  const height = imageData.height;

  // Pass 1: Global Max LumaとBorder Average Lumaを算出
  let borderLumaSum = 0;
  let borderPixelCount = 0;
  let globalMaxLuma = 0;

  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;

      const a = data[i + 3];
      const r = data[i];
      const g = data[i + 1];
      const b = data[i + 2];
      const luma = (0.299 * r + 0.587 * g + 0.114 * b) * (a / 255);

      if (luma > globalMaxLuma) globalMaxLuma = luma;

      if (x === 0 || x === width - 1 || y === 0 || y === height - 1) {
        borderLumaSum += luma;
        borderPixelCount++;
      }
    }
  }

  const borderAvgLuma = borderPixelCount > 0 ? borderLumaSum / borderPixelCount : 0;
  const effectiveThreshold = Math.max(0, borderAvgLuma + thresholdAdjustment);

  // Pass 2: リマッピング適用
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const a = data[i + 3];

    const luma = (0.299 * r + 0.587 * g + 0.114 * b) * (a / 255);

    let newAlpha = 0;

    if (luma <= effectiveThreshold) {
      newAlpha = 0;
    } else {
      if (globalMaxLuma > effectiveThreshold) {
        newAlpha = (globalMaxLuma * (luma - effectiveThreshold)) / (globalMaxLuma - effectiveThreshold);
      } else {
        newAlpha = 0;
      }
    }

    // 白一色 + 計算されたアルファ
    data[i] = 255;
    data[i + 1] = 255;
    data[i + 2] = 255;
    data[i + 3] = newAlpha;
  }
}
