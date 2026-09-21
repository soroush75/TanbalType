# -*- coding: utf-8 -*-
"""
فهرست‌های لغت (نام سایت‌ها، لغات انگلیسی، پسوندها، ...) را از سورس C# نسخهٔ ویندوز
استخراج می‌کند و به Sources/Generated/DetectorData.swift تبدیل می‌کند.

این‌طوری فهرست‌ها فقط یک جا (Detector.cs) نگهداری می‌شوند و نسخهٔ مک با هر build
خودکار با نسخهٔ ویندوز هم‌گام می‌ماند.
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
CS_DIR = os.path.join(os.path.dirname(ROOT), 'TanbalType')
OUT = os.path.join(ROOT, 'Sources', 'Generated', 'DetectorData.swift')

# (فایل C#، نام فیلد در C#، نام در Swift، نوع Swift)
FIELDS = [
    ('Detector.cs', 'DomainSuffixes', 'domainSuffixes', 'array'),
    ('Detector.cs', 'SiteNames', 'siteNames', 'lowerset'),
    ('Detector.cs', 'EnglishWords', 'englishWords', 'lowerset'),
    ('Detector.cs', 'PersianColloquial', 'persianColloquial', 'array'),
    ('Detector.cs', 'PersianAttachedSuffixes', 'persianAttachedSuffixes', 'array'),
    ('Detector.cs', 'CommonTwoLetterPersian', 'commonTwoLetterPersian', 'set'),
    ('Detector.cs', 'PersianMarkers', 'persianMarkers', 'array'),
    ('Detector.cs', 'PersianBigramList', 'persianBigramList', 'array'),
    ('Detector.cs', 'EnglishClusters', 'englishClusters', 'array'),
    ('DetectorSelfTest.cs', 'EnglishOnFaLayout', 'englishOnFaLayout', 'array'),
    ('DetectorSelfTest.cs', 'PersianOnEnLayout', 'persianOnEnLayout', 'array'),
    ('DetectorSelfTest.cs', 'PersianShouldNotFix', 'persianShouldNotFix', 'array'),
    ('DetectorSelfTest.cs', 'SiteNamesShouldNotFix', 'siteNamesShouldNotFix', 'array'),
    ('DetectorSelfTest.cs', 'DigitTokensShouldNotFix', 'digitTokensShouldNotFix', 'array'),
    ('DetectorSelfTest.cs', 'EnglishShouldNotFix', 'englishShouldNotFix', 'array'),
]


def strip_comments(src):
    """کامنت‌های // و /* */ را حذف می‌کند، بدون دست زدن به محتوای رشته‌ها."""
    out, i, n = [], 0, len(src)
    while i < n:
        c = src[i]
        if c == '"':
            j = i + 1
            while j < n and src[j] != '"':
                j += 2 if src[j] == '\\' else 1
            out.append(src[i:j + 1])
            i = j + 1
        elif c == "'" and i + 2 < n:
            # کاراکتر C# مثل '\'' یا 'a'
            j = i + 1
            while j < n and src[j] != "'":
                j += 2 if src[j] == '\\' else 1
            out.append(src[i:j + 1])
            i = j + 1
        elif src.startswith('//', i):
            while i < n and src[i] != '\n':
                i += 1
        elif src.startswith('/*', i):
            i = src.index('*/', i) + 2
        else:
            out.append(c)
            i += 1
    return ''.join(out)


def extract(src, name):
    m = re.search(r'\b' + re.escape(name) + r'\s*=', src)
    if not m:
        sys.exit(f'field {name} not found')
    start = m.end()
    # اولین { یا [ بعد از = شروع فهرست است؛ تا بستهٔ متناظرش جلو می‌رویم
    open_idx = min(i for i in (src.find('{', start), src.find('[', start)) if i >= 0)
    opener = src[open_idx]
    closer = '}' if opener == '{' else ']'
    depth, i = 0, open_idx
    while True:
        ch = src[i]
        if ch == '"':
            i += 1
            while src[i] != '"':
                i += 2 if src[i] == '\\' else 1
        elif ch == opener:
            depth += 1
        elif ch == closer:
            depth -= 1
            if depth == 0:
                break
        i += 1
    body = src[open_idx + 1:i]
    items = re.findall(r'"((?:[^"\\]|\\.)*)"', body)
    if not items:
        sys.exit(f'field {name} is empty')
    return [bytes(s, 'utf-8').decode('unicode_escape').encode('latin-1').decode('utf-8')
            if '\\' in s else s for s in items]


def swift_literal(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def main():
    sources = {}
    lines = [
        '// ⚠️ این فایل خودکار توسط tools/gen_detector_data.py از سورس C# ساخته می‌شود.',
        '// دستی ویرایش نکنید؛ فهرست‌ها را در TanbalType/Detector.cs تغییر دهید.',
        '',
        'enum DetectorData {',
    ]
    for cs_file, cs_name, swift_name, kind in FIELDS:
        if cs_file not in sources:
            with open(os.path.join(CS_DIR, cs_file), encoding='utf-8-sig') as f:
                sources[cs_file] = strip_comments(f.read())
        items = extract(sources[cs_file], cs_name)
        if kind == 'lowerset':
            items = [s.lower() for s in items]
        literal = ',\n        '.join(swift_literal(s) for s in items)
        swift_type = '[String]' if kind == 'array' else 'Set<String>'
        lines.append(f'    static let {swift_name}: {swift_type} = [\n        {literal},\n    ]')
        lines.append('')
    lines.append('}')

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    content = '\n'.join(lines) + '\n'
    old = open(OUT, encoding='utf-8').read() if os.path.exists(OUT) else None
    if old != content:
        with open(OUT, 'w', encoding='utf-8') as f:
            f.write(content)
    print(f'DetectorData.swift: {len(FIELDS)} lists')


if __name__ == '__main__':
    main()
