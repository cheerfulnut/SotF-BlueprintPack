"""Builds a blueprint book page image from an exported design, matching the game's page layout.

    python blender/make_page.py <build base path> <Blueprints folder>

Reads <base>.json, <base>-render.png and <base>-icon-<item>.png (from export_design.py) and writes the layout and
<name>-page.png into the Blueprints folder, which is what gets installed. Needs Pillow. Everything drawn here is our
own work: the layout was measured from the game's pages (1024 x 1024; only the left 672 px are shown in the book),
but no game artwork is copied. The title, blueprint name and tab labels are drawn by the book itself.
"""
import json
import os
import shutil
import sys

from PIL import Image, ImageDraw, ImageFont

BACKGROUND = "#d8c6b7"
SLOT = "#c3b3a5"
COUNT_TEXT = "#5b4e44"
CATEGORY = {"Shelters": "#708649"}
# The bottom bars name the previous and next categories; our pages sit between Shelter and Tree Shelter.
PREVIOUS_BAR, NEXT_BAR = "#708649", "#b47f41"

HEADER = (0, 0, 671, 124)
TOP_SLOT = (88, 210, 345, 496)
ICON_X, ICON_SIZE, ROW_SPACING = 372, 70, 80
PREVIOUS_BUTTON = (72, 923, 369, 1002)
NEXT_BUTTON = (378, 923, 674, 1002)

ITEM_ORDER = ["Log", "HalfLog", "QuarterLog", "Plank", "Stone", "Rope"]


def font(size):
    for name in ("bahnschrift.ttf", "arial.ttf"):
        try:
            return ImageFont.truetype(os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name), size)
        except OSError:
            continue
    return ImageFont.load_default()


def clean_alpha(image, threshold=40):
    """The invisible ground leaves a faint haze everywhere; keep only the model and its real shadow."""
    alpha = image.getchannel("A").point(lambda a: 0 if a < threshold else a)
    image.putalpha(alpha)
    return image


def fit(image, box, padding):
    """Trims transparent margins and scales the image to fit inside box, centred."""
    image = image.crop(image.getchannel("A").getbbox())
    w, h = box[2] - box[0] - 2 * padding, box[3] - box[1] - 2 * padding
    scale = min(w / image.width, h / image.height)
    image = image.resize((max(1, int(image.width * scale)), max(1, int(image.height * scale))), Image.LANCZOS)
    x = box[0] + (box[2] - box[0] - image.width) // 2
    y = box[1] + (box[3] - box[1] - image.height) // 2
    return image, (x, y)


def main():
    base, out_dir = sys.argv[1], sys.argv[2]
    with open(base + ".json", encoding="utf-8") as f:
        layout = json.load(f)
    os.makedirs(out_dir, exist_ok=True)

    page = Image.new("RGBA", (1024, 1024), BACKGROUND)
    draw = ImageDraw.Draw(page)
    draw.rectangle(HEADER, fill=CATEGORY.get(layout["category"], "#708649"))
    draw.rounded_rectangle(TOP_SLOT, radius=18, fill=SLOT)
    draw.rounded_rectangle(PREVIOUS_BUTTON, radius=8, fill=PREVIOUS_BAR)
    draw.rounded_rectangle(NEXT_BUTTON, radius=8, fill=NEXT_BAR)

    render, pos = fit(clean_alpha(Image.open(base + "-render.png").convert("RGBA")), TOP_SLOT, 12)
    page.alpha_composite(render, pos)

    items = sorted(layout["ingredients"].items(), key=lambda kv: ITEM_ORDER.index(kv[0]) if kv[0] in ITEM_ORDER else 99)
    rows = len(items)
    first_centre = (TOP_SLOT[1] + TOP_SLOT[3]) // 2 - (rows - 1) * ROW_SPACING // 2
    text_font = font(34)
    for i, (item, count) in enumerate(items):
        centre_y = first_centre + i * ROW_SPACING
        box = (ICON_X, centre_y - ICON_SIZE // 2, ICON_X + ICON_SIZE, centre_y + ICON_SIZE // 2)
        icon, icon_pos = fit(Image.open(f"{base}-icon-{item}.png").convert("RGBA"), box, 2)
        page.alpha_composite(icon, icon_pos)
        draw.text((ICON_X + ICON_SIZE + 12, centre_y), f"x{count}", font=text_font, fill=COUNT_TEXT, anchor="lm")

    page_path = os.path.join(out_dir, layout["page"])
    page.convert("RGB").save(page_path)
    shutil.copyfile(base + ".json", os.path.join(out_dir, os.path.basename(base) + ".json"))
    print(f"HB: wrote {page_path} and the layout")


if __name__ == "__main__":
    main()
