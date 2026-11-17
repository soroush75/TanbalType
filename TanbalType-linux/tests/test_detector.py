# -*- coding: utf-8 -*-
"""آزمون تشخیص وقتی «آغازِ کلمه» نامعلوم است (پورت EnFragments/FaFragments/Unanchored در
DetectorSelfTest.cs).

کاربر پنجره را عوض کرده، با ماوس جای مکان‌نما را برده، کلیدِ جهتی زده یا برنامه تازه بالا آمده؛
پس نمی‌دانیم پیش از آنچه می‌نویسد فاصله بوده یا حرفی از کلمه‌ای نیمه‌تمام. در آن حالت تکهٔ
پایانیِ یک کلمه نباید اصلاح شود، ولی کلمهٔ کاملی که روی کادرِ خالی نوشته شده باید اصلاح شود.

    python3 -m unittest discover -s tests
"""
import os
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.environ.setdefault('XDG_CONFIG_HOME', tempfile.mkdtemp())

from tanbaltype import detector, mapper  # noqa: E402

DATA = detector.DATA


class UnanchoredDetectionTest(unittest.TestCase):
    def setUp(self):
        # کلیدهای فهرست‌ها برای چیدمان ویندوز نوشته شده‌اند
        mapper.use_windows_layout()

    def test_english_fragments_not_fixed(self):
        """«ing» (در engineering)، «nd»، «ther» و ... تصویرِ فارسی دارند ولی تکه‌اند."""
        for frag in DATA['enFragmentsShouldNotFix']:
            with self.subTest(frag=frag):
                self.assertIsNone(detector.detect_wrong_layout(frag, False, False))

    def test_persian_fragments_not_fixed(self):
        """«اث» (در «احداث»)، «اشقی» (در «عاشقی») و ... تصویرِ انگلیسی دارند ولی تکه‌اند."""
        for frag in DATA['faFragmentsShouldNotFix']:
            with self.subTest(frag=frag):
                self.assertIsNone(detector.detect_wrong_layout(frag, True, False))

    def test_whole_words_still_fixed(self):
        """کلمهٔ کامل («sghl» → «سلام») با نامعلوم‌بودنِ آغازِ کلمه هم اصلاح می‌شود."""
        for keys in DATA['unanchoredShouldStillFix']:
            expected = mapper.en_keys_to_persian(keys)
            with self.subTest(keys=keys):
                self.assertEqual(detector.detect_wrong_layout(keys, False, False), expected)

    def test_known_word_start_is_the_default(self):
        """با آغازِ کلمهٔ معلوم (حالت پیش‌فرض) همان تکه‌ها مثل قبل اصلاح می‌شوند."""
        self.assertEqual(detector.detect_wrong_layout('nd', False), 'دی')
        self.assertEqual(detector.detect_wrong_layout('ther', False), 'فاثق')
        self.assertEqual(detector.detect_wrong_layout('اث', True), 'he')

    def test_shape_that_no_english_word_has(self):
        """«o,f» («خوب») کوتاه است ولی ویرگول وسطِ کلمهٔ انگلیسی دیده نمی‌شود، پس ایمن است."""
        self.assertEqual(detector.detect_wrong_layout('o,f', False, False), 'خوب')
        # همان طول، ولی شکلِ یک تکهٔ انگلیسی را دارد
        self.assertIsNone(detector.detect_wrong_layout('ard', False, False))


if __name__ == '__main__':
    unittest.main()
