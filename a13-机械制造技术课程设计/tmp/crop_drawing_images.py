from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageChops


def crop_to_content(source: Path, target: Path, margin: int = 30) -> None:
    image = Image.open(source).convert("RGB")
    white = Image.new("RGB", image.size, "white")
    bbox = ImageChops.difference(image, white).getbbox()
    if bbox is None:
        raise ValueError(f"No non-white content found in {source}")
    left, top, right, bottom = bbox
    left = max(0, left - margin)
    top = max(0, top - margin)
    right = min(image.width, right + margin)
    bottom = min(image.height, bottom + margin)
    target.parent.mkdir(parents=True, exist_ok=True)
    image.crop((left, top, right, bottom)).save(target, optimize=True)


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("source", type=Path)
    parser.add_argument("target", type=Path)
    parser.add_argument("--margin", type=int, default=30)
    args = parser.parse_args()
    crop_to_content(args.source, args.target, args.margin)


if __name__ == "__main__":
    main()
