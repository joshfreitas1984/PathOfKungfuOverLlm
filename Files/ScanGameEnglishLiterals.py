"""Finds the game's own English literals that sit next to a dumped Chinese literal and writes DynamicStringsGameEnglish.csv.

The game picks its text with `WordManager.Singleton.Open ? "English" : "中文"`. The dump only holds the Chinese side, and the plugin targets English
mode, so the game's English wins and our translation of the Chinese side is never shown. Packaging emits an extra contract per row here, carrying
the translation of the Chinese side, so the same transpiler replaces the English literal too (see Translate/TranslationPackaging.cs).

Re-run after a game update (new decompile, new dump):  python Files/ScanGameEnglishLiterals.py
Only pairs where both sides are string literals are found; a side that is a field, call or other expression is not.
A Chinese literal that is the left operand of a `+` (`"修为+" + num`) is a fragment, not a whole string, so it is matched against the dump as is.
"""
import csv
import glob
import os
import re
import sys

root = os.path.dirname(os.path.abspath(__file__))
zh = re.compile(r'[一-鿿]')
# A string literal, optionally interpolated: quotes, then any run of non-quote characters or escaped characters.
literal = r'\$?"(?:[^"\\\n]|\\.)*"'
# Open ? "English" : "中文"      and      Open ? "English" : ("中文" + value)
ternary = re.compile(r'WordManager\.Singleton\.Open\s*\?\s*(' + literal + r')\s*:\s*\(?\s*(' + literal + r')')
# A helper that takes the English and Chinese text, such as GetText("Phy Atk", "外功攻击", open).
helper_definition = re.compile(r'string\s+(\w+)\(\s*string\s+\w*[eE]nglish\w*\s*,\s*string\s+\w*[cC]hinese')
hole = re.compile(r'\{[^{}]*?(:[^{}]*)?\}')


def normalise(text):
    """Quotes off; an interpolated string's holes become {0}, {1}... in order, as the compiler's format string has them."""
    interpolated = text.startswith('$')
    text = text[1:] if interpolated else text
    text = text[1:-1]
    if not interpolated:
        return text
    count = iter(range(1000))
    text = text.replace('{{', '\x00').replace('}}', '\x01')
    text = hole.sub(lambda m: '{%d%s}' % (next(count), m.group(1) or ''), text)
    return text.replace('\x00', '{{').replace('\x01', '}}')


dump = {}
for line in open(os.path.join(root, 'Raw/Dumped/DynamicStrings/dynamicStrings.txt'), encoding='utf-8'):
    fields = line.rstrip('\n').split(',')
    if len(fields) == 5:
        dump.setdefault(fields[3], []).append((fields[0], fields[1]))

paths = glob.glob(os.path.join(root, 'Raw/Decompiled/Assembly-CSharp*/**/*.cs'), recursive=True)
sources = {path: open(path, encoding='utf-8', errors='ignore').read() for path in paths}

helpers = {m.group(1) for text in sources.values() for m in helper_definition.finditer(text)}
patterns = [ternary] + [re.compile(re.escape(name) + r'\(\s*(' + literal + r')\s*,\s*(' + literal + r')') for name in sorted(helpers)]

rows = set()
found = 0
for path, text in sources.items():
    stem = os.path.splitext(os.path.basename(path))[0]
    for match in (m for pattern in patterns for m in pattern.finditer(text)):
        english, chinese = normalise(match.group(1)), normalise(match.group(2))
        if zh.search(english) or not zh.search(chinese):
            continue
        found += 1
        for type_name, method in dump.get(chinese.replace(',', '，'), []):
            outer = type_name.split('/')[0].split('.')[-1].split('`')[0]
            if outer == stem:
                rows.add((type_name, method, english, chinese))

with open(os.path.join(root, 'DynamicStringsGameEnglish.csv'), 'w', encoding='utf-8', newline='') as out:
    writer = csv.writer(out, lineterminator='\n')
    writer.writerow(['Type', 'Method', 'English', 'Chinese'])
    writer.writerows(sorted(rows))

print(f'{found} literal pairs in the code, {len(rows)} rows matched to the dump', file=sys.stderr)
