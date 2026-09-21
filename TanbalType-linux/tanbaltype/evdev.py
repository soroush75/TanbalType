# -*- coding: utf-8 -*-
"""دسترسی مستقیم به کیبورد از طریق هستهٔ لینوکس (بدون هیچ کتابخانهٔ جانبی).

* خواندن: /dev/input/event*  (مستقل از X11/Wayland/کنسول)
* نوشتن:  /dev/uinput         (یک کیبورد مجازی که کلیدهای اصلاح‌شده از آن ارسال می‌شود)

کیبوردهای «خالص» grab می‌شوند (EVIOCGRAB): همهٔ کلیدها اول به برنامه می‌رسند و برنامه آن‌ها را از
کیبورد مجازی عبور می‌دهد. این‌طوری می‌توان Space را تا پایان اصلاح نگه داشت (مثل نسخهٔ ویندوز و مک).
اگر برنامه به هر دلیل بسته شود، هسته grab را خودکار آزاد می‌کند و کیبورد عادی کار می‌کند.
"""
import errno
import fcntl
import os
import struct
import time

EV_SYN = 0x00
EV_KEY = 0x01
EV_REL = 0x02
EV_ABS = 0x03
EV_MSC = 0x04
EV_LED = 0x11
SYN_REPORT = 0
SYN_DROPPED = 3
LED_NUML, LED_CAPSL, LED_SCROLLL = 0, 1, 2
BTN_LEFT = 0x110
KEY_CNT = 0x300

# struct input_event: struct timeval (دو long) + u16 type + u16 code + s32 value
EVENT_FORMAT = 'llHHi'
EVENT_SIZE = struct.calcsize(EVENT_FORMAT)

VIRTUAL_NAME = 'TanbalType virtual keyboard'


def _ioc(direction, kind, nr, size):
    return (direction << 30) | (size << 16) | (ord(kind) << 8) | nr


def _eviocgbit(ev, length):
    return _ioc(2, 'E', 0x20 + ev, length)


EVIOCGNAME = _ioc(2, 'E', 0x06, 256)
EVIOCGLED = _ioc(2, 'E', 0x19, 8)
EVIOCGRAB = _ioc(1, 'E', 0x90, 4)

UI_DEV_CREATE = _ioc(0, 'U', 1, 0)
UI_DEV_DESTROY = _ioc(0, 'U', 2, 0)
UI_DEV_SETUP = _ioc(1, 'U', 3, 92)
UI_SET_EVBIT = _ioc(1, 'U', 100, 4)
UI_SET_KEYBIT = _ioc(1, 'U', 101, 4)
UI_SET_LEDBIT = _ioc(1, 'U', 105, 4)

BUS_VIRTUAL = 0x06


def _bits(fd, ev, count):
    buf = bytearray((count + 7) // 8)
    try:
        fcntl.ioctl(fd, _eviocgbit(ev, len(buf)), buf, True)
    except OSError:
        return frozenset()
    return frozenset(i for i in range(count) if buf[i // 8] >> (i % 8) & 1)


class InputDevice:
    """یک دستگاه ورودی /dev/input/eventN."""

    def __init__(self, path):
        self.path = path
        self.writable = True
        try:
            self.fd = os.open(path, os.O_RDWR | os.O_NONBLOCK | os.O_CLOEXEC)
        except PermissionError:
            # نوشتن فقط برای روشن کردن چراغ Caps Lock لازم است
            self.writable = False
            self.fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | os.O_CLOEXEC)
        try:
            name = bytearray(256)
            fcntl.ioctl(self.fd, EVIOCGNAME, name, True)
            self.name = name.split(b'\0', 1)[0].decode('utf-8', 'replace')
        except OSError:
            self.name = '?'
        self.types = _bits(self.fd, 0, 0x20)
        self.keys = _bits(self.fd, EV_KEY, KEY_CNT) if EV_KEY in self.types else frozenset()
        self.grabbed = False
        self._pending = b''

    def fileno(self):
        return self.fd

    @property
    def is_keyboard(self):
        # حروف A و Z و Space و Enter: کیبورد واقعی (نه دکمهٔ پاور یا کنترل صدا)
        return {30, 44, 57, 28} <= self.keys

    @property
    def is_pointer(self):
        return BTN_LEFT in self.keys

    @property
    def is_pure_keyboard(self):
        """کیبوردی که محور حرکتی (موس/تاچ‌پد) ندارد و می‌توان کامل grab کرد."""
        return self.is_keyboard and EV_REL not in self.types and EV_ABS not in self.types

    def grab(self):
        fcntl.ioctl(self.fd, EVIOCGRAB, 1)
        self.grabbed = True

    def caps_lock_on(self):
        buf = bytearray(8)
        try:
            fcntl.ioctl(self.fd, EVIOCGLED, buf, True)
        except OSError:
            return None
        return bool(buf[0] >> LED_CAPSL & 1)

    def read(self):
        """رویدادهای آماده: فهرست (type, code, value). اگر دستگاه جدا شده باشد OSError."""
        try:
            data = self._pending + os.read(self.fd, EVENT_SIZE * 64)
        except BlockingIOError:
            return []
        count = len(data) // EVENT_SIZE
        self._pending = data[count * EVENT_SIZE:]
        events = []
        for i in range(count):
            _, _, kind, code, value = struct.unpack_from(EVENT_FORMAT, data, i * EVENT_SIZE)
            events.append((kind, code, value))
        return events

    def write_led(self, code, value):
        if not self.writable:
            return
        try:
            os.write(self.fd, _pack(EV_LED, code, value) + _pack(EV_SYN, SYN_REPORT, 0))
        except OSError:
            pass

    def close(self):
        try:
            os.close(self.fd)
        except OSError:
            pass
        self.fd = -1


def _pack(kind, code, value):
    now = time.time()
    return struct.pack(EVENT_FORMAT, int(now), int(now % 1 * 1e6), kind, code, value)


def list_event_devices():
    try:
        names = os.listdir('/dev/input')
    except OSError:
        return []
    return sorted(os.path.join('/dev/input', n) for n in names if n.startswith('event'))


def _declared_keys():
    # همهٔ کلیدهای کیبورد، به‌جز محدودهٔ دکمه‌های موس/دسته بازی (BTN_*) تا دستگاه مجازی
    # با موس یا جوی‌استیک اشتباه گرفته نشود
    return [c for c in range(1, KEY_CNT)
            if not (0x100 <= c < 0x160 or 0x220 <= c < 0x224 or c >= 0x2c0)]


class VirtualKeyboard:
    """کیبورد مجازی روی /dev/uinput."""

    def __init__(self):
        self.fd = os.open('/dev/uinput', os.O_RDWR | os.O_NONBLOCK | os.O_CLOEXEC)
        try:
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_KEY)
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_SYN)
            fcntl.ioctl(self.fd, UI_SET_EVBIT, EV_LED)
            for led in (LED_NUML, LED_CAPSL, LED_SCROLLL):
                fcntl.ioctl(self.fd, UI_SET_LEDBIT, led)
            for code in _declared_keys():
                fcntl.ioctl(self.fd, UI_SET_KEYBIT, code)

            name = VIRTUAL_NAME.encode()
            try:
                setup = struct.pack('HHHH80sI', BUS_VIRTUAL, 0x7462, 0x0001, 1, name, 0)
                fcntl.ioctl(self.fd, UI_DEV_SETUP, setup)
            except OSError as exc:
                if exc.errno not in (errno.EINVAL, errno.ENOTTY):
                    raise
                # هسته‌های قدیمی‌تر از 4.5
                legacy = struct.pack('80sHHHHI', name, BUS_VIRTUAL, 0x7462, 0x0001, 1, 0)
                os.write(self.fd, legacy + b'\0' * (4 * 64 * 4))
            fcntl.ioctl(self.fd, UI_DEV_CREATE)
        except OSError:
            os.close(self.fd)
            raise
        self.down = set()  # کلیدهایی که از طریق این دستگاه پایین نگه داشته شده‌اند

    def fileno(self):
        return self.fd

    def emit(self, code, value):
        os.write(self.fd, _pack(EV_KEY, code, value) + _pack(EV_SYN, SYN_REPORT, 0))
        if value:
            self.down.add(code)
        else:
            self.down.discard(code)

    def tap(self, code, delay=0.0015):
        self.emit(code, 1)
        self.emit(code, 0)
        # مکث کوتاه تا برنامه‌های کند (مثلاً Electron) ترتیب رویدادها را گم نکنند
        time.sleep(delay)

    def read_leds(self):
        """رویدادهای LED که compositor به کیبورد مجازی می‌فرستد: فهرست (code, value)."""
        leds = []
        while True:
            try:
                data = os.read(self.fd, EVENT_SIZE * 16)
            except (BlockingIOError, OSError):
                break
            if not data:
                break
            for i in range(len(data) // EVENT_SIZE):
                _, _, kind, code, value = struct.unpack_from(EVENT_FORMAT, data, i * EVENT_SIZE)
                if kind == EV_LED:
                    leds.append((code, value))
        return leds

    def close(self):
        for code in list(self.down):
            try:
                self.emit(code, 0)
            except OSError:
                pass
        try:
            fcntl.ioctl(self.fd, UI_DEV_DESTROY)
        except OSError:
            pass
        os.close(self.fd)
