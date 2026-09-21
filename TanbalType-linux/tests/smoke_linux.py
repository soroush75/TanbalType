# -*- coding: utf-8 -*-
"""آزمون دود روی لینوکس واقعی: ساخت کیبورد مجازی (uinput)، پیدا کردنش در /dev/input،
grab کردن، و خواندن کلیدهایی که از آن فرستاده می‌شود. ساختارهای ioctl هسته را واقعاً می‌آزماید.

    sudo python3 tests/smoke_linux.py      (یا بدون sudo اگر قانون udev نصب شده باشد)
"""
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from tanbaltype import evdev, keymaps  # noqa: E402


def main():
    vkbd = evdev.VirtualKeyboard()
    try:
        dev = None
        for _ in range(50):
            for path in evdev.list_event_devices():
                try:
                    d = evdev.InputDevice(path)
                except OSError:
                    continue
                if d.name == evdev.VIRTUAL_NAME:
                    dev = d
                    break
                d.close()
            if dev:
                break
            time.sleep(0.1)
        assert dev, 'virtual keyboard did not appear in /dev/input'
        print('found %s "%s" keyboard=%s pure=%s' % (dev.path, dev.name, dev.is_keyboard, dev.is_pure_keyboard))
        assert dev.is_keyboard and dev.is_pure_keyboard

        dev.grab()
        print('grab OK, caps lock LED:', dev.caps_lock_on())
        codes = [keymaps.KEY_A, keymaps.KEY_SPACE, keymaps.KEY_BACKSPACE]
        for c in codes:
            vkbd.tap(c)
        time.sleep(0.1)
        got = [(code, value) for kind, code, value in dev.read() if kind == evdev.EV_KEY]
        expected = [(c, v) for c in codes for v in (1, 0)]
        print('events:', got)
        assert got == expected, 'expected %s' % expected
        dev.close()
    finally:
        vkbd.close()
    print('SMOKE TEST OK')


if __name__ == '__main__':
    main()
