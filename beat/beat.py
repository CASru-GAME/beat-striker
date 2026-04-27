#!/usr/bin/env python3

import sys
from types import ModuleType
import collections
import collections.abc
import numpy as np

# =========================================================
# 古いライブラリ(madmom)を現代の環境で動かすための互換性パッチ
# =========================================================

# 1. 現代Python(3.10以降)の collections 互換パッチ
collections.MutableSequence = collections.abc.MutableSequence
collections.Sequence = collections.abc.Sequence
collections.Iterable = collections.abc.Iterable
collections.Mapping = collections.abc.Mapping
collections.MutableMapping = collections.abc.MutableMapping

# 2. 廃止された pkg_resources の偽装 (バージョン確認回避用)
try:
    import pkg_resources
except ImportError:
    fake_pkg = ModuleType("pkg_resources")
    class DummyDist:
        version = "0.16.1"
    fake_pkg.get_distribution = lambda name: DummyDist()
    sys.modules["pkg_resources"] = fake_pkg

# 3. 最新NumPy(1.24以降)の廃止されたエイリアスを復活させるパッチ
if not hasattr(np, 'float'):
    np.float = float
if not hasattr(np, 'int'):
    np.int = int
if not hasattr(np, 'bool'):
    np.bool = bool
if not hasattr(np, 'complex'):
    np.complex = complex
if not hasattr(np, 'object'):
    np.object = object

# =========================================================

import shlex
from pathlib import Path
import glob

# ここでようやくmadmomを読み込む
from madmom.features.beats import RNNBeatProcessor, DBNBeatTrackingProcessor


DEFAULT_EXTENSIONS = {
    ".wav",
    ".mp3",
    ".flac",
    ".ogg",
    ".m4a",
    ".aiff",
    ".aif",
}


def expand_inputs(inputs, recursive=False, extensions=None):
    files = []

    if extensions is None:
        extensions = DEFAULT_EXTENSIONS
    else:
        extensions = {ext.lower() if ext.startswith(".") else f".{ext.lower()}" for ext in extensions}

    for item in inputs:
        item = str(Path(item).expanduser())

        # glob対応
        matched = glob.glob(item, recursive=recursive)

        if matched:
            paths = [Path(p) for p in matched]
        else:
            paths = [Path(item)]

        for path in paths:
            if path.is_file():
                if path.suffix.lower() in extensions:
                    files.append(path)
            elif path.is_dir():
                pattern = "**/*" if recursive else "*"
                for child in path.glob(pattern):
                    if child.is_file() and child.suffix.lower() in extensions:
                        files.append(child)

    # 重複削除しつつ順序維持
    seen = set()
    unique_files = []
    for f in files:
        resolved = f.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique_files.append(f)

    return unique_files


def make_output_path(audio_path, out_dir=None, suffix=".beats.txt"):
    audio_path = Path(audio_path)

    if out_dir is None:
        return audio_path.with_name(audio_path.stem + suffix)

    out_dir = Path(out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    return out_dir / f"{audio_path.stem}{suffix}"


def detect_beats(
    audio_path,
    fps=100,
    min_bpm=100,
    max_bpm=155,
):
    # RNNでビートらしさを推定
    beat_activation_processor = RNNBeatProcessor()
    activations = beat_activation_processor(str(audio_path))

    # DBNでビート列に変換
    beat_tracker = DBNBeatTrackingProcessor(
        fps=fps,
        min_bpm=min_bpm,
        max_bpm=max_bpm,
    )

    beats = beat_tracker(activations)

    return beats


def save_beats(beats, output_path, with_header=False, source_file=None):
    output_path = Path(output_path)

    if with_header:
        header = "beat_time_seconds"
        if source_file is not None:
            header += f"\nsource={source_file}"
        np.savetxt(output_path, beats, fmt="%.6f", header=header, comments="# ")
    else:
        np.savetxt(output_path, beats, fmt="%.6f")


def get_interactive_args():
    """ユーザーからの対話的な入力を受け取って設定オブジェクトを返す"""
    class Args: pass
    args = Args()

    print("=== Beat Detection 設定 ===")

    # 入力ファイル/ディレクトリ (必須)
    while True:
        in_str = input("対象の音声ファイル/ディレクトリ/パターンを入力 (複数ある場合はスペース区切り。パスにスペースを含む場合は引用符で囲む):\n> ").strip()
        if in_str:
            args.inputs = shlex.split(in_str)
            break
        print("※エラー: 少なくとも1つの入力が必要です。")

    # 出力先ディレクトリ
    out_str = input("\n出力先ディレクトリを入力 (空文字で元のファイルと同じ場所):\n> ").strip()
    args.out_dir = out_str if out_str else None

    while True:
        try:
            min_bpm = input("\n最小BPM [デフォルト: 100]:\n> ").strip()
            args.min_bpm = float(min_bpm) if min_bpm else 100.0
            break
        except ValueError:
            print("※数値を入力してください。")

    while True:
        try:
            max_bpm = input("\n最大BPM [デフォルト: 155]:\n> ").strip()
            args.max_bpm = float(max_bpm) if max_bpm else 155.0
            break
        except ValueError:
            print("※数値を入力してください。")

    # 依頼に合わせて設定項目を固定
    args.fps = 100
    args.suffix = ".beats.txt"
    args.ext = None
    args.recursive = True
    args.overwrite = True
    args.header = False

    print("===========================\n")
    return args


def main():
    # コマンドライン引数の代わりにインタラクティブなプロンプトを使用
    args = get_interactive_args()

    audio_files = expand_inputs(
        args.inputs,
        recursive=args.recursive,
        extensions=args.ext,
    )

    if not audio_files:
        print("対象となる音声ファイルが見つかりませんでした。", file=sys.stderr)
        sys.exit(1)

    print(f"{len(audio_files)} 件の音声ファイルが見つかりました。処理を開始します...")

    failed = []

    for index, audio_path in enumerate(audio_files, start=1):
        output_path = make_output_path(
            audio_path,
            out_dir=args.out_dir,
            suffix=args.suffix,
        )

        print(f"[{index}/{len(audio_files)}] {audio_path}")

        if output_path.exists() and not args.overwrite:
            print(f"  Skip: 既に存在するためスキップしました: {output_path}")
            continue

        try:
            beats = detect_beats(
                audio_path,
                fps=args.fps,
                min_bpm=args.min_bpm,
                max_bpm=args.max_bpm,
            )

            save_beats(
                beats,
                output_path,
                with_header=args.header,
                source_file=audio_path,
            )

            print(f"  Saved: {output_path}")
            print(f"  Beats: {len(beats)}")

        except Exception as e:
            print(f"  Failed: {audio_path}", file=sys.stderr)
            print(f"  Error: {e}", file=sys.stderr)
            failed.append(audio_path)

    if failed:
        print("\n以下のファイルは処理に失敗しました:", file=sys.stderr)
        for f in failed:
            print(f"  {f}", file=sys.stderr)
        sys.exit(2)

    print("\nすべての処理が完了しました。")


if __name__ == "__main__":
    main()