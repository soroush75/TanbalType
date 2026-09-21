# -*- coding: utf-8 -*-
"""آزمون درستیِ تشخیص دوطرفه (پورت DetectorSelfTest.cs). داده‌ها از سورس C# تولید می‌شوند.

کلیدهای PersianOnEnLayout برای چیدمان ویندوز نوشته شده‌اند؛ برای چیدمان‌های لینوکس، خودِ واژهٔ
فارسیِ مورد انتظار نگه داشته می‌شود و کلیدهایش از روی چیدمانِ آزمون‌شده دوباره ساخته می‌شود.
"""
from . import applog, detector, keymaps, mapper

DATA = detector.DATA


def run(layouts=None):
    """خروجی: تعداد موارد ناموفق."""
    current = mapper.source_id
    failed = 0

    mapper.use_windows_layout()
    persian_targets = [mapper.en_keys_to_persian(w) for w in DATA['persianOnEnLayout']]

    if layouts is None:
        layouts = list(keymaps.PERSIAN_LAYOUTS)
    # نگاشت ویندوز همیشه آزموده می‌شود؛ به‌علاوهٔ چیدمان‌های لینوکس خواسته‌شده
    for name in ['windows'] + [n for n in layouts if n != 'windows']:
        if name == 'windows':
            mapper.use_windows_layout()
        else:
            mapper.configure(name)
        count = _run_cases(persian_targets)
        failed += count
        applog.write('SelfTest [%s]: %d failure(s)' % (name, count))

    if current and current != 'windows':
        mapper.configure(current)

    applog.write('SelfTest OK — corrections + no false positives' if failed == 0
                 else 'SelfTest FAILED — %d case(s)' % failed)
    return failed


def _run_cases(persian_targets):
    failed = 0
    detect = detector.detect_wrong_layout

    for word in DATA['englishOnFaLayout']:
        on_screen = mapper.en_keys_to_persian(word)
        got = detect(on_screen, True)
        if got != word:
            applog.write('SelfTest FA->EN FAIL: %s -> %r (expect %s)' % (on_screen, got, word))
            failed += 1

    for expected in persian_targets:
        keys = mapper.persian_to_en_keys(expected)
        got = detect(keys, False)
        if got != expected:
            applog.write('SelfTest EN->FA FAIL: %s -> %r (expect %s)' % (keys, got, expected))
            failed += 1

    for word in DATA['persianShouldNotFix']:
        if detect(word, True) is not None:
            applog.write('SelfTest FA false-positive: %s' % word)
            failed += 1

    for word in DATA['englishShouldNotFix']:
        if detect(word, False) is not None:
            applog.write('SelfTest EN false-positive: %s' % word)
            failed += 1

    for word in DATA['siteNamesShouldNotFix']:
        fixed = detect(word, False)
        if fixed is not None:
            applog.write('SelfTest site-name interference: %s -> %r' % (word, fixed))
            failed += 1

    for word in DATA['digitTokensShouldNotFix']:
        if detect(word, True) is not None or detect(word, False) is not None:
            applog.write('SelfTest digit false-positive: %s' % word)
            failed += 1

    return failed
