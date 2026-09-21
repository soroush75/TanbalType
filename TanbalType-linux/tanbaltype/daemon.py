# -*- coding: utf-8 -*-
"""سرویس اصلی: خواندن کیبوردها، عبور دادن کلیدها از کیبورد مجازی، و فرمان‌های کنترلی (socket)."""
import errno
import fcntl
import os
import select
import signal
import socket
import time

from . import applog, evdev, layout, notify, paths
from .corrector import FUNCTION_KEYS, Corrector

EVIOCGKEY = evdev._ioc(2, 'E', 0x18, evdev.KEY_CNT // 8)
RESCAN_INTERVAL = 2.0


class PermissionProblem(Exception):
    pass


def _keys_down(dev):
    buf = bytearray(evdev.KEY_CNT // 8)
    try:
        fcntl.ioctl(dev.fd, EVIOCGKEY, buf, True)
    except OSError:
        return False
    return any(buf)


class Daemon:
    def __init__(self, config):
        self.config = config
        self.devices = {}          # path → InputDevice
        self._pending_grab = set()  # کیبوردهایی که منتظر رها شدن همهٔ کلیدها هستند
        self._grab_retry = set()    # grab ناموفق (مثلاً در دست نشست کاربر دیگر)؛ بعداً دوباره
        self._led_seen = False
        self._ignored = set()
        self._clients = []
        self._running = True
        self._last_scan = 0.0
        self._last_layout_check = 0.0

        try:
            self.vkbd = evdev.VirtualKeyboard()
        except OSError as exc:
            raise PermissionProblem('/dev/uinput: %s' % exc.strerror)

        toggle = FUNCTION_KEYS.get(config.get('toggle_key', 'F10').strip().upper())
        self.notify_enabled = config.getboolean('notifications', True)
        self.corrector = Corrector(self.vkbd, None, toggle_key=toggle, on_toggle=self._toggled)
        backend = layout.detect_backend(config.get('backend', 'auto').strip().lower(),
                                        self.corrector.press_shortcut)
        self.tracker = layout.LayoutTracker(backend)
        self.corrector.tracker = self.tracker
        applog.write('Layout backend: %s, layouts: %s' % (backend.name, self.tracker.layouts()))
        if backend.name == 'none':
            applog.error('could not detect how to switch keyboard layouts in this desktop; '
                         'set "backend" in %s' % paths.CONFIG_PATH)
            notify.send(paths.APP_NAME, 'روش تعویض زبان کیبورد در این محیط شناخته نشد.\n'
                        'در تنظیمات کیبورد یک چیدمان فارسی و یک انگلیسی اضافه کنید.')
        elif self.tracker.persian_table() is None:
            applog.error('no Persian keyboard layout is enabled')
            notify.send(paths.APP_NAME, 'هیچ چیدمان فارسی‌ای فعال نیست.\n'
                        'در تنظیمات کیبورد (Input Sources) چیدمان Persian را اضافه کنید.')
        self._update_persian_table()

        self.server = self._listen()

    # ------------------------------------------------------------ راه‌اندازی

    def _listen(self):
        try:
            os.unlink(paths.SOCKET_PATH)
        except OSError:
            pass
        server = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
        server.bind(paths.SOCKET_PATH)
        os.chmod(paths.SOCKET_PATH, 0o600)
        server.listen(4)
        server.setblocking(False)
        return server

    def _update_persian_table(self):
        forced = self.config.get('persian_layout', 'auto').strip().lower()
        name = forced if forced in ('pes', 'winkeys') else (self.tracker.persian_table() or 'pes')
        self.corrector.set_persian_table(name)
        self._last_layout_check = time.monotonic()

    def _scan(self):
        self._last_scan = time.monotonic()
        paths_now = set(evdev.list_event_devices())

        for path in list(self.devices):
            # دستگاه جدا شده، یا کاربر دیگری وارد شده و دسترسی گرفته شده (تعویض کاربر)
            if path not in paths_now or not os.access(path, os.R_OK):
                self._drop(path)
        self._ignored &= paths_now

        for path in sorted(paths_now - set(self.devices) - self._ignored):
            try:
                dev = evdev.InputDevice(path)
            except OSError:
                self._ignored.add(path)
                continue
            if dev.name == evdev.VIRTUAL_NAME or not (dev.is_keyboard or dev.is_pointer):
                dev.close()
                self._ignored.add(path)
                continue
            self.devices[path] = dev
            if dev.is_pure_keyboard:
                self._pending_grab.add(path)
            applog.write('Device %s "%s" keyboard=%s pointer=%s' % (path, dev.name, dev.is_keyboard, dev.is_pointer))
        self._pending_grab |= self._grab_retry & set(self.devices)
        self._grab_retry.clear()
        self._sync_caps()
        self._try_grab()

    def _sync_caps(self):
        # تا وقتی compositor چراغ‌ها را روی کیبورد مجازی تنظیم نکرده، از چراغ کیبورد واقعی
        if self._led_seen:
            return
        for dev in self.keyboards():
            caps = dev.caps_lock_on()
            if caps is not None:
                self.corrector.caps = caps
                return

    def _try_grab(self):
        for path in list(self._pending_grab):
            dev = self.devices.get(path)
            if dev is None:
                self._pending_grab.discard(path)
                continue
            # اگر وسط فشردن کلیدی grab کنیم، رها شدن آن کلید به سیستم نمی‌رسد و کلید «گیر» می‌کند
            if _keys_down(dev):
                continue
            try:
                dev.grab()
                applog.write('Grabbed %s "%s"' % (path, dev.name))
            except OSError as exc:
                applog.write('Grab %s failed (%s); listening only for now' % (path, exc))
                self._grab_retry.add(path)
            self._pending_grab.discard(path)

    def _drop(self, path):
        dev = self.devices.pop(path, None)
        self._pending_grab.discard(path)
        self._grab_retry.discard(path)
        if dev:
            applog.write('Device removed %s' % path)
            dev.close()

    def keyboards(self):
        return [d for d in self.devices.values() if d.is_keyboard]

    # ------------------------------------------------------------ حلقهٔ اصلی

    def run(self):
        self._scan()
        if not self.keyboards():
            raise PermissionProblem('no readable keyboard in /dev/input')
        applog.write('Keyboards: %s' % ', '.join('"%s"%s' % (d.name, '' if d.grabbed else ' (listen)')
                                                 for d in self.keyboards()))

        while self._running:
            readers = list(self.devices.values()) + [self.vkbd, self.server] + self._clients
            timeout = 0.1 if self._pending_grab else RESCAN_INTERVAL
            try:
                ready, _, _ = select.select(readers, [], [], timeout)
            except InterruptedError:
                continue
            except (OSError, ValueError):
                # یکی از دستگاه‌ها همین حالا بسته شده
                self._scan()
                continue

            for obj in ready:
                if obj is self.server:
                    self._accept()
                elif obj is self.vkbd:
                    self._forward_leds()
                elif isinstance(obj, socket.socket):
                    self._serve(obj)
                else:
                    self._read_device(obj)

            now = time.monotonic()
            if self._pending_grab:
                self._try_grab()
            if now - self._last_scan >= RESCAN_INTERVAL:
                self._scan()
            if now - self._last_layout_check >= 60:
                self._update_persian_table()

    def _read_device(self, dev):
        try:
            events = dev.read()
        except OSError as exc:
            if exc.errno in (errno.ENODEV, errno.EBADF, errno.EIO):
                self._drop(dev.path)
                return
            raise
        for kind, code, value in events:
            if kind != evdev.EV_KEY:
                continue
            if 0x100 <= code < 0x160:
                # دکمه‌های موس/تاچ‌پد
                if value == 1:
                    self.corrector.reset_pointer()
                continue
            if not dev.is_keyboard:
                continue
            forward = self.corrector.on_key(code, value, grabbed=dev.grabbed)
            if forward and dev.grabbed:
                self.vkbd.emit(code, value)

    def _forward_leds(self):
        # compositor چراغ‌ها (Caps/Num Lock) را روی کیبورد مجازی روشن می‌کند؛ به کیبورد واقعی منتقل می‌کنیم
        for code, value in self.vkbd.read_leds():
            if code == evdev.LED_CAPSL:
                self._led_seen = True
                self.corrector.caps = bool(value)
            for dev in self.devices.values():
                if dev.grabbed:
                    dev.write_led(code, value)

    # ------------------------------------------------------------ فرمان‌ها

    def _toggled(self, enabled):
        if self.notify_enabled:
            notify.send(paths.APP_NAME, 'فعال شد' if enabled else 'غیرفعال شد')

    def _accept(self):
        try:
            conn, _ = self.server.accept()
        except OSError:
            return
        conn.settimeout(1.0)
        self._clients.append(conn)

    def _serve(self, conn):
        self._clients.remove(conn)
        try:
            request = conn.recv(1024).decode('utf-8', 'replace').strip()
            reply = self.command(request)
            conn.sendall((reply + '\n').encode('utf-8'))
        except OSError:
            pass
        finally:
            conn.close()

    def command(self, request):
        c = self.corrector
        if request == 'status':
            current = self.tracker.current(0.3)
            return ('enabled=%d log=%d backend=%s persian_layout=%s current=%s keyboards=%s'
                    % (c.enabled, applog.enabled, self.tracker.backend.name, c.persian_table_name,
                       current, ','.join('%s%s' % (d.name.replace(' ', '_').replace(',', ''),
                                                   '' if d.grabbed else '(listen)')
                                         for d in self.keyboards())))
        if request in ('enable', 'disable', 'toggle'):
            enabled = not c.enabled if request == 'toggle' else request == 'enable'
            c.set_enabled(enabled)
            return 'enabled=%d' % enabled
        if request in ('log-on', 'log-off'):
            applog.enabled = request == 'log-on'
            if applog.enabled:
                applog.write_header()
                applog.write('Log saving enabled by user. Backend: %s, layouts: %s, key map: %s'
                             % (self.tracker.backend.name, self.tracker.layouts(0), c.persian_table_name))
                from . import selftest
                selftest.run([c.persian_table_name])
            return 'log=%d' % applog.enabled
        if request in ('suspend', 'resume'):
            c.suspended = request == 'suspend'
            c.reset()
            return 'suspended=%d' % c.suspended
        if request == 'quit':
            self._running = False
            return 'bye'
        if request == 'ping':
            return 'pong'
        return 'error: unknown command'

    def stop(self, *_):
        self._running = False

    def close(self):
        for path in list(self.devices):
            self._drop(path)
        try:
            self.vkbd.close()
        except OSError:
            pass
        try:
            self.server.close()
            os.unlink(paths.SOCKET_PATH)
        except OSError:
            pass
        applog.write('Service exit.')


def send_command(request, timeout=2.0):
    """فرستادن فرمان به سرویس در حال اجرا. خروجی: پاسخ، یا None اگر سرویس اجرا نیست."""
    sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    sock.settimeout(timeout)
    try:
        sock.connect(paths.SOCKET_PATH)
        sock.sendall((request + '\n').encode('utf-8'))
        data = b''
        while not data.endswith(b'\n'):
            chunk = sock.recv(4096)
            if not chunk:
                break
            data += chunk
        return data.decode('utf-8', 'replace').strip()
    except OSError:
        return None
    finally:
        sock.close()


PERMISSION_HELP = '''TanbalType به کیبورد دسترسی ندارد ({detail}).
برای رفع مشکل یک بار از سیستم خارج و دوباره وارد شوید (Log out / Log in).
اگر مشکل ماند، این دستور را اجرا کنید و دوباره وارد شوید:
    sudo tanbaltype setup'''


def run(config):
    # دو نسخهٔ هم‌زمان هر کلمه را دو بار اصلاح می‌کنند
    if send_command('ping', 0.5) == 'pong':
        applog.error('already running')
        return 0

    try:
        daemon = Daemon(config)
    except PermissionProblem as exc:
        message = PERMISSION_HELP.format(detail=exc)
        applog.error(message)
        notify.send(paths.APP_NAME, message)
        return 2

    signal.signal(signal.SIGTERM, daemon.stop)
    signal.signal(signal.SIGINT, daemon.stop)
    signal.signal(signal.SIGHUP, daemon.stop)

    # لغت‌نامه (~۳۳۰ هزار کلمه) قبل از اولین کلمه بارگذاری می‌شود
    from . import detector
    detector.persian_words()

    try:
        daemon.run()
    except PermissionProblem as exc:
        message = PERMISSION_HELP.format(detail=exc)
        applog.error(message)
        notify.send(paths.APP_NAME, message)
        return 2
    finally:
        daemon.close()
    return 0
