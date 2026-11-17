# -*- coding: utf-8 -*-
"""آزمون منطق اصلاح با کیبورد مجازی و backend ساختگی (بدون نیاز به لینوکس واقعی).

    python3 -m unittest discover -s tests
"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.environ['XDG_CONFIG_HOME'] = tempfile.mkdtemp()

from tanbaltype import keymaps, layout, mapper  # noqa: E402
from tanbaltype.corrector import Corrector  # noqa: E402
from tanbaltype.words import user_words  # noqa: E402

US_CODE = {plain: code for code, plain, _, _ in keymaps.US_KEYS}


class FakeKeyboard:
    def __init__(self, screen):
        self.screen = screen
        self.down = set()

    def emit(self, code, value):
        if value:
            self.down.add(code)
        else:
            self.down.discard(code)
        if value:
            self.screen.press(code)

    def tap(self, code, delay=0):
        self.emit(code, 1)
        self.emit(code, 0)


class FakeBackend(layout.Backend):
    name = 'fake'

    def __init__(self, table='pes'):
        self.index = 0
        self.list = [layout.Layout(False, label='us'), layout.Layout(True, table, 'ir')]

    def layouts(self):
        return self.list

    def current_index(self):
        return self.index

    def select(self, index, layouts):
        self.index = index
        return True


class Screen:
    """متنی که برنامهٔ فعال نشان می‌دهد، با توجه به چیدمان فعال و Shift/Caps."""

    def __init__(self, backend, table):
        self.backend = backend
        self.table = keymaps.persian_table(table)
        self.text = ''
        self.shift = False
        self.caps = False
        self.ctrl = False

    def press(self, code):
        if code in keymaps.MODIFIERS or self.ctrl:
            return  # Ctrl+حرف یک میان‌بر است، نه تایپ
        if code == keymaps.KEY_BACKSPACE:
            self.text = self.text[:-1]
            return
        if code == keymaps.KEY_SPACE:
            self.text += keymaps.ZWNJ if (self.shift and self.backend.index == 1) else ' '
            return
        if code == keymaps.KEY_ENTER:
            self.text += '\n'
            return
        if code not in keymaps.CHAR_KEYS:
            return
        if self.backend.index == 1:
            self.text += self.table[code][1 if self.shift else 0]
        else:
            plain, upper = keymaps.US_BY_CODE[code]
            self.text += upper if (self.shift != self.caps if plain.isalpha() else self.shift) else plain


class Harness:
    def __init__(self, table='pes', start_persian=False):
        mapper.configure(table)
        self.backend = FakeBackend(table)
        self.backend.index = 1 if start_persian else 0
        self.screen = Screen(self.backend, table)
        vkbd = FakeKeyboard(self.screen)
        # Shift روی کیبورد مجازی روی صفحه اثر دارد
        original_emit = vkbd.emit

        def emit(code, value):
            if code in keymaps.SHIFT_KEYS:
                self.screen.shift = bool(value)
            if code == keymaps.KEY_LEFTCTRL:
                self.screen.ctrl = bool(value)
            original_emit(code, value)
        vkbd.emit = emit
        self.vkbd = vkbd
        self.tracker = layout.LayoutTracker(self.backend)
        self.corrector = Corrector(vkbd, self.tracker, toggle_key=keymaps.KEY_F10)
        self.corrector.set_persian_table(table)

    def key(self, code, value=None):
        values = [value] if value is not None else [1, 0]
        for v in values:
            if self.corrector.on_key(code, v, grabbed=True):
                self.vkbd.emit(code, v)

    def type_physical(self, keys):
        """کلیدها را با نام کلید US می‌زند (حروف بزرگ = با Shift)."""
        for ch in keys:
            if ch == ' ':
                self.key(keymaps.KEY_SPACE)
            elif ch == '\n':
                self.key(keymaps.KEY_ENTER)
            elif ch.isupper():
                self.key(keymaps.KEY_LEFTSHIFT, 1)
                self.key(US_CODE[ch.lower()])
                self.key(keymaps.KEY_LEFTSHIFT, 0)
            else:
                self.key(US_CODE[ch])


class CorrectorTest(unittest.TestCase):
    def test_persian_typed_on_english(self):
        h = Harness()
        h.type_physical('sghl ')
        self.assertEqual(h.screen.text, 'سلام ')
        self.assertEqual(h.backend.index, 1)

    def test_english_typed_on_persian(self):
        h = Harness(start_persian=True)
        h.type_physical('hello ')
        self.assertEqual(h.screen.text, 'hello ')
        self.assertEqual(h.backend.index, 0)

    def test_sentence_keeps_layout(self):
        h = Harness()
        h.type_physical('sghl ofdv ')
        # بعد از اصلاح اول چیدمان فارسی است، پس کلمهٔ دوم درست تایپ می‌شود
        self.assertEqual(h.screen.text, 'سلام خبیر ')

    def test_no_change_for_english(self):
        h = Harness()
        h.type_physical('hello world ')
        self.assertEqual(h.screen.text, 'hello world ')
        self.assertEqual(h.backend.index, 0)

    def test_enter_delimiter(self):
        h = Harness()
        h.type_physical('sghl\n')
        self.assertEqual(h.screen.text, 'سلام\n')

    def test_rtl_space_fix(self):
        # کلمهٔ دوم فارسی بعد از Space: فاصلهٔ قبلی هم دوباره تایپ می‌شود
        h = Harness(start_persian=True)
        h.type_physical('hello ')
        h.type_physical('sghl ')
        self.assertEqual(h.screen.text, 'hello سلام ')

    def test_backspace(self):
        h = Harness()
        h.type_physical('sghx')
        h.key(keymaps.KEY_BACKSPACE)
        h.type_physical('l ')
        self.assertEqual(h.screen.text, 'سلام ')

    def test_ctrl_resets(self):
        h = Harness()
        h.type_physical('sg')
        h.key(keymaps.KEY_LEFTCTRL, 1)
        h.key(US_CODE['v'])
        h.key(keymaps.KEY_LEFTCTRL, 0)
        h.type_physical('x ')
        self.assertEqual(h.screen.text, 'sgx ')

    def test_toggle(self):
        h = Harness()
        h.key(keymaps.KEY_F10)
        self.assertFalse(h.corrector.enabled)
        h.type_physical('sghl ')
        self.assertEqual(h.screen.text, 'sghl ')
        h.key(keymaps.KEY_F10)
        self.assertTrue(h.corrector.enabled)

    def test_shift_letter(self):
        # «آقا» روی چیدمان pes با Shift+H.
        # فاصلهٔ اول مرزِ کلمه را معلوم می‌کند؛ بی‌آن، کلمهٔ سه‌حرفی مثل نسخهٔ ویندوز به‌عنوان
        # تکهٔ ناتمام رد می‌شود (test_short_word_unknown_start_at_startup)
        h = Harness()
        h.type_physical(' Hrh ')
        self.assertEqual(h.screen.text, ' آقا ')

    def test_caps_lock_english_output(self):
        h = Harness(start_persian=True)
        h.corrector.caps = True
        h.screen.caps = True
        h.type_physical('hello ')
        # خروجی دقیقاً همان کلمهٔ اصلاح‌شده است، مستقل از Caps Lock
        self.assertEqual(h.screen.text, 'hello ')

    def test_user_word(self):
        user_words.add('GitHub')
        try:
            h = Harness(start_persian=True)
            h.type_physical('github ')
            self.assertEqual(h.screen.text, 'GitHub ')
        finally:
            user_words.remove('GitHub')

    def test_winkeys_layout(self):
        h = Harness('winkeys')
        h.type_physical('\\dhl ')  # «پیام» روی چیدمان ویندوز
        self.assertEqual(h.screen.text, 'پیام ')

    def test_digits_untouched(self):
        h = Harness()
        h.type_physical('sghl123 ')
        self.assertEqual(h.screen.text, 'sghl123 ')

    # ------------------------------------------------ آغازِ کلمه معلوم است یا نه

    def test_short_word_unknown_start_at_startup(self):
        # برنامه تازه بالا آمده: نمی‌دانیم پیش از آنچه کاربر می‌نویسد فاصله بوده یا حرفی از
        # کلمه‌ای نیمه‌تمام، پس تکهٔ کوتاه دست‌نخورده می‌ماند
        h = Harness()
        h.type_physical('Hrh ')
        self.assertEqual(h.screen.text, 'Hrh ')

    def test_whole_word_fixed_even_with_unknown_start(self):
        # کلمهٔ کامل شاهدِ قطعی است و با نامعلوم‌بودنِ آغازِ کلمه هم اصلاح می‌شود
        h = Harness()
        h.type_physical('sghl ')
        self.assertEqual(h.screen.text, 'سلام ')

    def test_mouse_click_invalidates_buffer(self):
        # کلیک می‌تواند مکان‌نما را وسط یا انتهای کلمه‌ای که از قبل نوشته شده ببرد؛
        # از آن پس بافر با متنِ پیشِ مکان‌نما نمی‌خواند
        h = Harness()
        h.type_physical(' sg')
        h.corrector.invalidate_buffer()
        h.type_physical('hl ')
        self.assertEqual(h.screen.text, ' sghl ')

    def test_mouse_click_applied_on_next_key_only(self):
        # هندلرِ ماوس فقط پرچم ست می‌کند؛ بافر در کلیدِ بعدی دور ریخته می‌شود
        h = Harness()
        h.type_physical(' sg')
        h.corrector.invalidate_buffer()
        self.assertEqual(len(h.corrector._buffer), 2)
        h.type_physical('h')
        self.assertEqual(len(h.corrector._buffer), 1)

    def test_arrow_key_invalidates_buffer(self):
        h = Harness()
        h.type_physical(' sg')
        h.key(keymaps.KEY_LEFT)
        h.key(keymaps.KEY_RIGHT)
        h.type_physical('hl ')
        self.assertEqual(h.screen.text, ' sghl ')

    def test_delete_key_invalidates_buffer(self):
        h = Harness()
        h.type_physical(' sg')
        h.key(keymaps.KEY_DELETE)
        h.type_physical('hl ')
        self.assertEqual(h.screen.text, ' sghl ')

    def test_backspace_past_buffer_makes_start_unknown(self):
        # حرفی که ما نشمرده‌ایم پاک شده: نه می‌دانیم چه بوده و نه چه چیزی پیش‌تر از آن مانده
        h = Harness()
        h.type_physical(' ')
        h.key(keymaps.KEY_BACKSPACE)
        h.type_physical('Hrh ')
        self.assertEqual(h.screen.text, 'Hrh ')

    def test_language_switch_shortcut_keeps_buffer(self):
        # Alt+Shift (تعویض زبان) فقط modifier است و نباید بافر را پاک کند
        h = Harness()
        h.type_physical(' sg')
        for code in (keymaps.KEY_LEFTALT, keymaps.KEY_LEFTSHIFT):
            h.key(code, 1)
        for code in (keymaps.KEY_LEFTSHIFT, keymaps.KEY_LEFTALT):
            h.key(code, 0)
        h.type_physical('hl ')
        self.assertEqual(h.screen.text, ' سلام ')

    def test_caret_keys_are_known(self):
        # هر کلیدِ CARET_KEYS از شاخهٔ ریست عبور می‌کند (هیچ‌کدام کلیدِ حرفی نیست)
        self.assertFalse(keymaps.CARET_KEYS & keymaps.CHAR_KEYS)
        self.assertFalse(keymaps.CARET_KEYS & keymaps.MODIFIERS)


if __name__ == '__main__':
    unittest.main()
