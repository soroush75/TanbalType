# -*- coding: utf-8 -*-
"""نگاشت بین کلیدهای کیبورد US انگلیسی و چیدمان فارسی.

چیدمان فارسیِ پیش‌فرض لینوکس (pes / ISIRI 9147) با چیدمان ویندوز فرق دارد (مثلاً «پ» روی m است)؛
پس نگاشت از روی همان چیدمانِ فارسی‌ای ساخته می‌شود که کاربر در سیستم فعال کرده (configure).
نگاشت ویندوز فقط برای self-test (هم‌خوانی با نسخهٔ ویندوز) نگه داشته شده است.
"""
import unicodedata

from . import keymaps

# لایهٔ پایهٔ چیدمان «Persian» ویندوز — همان نگاشت نسخهٔ ویندوز (مبنای self-test).
WINDOWS_BASE_KEYS = {
    '`': 'ذ', '1': '۱', '2': '۲', '3': '۳', '4': '۴',
    '5': '۵', '6': '۶', '7': '۷', '8': '۸', '9': '۹',
    '0': '۰', '-': '-', '=': '=',
    'q': 'ض', 'w': 'ص', 'e': 'ث', 'r': 'ق', 't': 'ف',
    'y': 'غ', 'u': 'ع', 'i': 'ه', 'o': 'خ', 'p': 'ح',
    '[': 'ج', ']': 'چ', '\\': 'پ',
    'a': 'ش', 's': 'س', 'd': 'ی', 'f': 'ب', 'g': 'ل',
    'h': 'ا', 'j': 'ت', 'k': 'ن', 'l': 'م',
    ';': 'ک', "'": 'گ',
    'z': 'ظ', 'x': 'ط', 'c': 'ز', 'v': 'ر', 'b': 'ذ',
    'n': 'د', 'm': 'ئ', ',': 'و', '.': '.', '/': '/',
}

# «آ» با Shift+H تایپ می‌شود؛ بدون این نگاشت واژه‌هایی مثل «آقا» (Hrh) تشخیص داده نمی‌شدند.
WINDOWS_SHIFTED_KEYS = {'H': 'آ'}

# حروفی که لغت‌نامه با آن‌ها ساخته شده؛ فقط نگاشت‌های Shift دارِ منتهی به این حروف پذیرفته می‌شوند
# (اعراب، «ي»/«ك» عربی و علائم نباید حروف بزرگ انگلیسی را خراب کنند).
DICTIONARY_LETTERS = frozenset('ابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیآئءؤ')

PERSIAN_PUNCT_ASCII = frozenset(',;')

en_key_to_fa = {}
en_layout_keys = frozenset()
_fa_to_en_key = {}

# نام چیدمانی که نگاشت فعلی از آن ساخته شده
source_id = ''


def use_windows_layout():
    _apply(WINDOWS_BASE_KEYS, WINDOWS_SHIFTED_KEYS, 'windows')


def _usable(ch):
    """کاراکتری که واقعاً روی صفحه دیده می‌شود (نه فاصله، نیم‌فاصله یا نشانگر جهت)."""
    return (ch is not None and len(ch) == 1 and not ch.isspace()
            and unicodedata.category(ch) != 'Cf')


def configure(layout_name):
    """نگاشت را از روی جدول کلیدهای یک چیدمان فارسی xkb می‌سازد."""
    if layout_name not in keymaps.PERSIAN_LAYOUTS:
        layout_name = 'pes'
    if layout_name == source_id:
        return
    table = keymaps.persian_table(layout_name)

    base = {}
    shifted = {}
    for code, plain, _, _ in keymaps.US_KEYS:
        fa = table.get(code, (None, None))[0]
        if _usable(fa):
            base[plain] = fa

    # Shift فقط وقتی لحاظ می‌شود که حرفی تولید کند که در لایهٔ پایه نیست (مثل آ، ژ، ء، ؤ، ئ)
    base_letters = set(base.values())
    for code, plain, upper, _ in keymaps.US_KEYS:
        if not plain.isalpha():
            continue
        fa = table.get(code, (None, None))[1]
        if _usable(fa) and fa in DICTIONARY_LETTERS and fa not in base_letters:
            shifted[upper] = fa

    _apply(base, shifted, layout_name)


def _reverse_priority(key):
    if 'a' <= key <= 'z':
        return 0
    if '0' <= key <= '9':
        return 1
    return 2


def _apply(base, shifted, name):
    global en_key_to_fa, en_layout_keys, _fa_to_en_key, source_id
    mapping = dict(base)

    # حروف بزرگ (Caps Lock یا Shift) مثل حرف کوچکِ خودشان نگاشت شوند
    for key, fa in base.items():
        if key.isascii() and key.islower():
            mapping[key.upper()] = fa

    # نگاشت اختصاصی Shift بر فالبکِ بالا اولویت دارد
    mapping.update(shifted)

    # در نگاشت معکوس a-z بر ارقام/علائم اولویت دارد (مثلاً b و ` هر دو → ذ)
    reverse = {}
    for key, fa in sorted(mapping.items(), key=lambda kv: (_reverse_priority(kv[0]), kv[0])):
        reverse.setdefault(fa, key)

    en_key_to_fa = mapping
    en_layout_keys = frozenset(mapping)
    _fa_to_en_key = reverse
    source_id = name


def en_keys_to_persian(text):
    return ''.join(en_key_to_fa.get(ch, ch) for ch in text)


def persian_to_en_keys(text):
    return ''.join(_fa_to_en_key.get(ch, ch) for ch in text)


def swap_layout(text):
    """همان متن، اگر با چیدمانِ مقابل تایپ می‌شد چه شکلی می‌شد."""
    return persian_to_en_keys(text) if count_persian(text) > 0 else en_keys_to_persian(text)


def count_en_layout_keys(text):
    return sum(1 for ch in text if ch in en_layout_keys)


def is_persian_char(ch):
    return '؀' <= ch <= 'ۿ'


def count_persian(text):
    return sum(1 for ch in text if is_persian_char(ch))


def count_ascii_letters(text):
    return sum(1 for ch in text if ch.isascii() and ch.isalpha())


use_windows_layout()
