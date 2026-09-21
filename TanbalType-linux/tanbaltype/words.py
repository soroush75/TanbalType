# -*- coding: utf-8 -*-
"""لغات شخصی و لغات استثنای کاربر (~/.config/tanbaltype/*.txt).

فایل‌ها ممکن است از بیرون (پنجرهٔ tray، دستور tanbaltype words ...، یا ویرایش دستی) عوض شوند؛
پس هر بار قبل از استفاده، زمان تغییر فایل بررسی و در صورت نیاز دوباره خوانده می‌شود.
"""
import os
import threading

from . import applog, paths


def _sorted(words):
    return sorted(words, key=lambda w: w.lower())


class _WordFile:
    file_name = ''

    def __init__(self):
        self._lock = threading.Lock()
        self._mtime = None
        self._words = {}

    @property
    def path(self):
        return os.path.join(paths.CONFIG_DIR, self.file_name)

    def _key(self, word):
        raise NotImplementedError

    def _reload(self):
        try:
            mtime = os.stat(self.path).st_mtime_ns
        except OSError:
            mtime = None
        if mtime == self._mtime:
            return
        self._mtime = mtime
        words = {}
        if mtime is not None:
            try:
                with open(self.path, encoding='utf-8-sig') as f:
                    for line in f:
                        line = line.strip()
                        key = self._key(line)
                        if key and key not in words:
                            words[key] = line
            except (OSError, UnicodeDecodeError) as exc:
                applog.write('Read %s failed: %s' % (self.file_name, exc))
        self._words = words

    def _save(self):
        try:
            os.makedirs(paths.CONFIG_DIR, exist_ok=True)
            lines = _sorted(self._words.values())
            tmp = self.path + '.tmp'
            with open(tmp, 'w', encoding='utf-8') as f:
                f.write(''.join(line + '\n' for line in lines))
            os.replace(tmp, self.path)
            self._mtime = os.stat(self.path).st_mtime_ns
        except OSError as exc:
            applog.error('Save %s failed: %s' % (self.file_name, exc))

    def all(self):
        with self._lock:
            self._reload()
            return _sorted(self._words.values())

    def lookup(self, word):
        with self._lock:
            self._reload()
            if not self._words:
                return None
            return self._words.get(self._key(word))

    def add(self, word):
        word = word.strip()
        key = self._key(word)
        if not key:
            return False
        with self._lock:
            self._reload()
            if key in self._words:
                return False
            self._words[key] = word
            self._save()
            return True

    def remove(self, word):
        with self._lock:
            self._reload()
            if self._words.pop(self._key(word), None) is None:
                return False
            self._save()
            return True


class _UserWords(_WordFile):
    """لغاتی که کاربر خودش به برنامه یاد می‌دهد. هر بار که همان کلمه با چیدمانِ اشتباه
    تایپ شود، برنامه آن را دقیقاً به شکلی که کاربر ثبت کرده اصلاح می‌کند."""
    file_name = 'userwords.txt'

    def _key(self, word):
        # یکسان‌سازی نویسه‌های عربی و حذف نیم‌فاصله/نشانگرهای جهت، تا کلمهٔ ثبت‌شده
        # با خروجیِ نگاشتِ کیبورد (که همیشه «ی» و «ک» فارسی و بدون نیم‌فاصله است) تطبیق پیدا کند.
        out = []
        for ch in word.strip():
            if ch in 'يى':
                out.append('ی')
            elif ch == 'ك':
                out.append('ک')
            elif ch in '‌‎‏':
                continue
            else:
                out.append(ch)
        return ''.join(out).lower()

    def is_user_word(self, word):
        return self.lookup(word) is not None

    def canonical(self, word):
        return self.lookup(word)


class _UserExceptions(_WordFile):
    """لغات استثنا: کلماتی که برنامه هرگز روی آن‌ها اصلاح خودکار انجام نمی‌دهد."""
    file_name = 'exceptions.txt'

    def _key(self, word):
        return word.strip().lower()

    def is_exception(self, word):
        if not word.strip():
            return False
        return self.lookup(word) is not None


user_words = _UserWords()
exceptions = _UserExceptions()
