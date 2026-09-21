# -*- coding: utf-8 -*-
"""آزمون حلقهٔ اصلی سرویس با دستگاه‌های ساختگی (pipe به‌جای /dev/input) و socket واقعی."""
import configparser
import os
import struct
import sys
import tempfile
import threading
import time
import unittest

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
os.environ['XDG_CONFIG_HOME'] = tempfile.mkdtemp()
os.environ['XDG_RUNTIME_DIR'] = tempfile.mkdtemp()

from tanbaltype import daemon, evdev, keymaps, layout, paths  # noqa: E402
from test_corrector import US_CODE, FakeBackend, Screen  # noqa: E402

paths.SOCKET_PATH = paths.runtime_path('tanbaltype.sock')


class FakeDevice:
    def __init__(self, path, name, keyboard=True, pure=True):
        self.path = path
        self.name = name
        self.is_keyboard = keyboard
        self.is_pointer = not pure
        self.is_pure_keyboard = keyboard and pure
        self.grabbed = False
        self.writable = True
        self.r, self.w = os.pipe()
        os.set_blocking(self.r, False)
        self.fd = self.r

    def fileno(self):
        return self.r

    def grab(self):
        self.grabbed = True

    def caps_lock_on(self):
        return False

    def send(self, code, value):
        os.write(self.w, struct.pack(evdev.EVENT_FORMAT, 0, 0, evdev.EV_KEY, code, value)
                 + struct.pack(evdev.EVENT_FORMAT, 0, 0, evdev.EV_SYN, 0, 0))

    def read(self):
        try:
            data = os.read(self.r, 4096)
        except BlockingIOError:
            return []
        return [struct.unpack_from(evdev.EVENT_FORMAT, data, i)[2:]
                for i in range(0, len(data), evdev.EVENT_SIZE)]

    def write_led(self, code, value):
        pass

    def close(self):
        pass


class FakeVirtualKeyboard:
    screen = None

    def __init__(self):
        self.down = set()
        self.r, self.w = os.pipe()

    def fileno(self):
        return self.r

    def emit(self, code, value):
        if value:
            self.down.add(code)
        else:
            self.down.discard(code)
        if code in keymaps.SHIFT_KEYS:
            self.screen.shift = bool(value)
        elif value:
            self.screen.press(code)

    def tap(self, code, delay=0):
        self.emit(code, 1)
        self.emit(code, 0)

    def read_leds(self):
        return []

    def close(self):
        pass


class DaemonTest(unittest.TestCase):
    def setUp(self):
        self.backend = FakeBackend()
        self.screen = Screen(self.backend, 'pes')
        FakeVirtualKeyboard.screen = self.screen
        self.kbd = FakeDevice('/dev/input/event3', 'AT Translated Set 2 keyboard')
        self.combo = FakeDevice('/dev/input/event5', 'Logitech K400', pure=False)
        devices = {d.path: d for d in (self.kbd, self.combo)}

        self._saved = (evdev.VirtualKeyboard, evdev.InputDevice, evdev.list_event_devices,
                       layout.detect_backend, daemon._keys_down)
        evdev.VirtualKeyboard = FakeVirtualKeyboard
        evdev.InputDevice = lambda path: devices[path]
        evdev.list_event_devices = lambda: sorted(devices)
        layout.detect_backend = lambda preferred, press: self.backend
        daemon._keys_down = lambda dev: False

        config = configparser.ConfigParser()
        config['tanbaltype'] = dict(paths.DEFAULT_CONFIG)
        config['tanbaltype']['notifications'] = 'false'
        self.daemon = daemon.Daemon(config['tanbaltype'])
        self.thread = threading.Thread(target=self._run, daemon=True)
        self.thread.start()
        self._wait(lambda: self.kbd.grabbed)

    def _run(self):
        try:
            self.daemon.run()
        finally:
            self.daemon.close()

    def tearDown(self):
        daemon.send_command('quit')
        self.thread.join(5)
        (evdev.VirtualKeyboard, evdev.InputDevice, evdev.list_event_devices,
         layout.detect_backend, daemon._keys_down) = self._saved

    def _wait(self, cond, timeout=3.0):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            if cond():
                return True
            time.sleep(0.01)
        self.fail('timeout')

    def type(self, dev, keys):
        for ch in keys:
            code = keymaps.KEY_SPACE if ch == ' ' else US_CODE[ch]
            dev.send(code, 1)
            dev.send(code, 0)
            time.sleep(0.01)

    def test_grab_and_correct(self):
        self.assertTrue(self.kbd.grabbed)
        self.assertFalse(self.combo.grabbed)  # کیبورد + تاچ‌پد: فقط شنیده می‌شود
        self.type(self.kbd, 'sghl ')
        self._wait(lambda: self.screen.text == 'سلام ')

    def test_listen_only_keyboard(self):
        # کیبورد grab نشده: کلیدها مستقیم به سیستم می‌رسند؛ اینجا خودمان شبیه‌سازی می‌کنیم
        for ch in 'sghl ':
            code = keymaps.KEY_SPACE if ch == ' ' else US_CODE[ch]
            self.screen.press(code)
            self.combo.send(code, 1)
            self.combo.send(code, 0)
            time.sleep(0.01)
        self._wait(lambda: self.screen.text == 'سلام ')

    def test_commands(self):
        self.assertIn('enabled=1', daemon.send_command('status'))
        self.assertEqual(daemon.send_command('toggle'), 'enabled=0')
        self.type(self.kbd, 'sghl ')
        self._wait(lambda: self.screen.text == 'sghl ')
        self.assertEqual(daemon.send_command('enable'), 'enabled=1')

    def test_f10(self):
        self.kbd.send(keymaps.KEY_F10, 1)
        self.kbd.send(keymaps.KEY_F10, 0)
        self._wait(lambda: not self.daemon.corrector.enabled)


if __name__ == '__main__':
    unittest.main()
