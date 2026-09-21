# -*- coding: utf-8 -*-
"""جدول کلیدهای فیزیکی (کدهای evdev لینوکس) و خروجی آن‌ها روی چیدمان US و چیدمان‌های فارسی xkb.

جدول‌های فارسی از فایل symbols/ir پروژهٔ xkeyboard-config برداشته شده‌اند:
  pes      → «Persian» (استاندارد ISIRI 9147؛ چیدمان پیش‌فرض لینوکس، «پ» روی کلید m)
  winkeys  → «Persian (Windows)» (همان چیدمان ویندوز؛ «پ» روی کلید \\)
"""

# کدهای evdev (linux/input-event-codes.h)
KEY_ESC = 1
KEY_BACKSPACE = 14
KEY_TAB = 15
KEY_ENTER = 28
KEY_LEFTCTRL = 29
KEY_LEFTSHIFT = 42
KEY_RIGHTSHIFT = 54
KEY_LEFTALT = 56
KEY_SPACE = 57
KEY_CAPSLOCK = 58
KEY_F1 = 59
KEY_F10 = 68
KEY_F11 = 87
KEY_F12 = 88
KEY_KPENTER = 96
KEY_RIGHTCTRL = 97
KEY_RIGHTALT = 100
KEY_LEFTMETA = 125
KEY_RIGHTMETA = 126
KEY_A = 30
KEY_Z = 44

SHIFT_KEYS = frozenset((KEY_LEFTSHIFT, KEY_RIGHTSHIFT))
# کلیدهایی که با نگه‌داشتنشان کلید بعدی «تایپ» حساب نمی‌شود (میان‌بر است)
COMMAND_MODIFIERS = frozenset((KEY_LEFTCTRL, KEY_RIGHTCTRL, KEY_LEFTALT, KEY_RIGHTALT,
                               KEY_LEFTMETA, KEY_RIGHTMETA))
MODIFIERS = SHIFT_KEYS | COMMAND_MODIFIERS | {KEY_CAPSLOCK}

# (کد evdev، حرف US بدون Shift، حرف US با Shift، نام کلید در xkb)
US_KEYS = [
    (41, '`', '~', 'TLDE'),
    (2, '1', '!', 'AE01'), (3, '2', '@', 'AE02'), (4, '3', '#', 'AE03'),
    (5, '4', '$', 'AE04'), (6, '5', '%', 'AE05'), (7, '6', '^', 'AE06'),
    (8, '7', '&', 'AE07'), (9, '8', '*', 'AE08'), (10, '9', '(', 'AE09'),
    (11, '0', ')', 'AE10'), (12, '-', '_', 'AE11'), (13, '=', '+', 'AE12'),
    (16, 'q', 'Q', 'AD01'), (17, 'w', 'W', 'AD02'), (18, 'e', 'E', 'AD03'),
    (19, 'r', 'R', 'AD04'), (20, 't', 'T', 'AD05'), (21, 'y', 'Y', 'AD06'),
    (22, 'u', 'U', 'AD07'), (23, 'i', 'I', 'AD08'), (24, 'o', 'O', 'AD09'),
    (25, 'p', 'P', 'AD10'), (26, '[', '{', 'AD11'), (27, ']', '}', 'AD12'),
    (43, '\\', '|', 'BKSL'),
    (30, 'a', 'A', 'AC01'), (31, 's', 'S', 'AC02'), (32, 'd', 'D', 'AC03'),
    (33, 'f', 'F', 'AC04'), (34, 'g', 'G', 'AC05'), (35, 'h', 'H', 'AC06'),
    (36, 'j', 'J', 'AC07'), (37, 'k', 'K', 'AC08'), (38, 'l', 'L', 'AC09'),
    (39, ';', ':', 'AC10'), (40, "'", '"', 'AC11'),
    (44, 'z', 'Z', 'AB01'), (45, 'x', 'X', 'AB02'), (46, 'c', 'C', 'AB03'),
    (47, 'v', 'V', 'AB04'), (48, 'b', 'B', 'AB05'), (49, 'n', 'N', 'AB06'),
    (50, 'm', 'M', 'AB07'), (51, ',', '<', 'AB08'), (52, '.', '>', 'AB09'),
    (53, '/', '?', 'AB10'),
]

US_BY_CODE = {code: (plain, shifted) for code, plain, shifted, _ in US_KEYS}
CODE_BY_XKB = {name: code for code, _, _, name in US_KEYS}
CHAR_KEYS = frozenset(US_BY_CODE)

ZWNJ = '‌'

# سطح ۱ و ۲ (بدون Shift / با Shift) هر کلید
PERSIAN_LAYOUTS = {
    'pes': {
        'TLDE': ('‍', '÷'),
        'AE01': ('۱', '!'), 'AE02': ('۲', '٬'), 'AE03': ('۳', '٫'),
        'AE04': ('۴', '﷼'), 'AE05': ('۵', '٪'), 'AE06': ('۶', '×'),
        'AE07': ('۷', '،'), 'AE08': ('۸', '*'), 'AE09': ('۹', ')'), 'AE10': ('۰', '('),
        'AE11': ('-', 'ـ'), 'AE12': ('=', '+'),
        'AD01': ('ض', 'ْ'), 'AD02': ('ص', 'ٌ'), 'AD03': ('ث', 'ٍ'),
        'AD04': ('ق', 'ً'), 'AD05': ('ف', 'ُ'), 'AD06': ('غ', 'ِ'),
        'AD07': ('ع', 'َ'), 'AD08': ('ه', 'ّ'), 'AD09': ('خ', ']'),
        'AD10': ('ح', '['), 'AD11': ('ج', '}'), 'AD12': ('چ', '{'),
        'AC01': ('ش', 'ؤ'), 'AC02': ('س', 'ئ'), 'AC03': ('ی', 'ي'), 'AC04': ('ب', 'إ'),
        'AC05': ('ل', 'أ'), 'AC06': ('ا', 'آ'), 'AC07': ('ت', 'ة'), 'AC08': ('ن', '»'),
        'AC09': ('م', '«'), 'AC10': ('ک', ':'), 'AC11': ('گ', '؛'),
        'AB01': ('ظ', 'ك'), 'AB02': ('ط', 'ٓ'), 'AB03': ('ز', 'ژ'),
        'AB04': ('ر', 'ٰ'), 'AB05': ('ذ', ZWNJ), 'AB06': ('د', 'ٔ'),
        'AB07': ('پ', 'ء'), 'AB08': ('و', '>'), 'AB09': ('.', '<'), 'AB10': ('/', '؟'),
        'BKSL': ('\\', '|'),
    },
    'winkeys': {
        'TLDE': ('÷', '×'),
        'AE01': ('1', '!'), 'AE02': ('2', '@'), 'AE03': ('3', '#'), 'AE04': ('4', '$'),
        'AE05': ('5', '%'), 'AE06': ('6', '^'), 'AE07': ('7', '&'), 'AE08': ('8', '*'),
        'AE09': ('9', ')'), 'AE10': ('0', '('), 'AE11': ('-', '_'), 'AE12': ('=', '+'),
        'AD01': ('ض', 'ً'), 'AD02': ('ص', 'ٌ'), 'AD03': ('ث', 'ٍ'),
        'AD04': ('ق', '﷼'), 'AD05': ('ف', '،'), 'AD06': ('غ', '؛'),
        'AD07': ('ع', ','), 'AD08': ('ه', ']'), 'AD09': ('خ', '['),
        'AD10': ('ح', '\\'), 'AD11': ('ج', '}'), 'AD12': ('چ', '{'),
        'AC01': ('ش', 'َ'), 'AC02': ('س', 'ُ'), 'AC03': ('ی', 'ِ'),
        'AC04': ('ب', 'ّ'), 'AC05': ('ل', 'ۀ'), 'AC06': ('ا', 'آ'),
        'AC07': ('ت', 'ـ'), 'AC08': ('ن', '«'), 'AC09': ('م', '»'), 'AC10': ('ک', ':'),
        'AC11': ('گ', '"'), 'BKSL': ('پ', '|'),
        'AB01': ('ظ', 'ة'), 'AB02': ('ط', 'ي'), 'AB03': ('ز', 'ژ'), 'AB04': ('ر', 'ؤ'),
        'AB05': ('ذ', 'أ'), 'AB06': ('د', 'إ'), 'AB07': ('ئ', 'ء'), 'AB08': ('و', '<'),
        'AB09': ('.', '>'), 'AB10': ('/', '؟'),
    },
}

# Shift+Space در چیدمان pes نیم‌فاصله تایپ می‌کند (nbsp(zwnj2nb3nnb4))
SPACE_SHIFT_ZWNJ = frozenset(('pes',))

PERSIAN_LAYOUT_NAMES = {
    'pes': 'Persian (ISIRI 9147)',
    'winkeys': 'Persian (Windows)',
}


def persian_table(name):
    """جدول {کد evdev: (بدون Shift، با Shift)} یک چیدمان فارسی."""
    layout = PERSIAN_LAYOUTS.get(name) or PERSIAN_LAYOUTS['pes']
    return {CODE_BY_XKB[key]: chars for key, chars in layout.items()}


def variant_to_table(variant):
    """نام variant چیدمان ir در xkb (یا نام نمایشیِ آن) → نام جدول."""
    v = (variant or '').lower()
    if 'win' in v:
        return 'winkeys'
    return 'pes'
