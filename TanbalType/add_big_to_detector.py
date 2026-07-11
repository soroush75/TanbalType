# -*- coding: utf-8 -*-
"""
لغت‌نامهٔ فارسی برنامه را از big.txt می‌سازد.

خروجی PersianWords.txt است که به‌صورت EmbeddedResource داخل برنامه قرار می‌گیرد
(دیگر کلمات داخل Detector.cs نوشته نمی‌شوند تا کامپایل و اجرای برنامه سریع بماند).
"""
import re

big_txt_path = 'big.txt'
out_path = 'PersianWords.txt'

words_set = set()

# خواندن فایل و جداسازی کلمات با فاصله و نیم‌فاصله (‌)
with open(big_txt_path, 'r', encoding='utf-8') as f:
    for line in f:
        for p in re.split(r'[ ‌\t\n]+', line.strip()):
            w = p.strip()
            if len(w) < 2:
                continue  # کلمات تک‌حرفی هیچ‌وقت بررسی نمی‌شوند
            if any(ord(c) < 128 for c in w):
                continue  # توکن‌های خراب حاوی حروف انگلیسی مثل "]n" یا "b[اسم"
            words_set.add(w)

sorted_words = sorted(words_set)

with open(out_path, 'w', encoding='utf-8', newline='\n') as f:
    f.write('\n'.join(sorted_words))
    f.write('\n')

print(f"عملیات با موفقیت انجام شد! {len(sorted_words)} لغت یکتا در {out_path} نوشته شد.")
