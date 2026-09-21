# -*- coding: utf-8 -*-
"""تشخیص کلمه‌ای که با چیدمان اشتباه تایپ شده — پورت مستقیم Detector.cs نسخهٔ ویندوز.
فهرست‌های لغت از detector_data.json (ساخته‌شده از سورس C#) خوانده می‌شوند."""
import json
import os
import threading
import unicodedata

from . import applog, mapper, paths
from .words import exceptions, user_words

SCORE_MARGIN = 0.12
STRONG_ENGLISH_THRESHOLD = 0.45
STRONG_PERSIAN_THRESHOLD = 0.35

with open(os.path.join(paths.PACKAGE_DIR, 'detector_data.json'), encoding='utf-8') as _f:
    DATA = json.load(_f)

_domain_suffixes = DATA['domainSuffixes']
_site_names = frozenset(DATA['siteNames'])
_english_words = frozenset(DATA['englishWords'])
_persian_attached_suffixes = DATA['persianAttachedSuffixes']
_common_two_letter_persian = frozenset(DATA['commonTwoLetterPersian'])
_persian_markers = DATA['persianMarkers']
_english_clusters = DATA['englishClusters']
_persian_bigrams = frozenset(DATA['persianBigramList'])

_persian_words = None
_load_lock = threading.Lock()


def persian_words():
    """لغت‌نامهٔ بزرگ فارسی (بار اول از فایل خوانده می‌شود)."""
    global _persian_words
    if _persian_words is not None:
        return _persian_words
    with _load_lock:
        if _persian_words is None:
            words = set()
            path = paths.data_file('PersianWords.txt')
            if path:
                with open(path, encoding='utf-8-sig') as f:
                    for line in f:
                        line = line.rstrip('\r\n')
                        if len(line) >= 2:
                            words.add(line)
            else:
                applog.error('PersianWords.txt NOT FOUND — dictionary is empty')
            words.update(DATA['persianColloquial'])
            _persian_words = words
    return _persian_words


# قدرتِ تطبیق یک کلمه با لغت‌نامهٔ فارسی
NONE, EXACT, STRONG, WEAK = 0, 1, 2, 3


def _lookup_persian(word):
    """جستجوی کلمه در لغت‌نامه، مستقیم یا با جداکردن پسوند چسبان (مثل «تشخیصش»، «کتابم»، «خونمون»)"""
    words = persian_words()
    if word in words:
        return EXACT

    length = len(word)
    for suffix in _persian_attached_suffixes:
        suffix_length = len(suffix)
        # برای پسوندهای تک‌حرفی، ریشهٔ حداقل ۳ حرفی لازم است
        min_stem = 3 if suffix_length == 1 else 2
        if length < suffix_length + min_stem:
            continue
        if not word.endswith(suffix):
            continue
        stem = word[:-suffix_length]
        if stem not in words:
            continue
        return WEAK if suffix_length == 1 and len(stem) <= 3 else STRONG

    return NONE


def _is_known_persian_word(word):
    return _lookup_persian(word) != NONE


def _is_english_word(word):
    return word.lower() in _english_words


def _is_digit(ch):
    """معادل char.IsDigit در .NET: هر رقم دهدهی یونیکد (از جمله ارقام فارسی و عربی)."""
    return unicodedata.category(ch) == 'Nd'


def detect_wrong_layout(text, current_layout_is_persian):
    """اگر کلمه با چیدمان اشتباه تایپ شده باشد، شکل درستش را برمی‌گرداند؛ وگرنه None."""
    word = text.strip()
    if len(word) < 2:
        return None

    # کلمات استثنایی که کاربر خودش اضافه کرده هرگز اصلاح نمی‌شوند
    if exceptions.is_exception(word):
        return None

    # لغاتی که کاربر خودش ثبت کرده بر بقیهٔ قاعده‌ها (از جمله محدودیتِ اعداد) اولویت دارند
    if user_words.is_user_word(word):
        return None
    mapped = (mapper.persian_to_en_keys(word) if current_layout_is_persian
              else mapper.en_keys_to_persian(word))
    canonical = user_words.canonical(mapped)
    if canonical is not None:
        return canonical

    # اعداد را هرگز اصلاح نمی‌کنیم: رمز عبور، کد، شماره، سال، نسخه و ...
    if any(_is_digit(ch) for ch in word):
        return None

    if current_layout_is_persian:
        return _detect_english_intended_on_persian_layout(word)
    return _detect_persian_intended_on_english_layout(word)


def _detect_english_intended_on_persian_layout(word):
    # کلمهٔ لغت‌نامه‌ای یا کلمه‌ای با پسوند چسبان — فارسیِ عمدی است
    if _is_known_persian_word(word):
        return None

    mapped_en = mapper.persian_to_en_keys(word)

    if exceptions.is_exception(mapped_en):
        return None

    # اگر روی حالت فارسی آدرس یا نام سایت تایپ شده باشد، به انگلیسی برمی‌گردد
    if _should_convert_to_web_address(mapped_en):
        return mapped_en

    if _is_english_word(mapped_en):
        return mapped_en

    length = float(len(word))
    if mapper.count_persian(word) >= length * 0.75:
        fa_score = _score_intentional_persian(word)
        if fa_score >= STRONG_PERSIAN_THRESHOLD:
            return None

        en_score = _score_english_on_persian_keys(word, mapped_en)
        if en_score >= 0.28 and en_score > fa_score + SCORE_MARGIN:
            return mapped_en
        return None

    if _mappable_en_ratio(word) >= 0.75:
        english_score = _score_intentional_english(word)
        persian_on_keys = _score_persian_on_english_keys(word)
        if english_score >= 0.28 and english_score > persian_on_keys + SCORE_MARGIN:
            return word

    return None


def _detect_persian_intended_on_english_layout(word):
    # کاربر در حال وارد کردن آدرس یا نام سایت است — نباید به فارسی تبدیل شود
    if _is_probably_web_input(word):
        return None

    if _is_english_comma_list(word):
        return None

    if _is_english_word(word):
        return None

    mapped_fa = mapper.en_keys_to_persian(word)

    if exceptions.is_exception(mapped_fa):
        return None

    match = _lookup_persian(mapped_fa)
    english_score = _score_intentional_english(word)

    # توکن دوحرفی: فقط واژه‌های پرکاربرد از روی لغت‌نامه اصلاح می‌شوند
    if len(mapped_fa) == 2 and mapped_fa not in _common_two_letter_persian:
        match = NONE

    # تطبیقِ ضعیف نباید بر شواهدِ قویِ انگلیسی غلبه کند
    if match == WEAK and english_score >= STRONG_ENGLISH_THRESHOLD:
        match = NONE

    if match != NONE:
        return mapped_fa

    if english_score >= STRONG_ENGLISH_THRESHOLD:
        return None

    persian_score = _score_persian_on_english_keys(word)
    if persian_score >= 0.22 and persian_score > english_score + SCORE_MARGIN:
        return mapped_fa

    return None


def _is_probably_web_input(word):
    """آیا کاربر روی حالت انگلیسی مشغول تایپ آدرس/ایمیل/نام سایت است؟"""
    if '://' in word:
        return True
    if word.lower().startswith('www.'):
        return True
    if '@' in word and _looks_like_email_or_domain(word):
        return True
    if _has_domain_suffix(word):
        return True
    return _is_known_site_name(word)


def _should_convert_to_web_address(mapped_en):
    """آیا متن نگاشت‌شده از حالت فارسی، یک آدرس/نام سایت واقعی است؟ (سخت‌گیری زیاد)"""
    if _is_known_site_name(mapped_en):
        return True
    if not _looks_like_email_or_domain(mapped_en):
        return False
    if mapped_en.lower().startswith('www.') and len(mapped_en) >= 8:
        return True
    return _has_domain_suffix(mapped_en) and mapper.count_ascii_letters(mapped_en) >= 4


def _has_domain_suffix(text):
    lower = text.lower()
    return any(len(lower) > len(s) and lower.endswith(s) for s in _domain_suffixes)


def _is_known_site_name(ascii_text):
    """نام سایت با یا بدون www. و پسوند دامنه (مثل digikala یا www.digikala.com)"""
    core = ascii_text.lower()
    if core.startswith('www.'):
        core = core[4:]
    for suffix in _domain_suffixes:
        if len(core) > len(suffix) and core.endswith(suffix):
            core = core[:-len(suffix)]
            break
    return len(core) >= 3 and core in _site_names


def _has_persian_punct(text):
    return any(ch in mapper.PERSIAN_PUNCT_ASCII for ch in text)


def _is_english_comma_list(word):
    if not _has_persian_punct(word):
        return False
    parts = [p for p in word.replace(';', ',').split(',') if p]
    return len(parts) >= 2 and all(_is_english_word(p) for p in parts)


def _mappable_en_ratio(text):
    return mapper.count_en_layout_keys(text) / len(text) if text else 0.0


def _mappable_fa_ratio(text):
    if not text:
        return 0.0
    converted = mapper.persian_to_en_keys(text)
    changed = sum(1 for a, b in zip(text, converted) if a != b)
    return changed / len(text)


def _persian_bigram_score(text):
    if len(text) < 2:
        return 0.0
    hits = sum(1 for i in range(len(text) - 1) if text[i:i + 2] in _persian_bigrams)
    return hits / (len(text) - 1)


def _has_persian_marker(text):
    return any(m in text for m in _persian_markers)


def _english_cluster_score(word):
    lower = word.lower()
    return 0.55 if any(c in lower for c in _english_clusters) else 0.0


def _lower_letters(word):
    return [ch.lower() for ch in word if ch.isalpha()]


def _is_vowel(ch):
    return ch in 'aeiou'


def _english_vowel_score(word):
    letters = _lower_letters(word)
    if len(letters) < 2:
        return 0.0
    vowels = sum(1 for ch in letters if _is_vowel(ch))
    if vowels == 0:
        return 0.0
    ratio = vowels / len(letters)
    if len(letters) >= 4 and ratio >= 0.38:
        return 0.65
    if ratio >= 0.30:
        return 0.35
    return 0.22


def _score_likely_english(candidate):
    if _is_english_word(candidate) or _is_known_site_name(candidate):
        return 1.0
    if _has_persian_punct(candidate):
        return 0.0

    letters = mapper.count_ascii_letters(candidate)
    if letters < len(candidate) * 0.85:
        return 0.0
    if letters <= 3:
        return 0.0
    if not _has_reasonable_english_vowel_ratio(candidate):
        return 0.0
    if _looks_like_gibberish_ascii(candidate):
        return 0.0
    if _looks_like_email_or_domain(candidate):
        return max(_english_vowel_score(candidate), 0.35)

    score = _english_cluster_score(candidate) + _english_vowel_score(candidate)
    return min(score, 1.0) if score >= 0.25 else 0.0


def _has_reasonable_english_vowel_ratio(word):
    letters = _lower_letters(word)
    if not letters:
        return False
    vowels = sum(1 for ch in letters if _is_vowel(ch))
    return vowels > 0 and vowels / len(letters) >= 0.20


def _looks_like_gibberish_ascii(word):
    if _has_long_consonant_run(word):
        return True
    letters = _lower_letters(word)
    if len(letters) >= 5:
        vowels = sum(1 for ch in letters if _is_vowel(ch))
        if vowels / len(letters) < 0.18:
            return True
    return False


def _has_long_consonant_run(word):
    run = 0
    for ch in word.lower():
        if ch.isalpha() and not _is_vowel(ch):
            run += 1
            if run >= 5:
                return True
        else:
            run = 0
    return False


def _score_intentional_english(word):
    if _is_english_word(word) or _is_known_site_name(word):
        return 1.0
    if _has_persian_punct(word):
        return 0.0
    return min(_english_cluster_score(word) + _english_vowel_score(word), 1.0)


def _score_persian_on_english_keys(word):
    if _mappable_en_ratio(word) < 0.75:
        return 0.0
    converted = mapper.en_keys_to_persian(word)
    if mapper.count_persian(converted) < len(converted) * 0.85:
        return 0.0

    bigram = _persian_bigram_score(converted)
    marker = _has_persian_marker(converted)
    score = bigram * 0.55
    if marker:
        score += 0.42
    if _has_persian_punct(word):
        score += 0.18
    if len(word) <= 3 and not marker and bigram < 0.45:
        return 0.0
    return min(score, 1.0)


def _score_intentional_persian(word):
    if _is_known_persian_word(word):
        return 1.0
    if mapper.count_persian(word) < len(word) * 0.85:
        return 0.0
    score = _persian_bigram_score(word) * 0.5
    if _has_persian_marker(word):
        score += 0.45
    if len(word) >= 4:
        score += 0.12
    return min(score, 1.0)


def _score_english_on_persian_keys(word, candidate):
    if mapper.count_persian(word) < len(word) * 0.75:
        return 0.0
    if _mappable_fa_ratio(word) < 0.75:
        return 0.0
    if not candidate.isascii() or mapper.count_persian(candidate) > 0:
        return 0.0
    if not _has_enough_ascii_letters(candidate):
        return 0.0
    return _score_likely_english(candidate)


def _has_enough_ascii_letters(candidate):
    if mapper.count_ascii_letters(candidate) >= len(candidate) * 0.85:
        return True
    return _looks_like_email_or_domain(candidate)


def _looks_like_email_or_domain(text):
    if mapper.count_ascii_letters(text) < 3:
        return False
    return all((ch.isascii() and ch.isalnum()) or ch in '.@-_' for ch in text)
