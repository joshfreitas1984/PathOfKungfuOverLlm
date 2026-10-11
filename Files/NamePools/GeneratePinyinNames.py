"""Builds the English-mode random NPC name pool as pinyin.

The game loads WorldEvolution/name2 (Western names: Smith, James...) when WordManager.Open is true and
WorldEvolution/name (Chinese) otherwise; see GDataManager.InitNameData. This script turns the Chinese pool into
pinyin with the same IDs and blanks, so the game picks the same way it does in Chinese mode.

Usage (from the repo root; needs `pip install pypinyin`):
    python Files/NamePools/GeneratePinyinNames.py

The game joins surname and given name with nothing between; PathOfKungfuPlugin/NamePoolPatch.cs adds the space.

Reads  Files/Raw/Dumped/GameData/WorldEvolution__name.csv
Writes Files/NamePools/WorldEvolution__name2.csv  (columns ID,surnam,male,female)
"""
import csv
import re
import sys
from pathlib import Path

from pypinyin import Style, lazy_pinyin

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Files/Raw/Dumped/GameData/WorldEvolution__name.csv"
OUTPUT = ROOT / "Files/NamePools/WorldEvolution__name2.csv"

# Surname readings that differ from the common reading of the character.
SURNAMES = {
    "单": "Shan", "乐": "Yue", "仇": "Qiu", "解": "Xie", "查": "Zha", "缪": "Miao", "都": "Du",
    "曾": "Zeng", "覃": "Qin", "万": "Wan", "盖": "Ge", "那": "Na", "任": "Ren", "贲": "Ben",
    "朴": "Piao", "区": "Ou", "褚": "Chu", "尉": "Yu", "晁": "Chao", "宿": "Su", "华": "Hua",
}

# The glossary writes u-umlaut as yu after l/n (Lyu), as in Character-Names.
UMLAUT = [(r"^lv", "lyu"), (r"^nv", "nyu")]


def syllables(text: str, overrides: dict[str, str] | None = None) -> list[str]:
    if overrides and text in overrides:
        return [overrides[text].lower()]
    result = []
    for syllable in lazy_pinyin(text, style=Style.NORMAL, v_to_u=False, errors="ignore"):
        for pattern, replacement in UMLAUT:
            syllable = re.sub(pattern, replacement, syllable)
        result.append(syllable)
    return result


def join(parts: list[str]) -> str:
    # An apostrophe keeps Xi'an from reading as Xian when a syllable starts with a vowel.
    text = ""
    for part in parts:
        text += ("'" if text and part[0] in "aoe" else "") + part
    return text.capitalize()


def convert(cell: str, surname: bool) -> str:
    cell = cell.strip()
    if not cell:
        return ""
    return join(syllables(cell, SURNAMES if surname else None))


def main() -> int:
    with SOURCE.open(encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))[2:]  # skip the dumper header row and the sheet's own header row

    out = []
    for row in rows:
        row += [""] * (5 - len(row))
        out.append([row[1], convert(row[2], True), convert(row[3], False), convert(row[4], False)])

    with OUTPUT.open("w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f, lineterminator="\r\n")
        writer.writerow(["ID", "surnam", "male", "female"])
        writer.writerows(out)
    print(f"Wrote {len(out)} rows to {OUTPUT.relative_to(ROOT)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
