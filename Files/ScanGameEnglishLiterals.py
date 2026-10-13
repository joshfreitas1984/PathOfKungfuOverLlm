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

branch = re.compile(r'if\s*\(\s*(!?)\s*WordManager\.Singleton\.Open\s*\)\s*\{')
literal_in_code = re.compile(literal)


def block_end(text, open_brace):
    """Index just past the brace that closes the one at open_brace (string literals are skipped), or -1."""
    depth = 0
    i = open_brace
    while i < len(text):
        c = text[i]
        if c == '"':
            i += 1
            while i < len(text) and text[i] != '"':
                i += 2 if text[i] == '\\' else 1
        elif c == '{':
            depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0:
                return i + 1
        i += 1
    return -1


def branch_pairs(text):
    """`if (Open) { English } else { 中文 }` (or the reverse with `!Open`): the literals of the two blocks, paired by position.
    Only blocks with the same number of literals are paired, and only a position where one side has Chinese and the other has none."""
    for match in branch.finditer(text):
        first_end = block_end(text, match.end() - 1)
        tail = re.match(r'\s*else\s*\{', text[first_end:]) if first_end > 0 else None
        if not tail:
            continue
        second_start = first_end + tail.end() - 1
        second_end = block_end(text, second_start)
        if second_end < 0:
            continue
        first = literal_in_code.findall(text[match.end() - 1:first_end])
        second = literal_in_code.findall(text[second_start:second_end])
        if len(first) != len(second):
            continue
        english_block, chinese_block = (second, first) if match.group(1) else (first, second)
        for english, chinese in zip(english_block, chinese_block):
            yield english, chinese


def candidates(text):
    for pattern in patterns:
        for match in pattern.finditer(text):
            yield match.group(1), match.group(2)
    yield from branch_pairs(text)


rows = set()
found = 0
for path, text in sources.items():
    stem = os.path.splitext(os.path.basename(path))[0]
    for english_literal, chinese_literal in candidates(text):
        english, chinese = normalise(english_literal), normalise(chinese_literal)
        # An empty English side ("" : "文", the currency suffix) would replace every empty string in the method.
        # A block pairing can line up a separator (":") with a Chinese string, so the English side must have at least two letters.
        if not re.search(r'[A-Za-z]{2}', english) or zh.search(english) or not zh.search(chinese):
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
