#!/usr/bin/env python3
"""پس‌زمینهٔ پنجرهٔ فایل نصبی DMG را می‌سازد.

خروجی: Resources/dmg-background.png  و  Resources/dmg-background@2x.png
make-dmg.sh این دو را با tiffutil به یک فایل چنددقتی تبدیل می‌کند تا روی
نمایشگر Retina هم شفاف بماند.

یک گرادیان ملایم است به‌همراه یک فلش از آیکن برنامه به‌سمت میان‌برِ Applications.
متنی روی تصویر نیست: خودِ Finder نام «TanbalType» و «Applications» را زیر
آیکن‌ها نشان می‌دهد، و رسم متن به فونت و رَستِرایزر نیاز داشت.

فقط کتابخانهٔ استاندارد پایتون لازم است (zlib + struct)، پس روی ویندوز و مک
یکسان اجرا می‌شود:

    python tools/gen_dmg_background.py
"""
import math
import os
import struct
import zlib

# اندازهٔ ناحیهٔ محتوای پنجره (point). make-dmg.sh از همین عددها برای
# bounds پنجره و جای آیکن‌ها استفاده می‌کند؛ اگر این‌ها را عوض کردید آنجا هم
# باید عوض شود.
WIDTH, HEIGHT = 640, 400

# مرکز آیکن‌ها (point) — با ICON_X/ICON_Y در make-dmg.sh یکی است.
APP_CENTER = (160, 185)
APPLICATIONS_CENTER = (480, 185)

GRADIENT_TOP = (253, 253, 254)
GRADIENT_BOTTOM = (237, 240, 245)
ARROW_COLOR = (178, 187, 200)

# فلش در فاصلهٔ میان دو آیکن (۱۲۸ پیکسلی) جا می‌شود.
ARROW_SHAFT = ((248, 185), (388, 185))
ARROW_BARBS = (((396, 185), (362, 157)), ((396, 185), (362, 213)))
ARROW_HALF_WIDTH = 4.5


def _distance_to_segment(px, py, x1, y1, x2, y2):
    """کوتاه‌ترین فاصلهٔ نقطه از پاره‌خط."""
    dx, dy = x2 - x1, y2 - y1
    length_squared = dx * dx + dy * dy
    if length_squared == 0.0:
        return math.hypot(px - x1, py - y1)
    t = ((px - x1) * dx + (py - y1) * dy) / length_squared
    t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
    return math.hypot(px - (x1 + t * dx), py - (y1 + t * dy))


def _render(scale):
    """یک بافرِ RGB از تصویر، در مقیاس داده‌شده، برمی‌گرداند."""
    width, height = WIDTH * scale, HEIGHT * scale
    half_width = ARROW_HALF_WIDTH * scale

    segments = [
        (ARROW_SHAFT[0][0] * scale, ARROW_SHAFT[0][1] * scale,
         ARROW_SHAFT[1][0] * scale, ARROW_SHAFT[1][1] * scale)
    ]
    for (x1, y1), (x2, y2) in ARROW_BARBS:
        segments.append((x1 * scale, y1 * scale, x2 * scale, y2 * scale))

    # فلش فقط در این کادر است؛ بیرونش سراغِ محاسبهٔ فاصله نمی‌رویم.
    pad = half_width + 2.0
    box_left = min(min(s[0], s[2]) for s in segments) - pad
    box_right = max(max(s[0], s[2]) for s in segments) + pad
    box_top = min(min(s[1], s[3]) for s in segments) - pad
    box_bottom = max(max(s[1], s[3]) for s in segments) + pad

    rows = []
    for y in range(height):
        # گرادیان عمودی
        ratio = y / (height - 1) if height > 1 else 0.0
        base = bytes(
            int(round(GRADIENT_TOP[i] + (GRADIENT_BOTTOM[i] - GRADIENT_TOP[i]) * ratio))
            for i in range(3)
        )
        row = bytearray(base * width)

        if box_top <= y + 0.5 <= box_bottom:
            py = y + 0.5
            x_from = max(0, int(box_left))
            x_to = min(width - 1, int(box_right) + 1)
            for x in range(x_from, x_to + 1):
                px = x + 0.5
                distance = min(_distance_to_segment(px, py, *s) for s in segments)
                # لبهٔ نرم: یک پیکسل محدوده برای ضدِ پله‌شدگی
                alpha = half_width + 0.5 - distance
                if alpha <= 0.0:
                    continue
                if alpha > 1.0:
                    alpha = 1.0
                offset = x * 3
                for i in range(3):
                    under = row[offset + i]
                    row[offset + i] = int(round(under + (ARROW_COLOR[i] - under) * alpha))
        rows.append(bytes(row))
    return width, height, rows


def _png(width, height, rows):
    """بایت‌های یک PNG هشت‌بیتیِ RGB."""
    raw = b''.join(b'\x00' + row for row in rows)  # filter 0 در ابتدای هر سطر

    def chunk(kind, data):
        return (struct.pack('>I', len(data)) + kind + data
                + struct.pack('>I', zlib.crc32(kind + data) & 0xFFFFFFFF))

    header = struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0)
    return (b'\x89PNG\r\n\x1a\n'
            + chunk(b'IHDR', header)
            + chunk(b'IDAT', zlib.compress(raw, 9))
            + chunk(b'IEND', b''))


def main():
    resources = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Resources')
    resources = os.path.normpath(resources)
    os.makedirs(resources, exist_ok=True)

    for scale, name in ((1, 'dmg-background.png'), (2, 'dmg-background@2x.png')):
        width, height, rows = _render(scale)
        path = os.path.join(resources, name)
        with open(path, 'wb') as handle:
            handle.write(_png(width, height, rows))
        print('%s  (%dx%d)' % (path, width, height))


if __name__ == '__main__':
    main()
