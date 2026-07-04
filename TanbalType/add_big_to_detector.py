import re

big_txt_path = 'big.txt'
detector_path = 'Detector.cs'

words_set = set()

# خواندن فایل و جداسازی کلمات با فاصله و نیم‌فاصله (\u200c)
with open(big_txt_path, 'r', encoding='utf-8') as f:
    for line in f:
        parts = re.split(r'[ \u200c\t\n]+', line.strip())
        for p in parts:
            clean_word = p.strip()
            if clean_word:
                words_set.add(clean_word)

sorted_words = sorted(list(words_set))

# فرمت کردن به شکل کدهای سی‌شارپ
formatted_words = []
for i in range(0, len(sorted_words), 10):
    chunk = ", ".join(f'"{w}"' for w in sorted_words[i:i+10])
    formatted_words.append("        " + chunk)

hashset_content = "private static readonly HashSet<string> PersianWords = new(StringComparer.Ordinal)\n    {\n"
hashset_content += ",\n".join(formatted_words)
hashset_content += "\n    };"

# جایگزینی در فایل اصلی
with open(detector_path, 'r', encoding='utf-8') as f:
    content = f.read()

pattern = r'private static readonly HashSet<string> PersianWords = new\(StringComparer\.Ordinal\)\s*\{.*?\};'
new_content = re.sub(pattern, hashset_content, content, flags=re.DOTALL)

with open(detector_path, 'w', encoding='utf-8') as f:
    f.write(new_content)

print(f"عملیات با موفقیت انجام شد! {len(words_set)} لغت یکتا به فایل Detector.cs اضافه شد.")