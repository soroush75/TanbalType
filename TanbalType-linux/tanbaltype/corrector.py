# -*- coding: utf-8 -*-
"""بافر کردن کلمهٔ در حال تایپ و اصلاح آن هنگام زدن Space/Enter/Tab (معادل CorrectionService).

برخلاف ویندوز و مک، اینجا کلیدهای فیزیکی (کد evdev) بافر می‌شوند و متنِ روی صفحه از روی جدول
چیدمانِ فعال ساخته می‌شود. اصلاح هم با تایپ دوبارهٔ کلیدها روی چیدمانِ درست انجام می‌شود.
"""
import time

from . import applog, detector, keymaps, mapper
from .keymaps import (COMMAND_MODIFIERS, KEY_BACKSPACE, KEY_CAPSLOCK, KEY_ENTER, KEY_KPENTER,
                      KEY_SPACE, KEY_TAB, MODIFIERS, SHIFT_KEYS)

DELIMITERS = {KEY_SPACE: ' ', KEY_ENTER: '\n', KEY_KPENTER: '\n', KEY_TAB: '\t'}

FUNCTION_KEYS = {'F%d' % i: 58 + i for i in range(1, 11)}
FUNCTION_KEYS.update({'F11': 87, 'F12': 88})


class Corrector:
    def __init__(self, vkbd, tracker, toggle_key=None, on_toggle=None):
        self.vkbd = vkbd
        self.tracker = tracker
        self.toggle_key = toggle_key
        self.on_toggle = on_toggle
        self.enabled = True
        # تایپ داخل پنجره‌های خودِ برنامه (مثلاً افزودن لغت استثنا) نباید اصلاح شود
        self.suspended = False
        self.caps = False
        self.persian_table_name = 'pes'
        self._persian_table = keymaps.persian_table('pes')
        self._mods = set()        # modifierهایی که الان پایین نگه داشته شده‌اند
        self._swallowed = set()   # کلیدهایی که press آن‌ها مصرف شد؛ release هم باید مصرف شود
        self.reset()

    # ------------------------------------------------------------ وضعیت

    def reset(self):
        self._buffer = []
        self._last_delimiter = None

    def set_persian_table(self, name):
        if name != self.persian_table_name:
            self.persian_table_name = name
            self._persian_table = keymaps.persian_table(name)
            mapper.configure(name)
            applog.write('Persian layout table: %s' % name)

    def set_enabled(self, enabled):
        self.enabled = enabled
        self.reset()
        applog.write('Enabled' if enabled else 'Disabled')

    # ------------------------------------------------------------ رویدادها

    def on_key(self, code, value, grabbed=True):
        """یک رویداد کلید از کیبورد فیزیکی. خروجی True یعنی رویداد باید به سیستم برسد."""
        if code in MODIFIERS:
            # وضعیت Caps Lock از چراغ کیبورد خوانده می‌شود (Daemon)، نه از فشردن کلید؛
            # چون در بعضی تنظیمات Caps Lock کلید تعویض زبان است
            if value == 1:
                self._mods.add(code)
            elif value == 0:
                self._mods.discard(code)
            return True

        if value == 0:
            if code in self._swallowed:
                self._swallowed.discard(code)
                return False
            return True
        if code in self._swallowed:
            return False  # تکرار خودکارِ کلیدی که مصرف شده بود

        # میان‌بر پیش از بررسی enabled سنجیده می‌شود تا در حالت غیرفعال هم بتواند برنامه را برگرداند.
        # فقط کلید تنها (بدون Ctrl/Alt/Super/Shift)؛ ترکیب‌هایی مثل Shift+F10 دست‌نخورده عبور می‌کنند.
        if code == self.toggle_key and not self._mods - {KEY_CAPSLOCK}:
            if grabbed:
                self._swallowed.add(code)
            if value == 1:
                self.set_enabled(not self.enabled)
                if self.on_toggle:
                    self.on_toggle(self.enabled)
            return not grabbed

        if not self.enabled or self.suspended:
            return True

        if self._mods & COMMAND_MODIFIERS:
            # میان‌برها (Ctrl+V، Alt+Tab، ...) متن یا پنجره را عوض می‌کنند
            self.reset()
            return True

        shift = bool(self._mods & SHIFT_KEYS)

        if code == KEY_BACKSPACE:
            # پاک‌کردن یعنی پاک‌کردن: برنامه نباید متن را بازگرداند یا دوباره تصمیم به اصلاح بگیرد
            if self._buffer:
                self._buffer.pop()
            else:
                # کلمه پاک شد و به فضای خالی رسیدیم؛ حافظهٔ جداکنندهٔ قبلی ریست می‌شود
                self._last_delimiter = None
            return True

        if code in DELIMITERS:
            if shift and code == KEY_SPACE:
                # Shift+Space در چیدمان فارسی نیم‌فاصله است
                self._buffer = []
                return True
            if value != 1:
                self.reset()
                return True
            return self._on_delimiter(code, grabbed)

        if code in keymaps.CHAR_KEYS:
            if not self._buffer:
                self.tracker.refresh_async()
            self._buffer.append((code, shift, self.caps))
            return True

        # جهت‌نماها، Home/End، Delete، Esc و ...: مکان‌نما جابه‌جا شده
        self.reset()
        return True

    def reset_pointer(self):
        """کلیک موس: مکان‌نما جابه‌جا شده است."""
        self.reset()

    # ------------------------------------------------------------ متن

    def _text(self, keys, persian):
        out = []
        for code, shift, caps in keys:
            if persian:
                ch = self._persian_table.get(code, (None, None))[1 if shift else 0]
            else:
                plain, upper = keymaps.US_BY_CODE[code]
                ch = upper if (shift != caps if plain.isalpha() else shift) else plain
            if ch:
                out.append(ch)
        return ''.join(out)

    def _plan(self, text, persian):
        """کلیدهای لازم برای تایپ متن روی یک چیدمان: فهرست (code, shift) یا None."""
        reverse = {' ': (KEY_SPACE, False), '\n': (KEY_ENTER, False), '\t': (KEY_TAB, False)}
        if persian:
            if self.persian_table_name in keymaps.SPACE_SHIFT_ZWNJ:
                reverse[keymaps.ZWNJ] = (KEY_SPACE, True)
            for level in (0, 1):
                for code, chars in self._persian_table.items():
                    reverse.setdefault(chars[level], (code, bool(level)))
        else:
            for code, plain, upper, _ in keymaps.US_KEYS:
                reverse.setdefault(plain, (code, False))
                reverse.setdefault(upper, (code, True))
        keys = []
        for ch in text:
            if ch not in reverse:
                return None
            keys.append(reverse[ch])
        return keys

    # ------------------------------------------------------------ اصلاح

    def _on_delimiter(self, code, grabbed):
        delimiter = DELIMITERS[code]
        keys, self._buffer = self._buffer, []

        # آیا کلمهٔ قبلی واقعاً با Space جدا شده است یا مثلاً با Enter (اول خط)
        replace_prev_space = self._last_delimiter == ' '
        self._last_delimiter = delimiter

        if len(keys) < 2:
            return True

        layout = self.tracker.current()
        if layout is None:
            applog.write('Delimiter: current layout unknown, skipped')
            return True

        word = self._text(keys, layout.persian)
        corrected = self._maybe_correct(word, layout.persian)
        # توجه: با فعال‌بودن log، متن تایپ‌شده هم ذخیره می‌شود (برای عیب‌یابی).
        applog.write("Delimiter key=%d layout=%s word='%s' -> '%s'"
                     % (code, 'fa' if layout.persian else 'en', word, corrected or word))
        if corrected is None:
            return True

        # کیبوردی که grab نشده: Space همین حالا به برنامه رسیده و باید پاک شود.
        # Enter/Tab را نمی‌شود پس گرفت (مثلاً پیام ارسال شده)، پس فقط روی Space اصلاح می‌کنیم.
        if not grabbed and delimiter != ' ':
            return True

        done = self._apply(word, corrected, delimiter, layout.persian, replace_prev_space,
                           delimiter_sent=not grabbed)
        if done and grabbed:
            self._swallowed.add(code)
            return False
        return True

    @staticmethod
    def _maybe_correct(word, persian):
        if not word.strip() or len(word) < 2:
            return None
        corrected = detector.detect_wrong_layout(word, persian)
        return None if corrected == word else corrected

    def _apply(self, original, corrected, delimiter, layout_was_persian, replace_space, delimiter_sent):
        corrected_is_persian = mapper.count_persian(corrected) > len(corrected) // 2
        delete_count = len(original)
        prefix = ''

        # فقط زمانی Space قبلی را حذف و دوباره تایپ می‌کنیم که مطمئن باشیم اول خط نیستیم
        # (حل مشکل به هم چسبیدن کلمات فارسی و انگلیسی در متن راست‌به‌چپ)
        if corrected_is_persian and not layout_was_persian and replace_space:
            prefix = ' '
            delete_count += 1
        if delimiter_sent:
            delete_count += 1

        plan = self._plan(prefix + corrected + delimiter, corrected_is_persian)
        if plan is None:
            applog.write("Apply skipped: '%s' cannot be typed on the target layout" % corrected)
            return False

        applog.write("Apply delete=%d '%s' -> '%s%s'" % (delete_count, original, prefix, corrected))

        # Shift و ... که کاربر هنوز نگه داشته، نباید روی کلیدهای ارسالی اثر بگذارد
        held = [c for c in self.vkbd.down if c in MODIFIERS]
        for c in held:
            self.vkbd.emit(c, 0)
        try:
            if corrected_is_persian != layout_was_persian:
                if self.tracker.switch(corrected_is_persian) is None:
                    return False
                time.sleep(0.03)

            for _ in range(delete_count):
                self.vkbd.tap(KEY_BACKSPACE)
            for key_code, shift in plan:
                if not corrected_is_persian and self.caps and keymaps.US_BY_CODE.get(key_code, ('',))[0].isalpha():
                    shift = not shift  # Caps Lock روشن است
                if shift:
                    self.vkbd.emit(keymaps.KEY_LEFTSHIFT, 1)
                self.vkbd.tap(key_code)
                if shift:
                    self.vkbd.emit(keymaps.KEY_LEFTSHIFT, 0)
        finally:
            for c in held:
                if c in self._mods:
                    self.vkbd.emit(c, 1)
        return True

    def press_shortcut(self, modifiers, key, times):
        """میان‌بر تعویض زبان (برای GNOME): modifierها نگه داشته و کلید times بار زده می‌شود."""
        for m in modifiers:
            self.vkbd.emit(m, 1)
        for _ in range(times):
            self.vkbd.tap(key, 0.02)
        time.sleep(0.02)
        for m in reversed(modifiers):
            self.vkbd.emit(m, 0)
