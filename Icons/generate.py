"""Regenerate the menu ICO files from their editable SVG sources.

Requires Pillow and resvg_py (development tools only):
    python -m pip install Pillow resvg_py==0.5.0
"""

from io import BytesIO
from pathlib import Path

from PIL import Image
from resvg_py import svg_to_bytes


ICON_NAMES = ("zip-version", "copy-file-path", "inspect-locks")
SIZES = (16, 24, 32, 48, 256)
ICON_DIR = Path(__file__).resolve().parent


for name in ICON_NAMES:
    source = ICON_DIR / f"{name}.svg"
    frames = []
    for size in SIZES:
        png = svg_to_bytes(svg_path=str(source), width=size, height=size)
        with Image.open(BytesIO(png)) as image:
            frames.append(image.convert("RGBA"))

    destination = ICON_DIR / f"{name}.ico"
    frames[-1].save(
        destination,
        format="ICO",
        sizes=[(size, size) for size in SIZES],
        append_images=frames[:-1],
    )
    print(destination)
