# -*- coding: utf-8 -*-
"""تشخیص و تعویض زبان کیبورد در محیط‌های مختلف لینوکس.

در لینوکس هر محیط دسکتاپ زبان کیبورد را خودش مدیریت می‌کند، پس برای هر کدام یک «backend» داریم:
  gnome     GNOME / Ubuntu / Budgie / Pantheon (X11 و Wayland)   — gsettings + میان‌بر تعویض زبان
  kde       KDE Plasma (X11 و Wayland)                           — D-Bus: org.kde.keyboard
  sway      Sway                                                 — swaymsg
  hyprland  Hyprland                                             — hyprctl
  x11       هر محیط X11 دیگر (Cinnamon، MATE، XFCE، LXQt، ...)   — XKB از طریق libX11
"""
import ast
import ctypes
import ctypes.util
import json
import os
import re
import shutil
import subprocess
import threading
import time

from . import applog, keymaps

PERSIAN_WORDS = ('persian', 'farsi', 'iran')


def _run(args, timeout=1.5):
    try:
        out = subprocess.run(args, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                             timeout=timeout, check=True)
    except (OSError, subprocess.SubprocessError):
        return None
    return out.stdout.decode('utf-8', 'replace')


def _is_persian_xkb(layout):
    return layout.split('(')[0].split('+')[0].strip().lower() == 'ir'


def _is_persian_name(name):
    lower = name.lower()
    return any(w in lower for w in PERSIAN_WORDS)


class Layout:
    """یک چیدمان: persian=True/False، و برای فارسی نام جدول (pes / winkeys)."""

    def __init__(self, persian, table='pes', label=''):
        self.persian = persian
        self.table = table
        self.label = label

    def __repr__(self):
        return self.label or ('fa' if self.persian else 'en')


class Backend:
    name = 'none'

    def layouts(self):
        """فهرست چیدمان‌های فعال (Layout)."""
        return []

    def current_index(self):
        """اندیس چیدمان فعلی در فهرست layouts()، یا None."""
        return None

    def select(self, index, layouts):
        """تعویض به چیدمان index. خروجی: موفق بود یا نه."""
        return False

    def available(self):
        return True


# ---------------------------------------------------------------- GNOME

class GnomeBackend(Backend):
    """GNOME زبان‌ها را در org.gnome.desktop.input-sources نگه می‌دارد؛ اولین عضو mru-sources
    همیشه زبان فعلی است. GNOME (از نسخهٔ 41) اجازهٔ تعویض مستقیم زبان به برنامه‌ها را نمی‌دهد،
    پس میان‌برِ تعویض زبان (پیش‌فرض Super+Space) از کیبورد مجازی فشرده می‌شود."""
    name = 'gnome'
    SCHEMA = 'org.gnome.desktop.input-sources'

    def __init__(self, press_shortcut):
        self._press_shortcut = press_shortcut

    def available(self):
        return shutil.which('gsettings') is not None and bool(self._sources())

    def _get(self, schema, key):
        out = _run(['gsettings', 'get', schema, key])
        if not out:
            return None
        text = out.strip()
        # قالب GVariant، مثل  @a(ss) []  یا  [('xkb', 'us'), ('xkb', 'ir')]
        text = re.sub(r'^@\S+\s+', '', text)
        try:
            return ast.literal_eval(text)
        except (ValueError, SyntaxError):
            return None

    def _sources(self):
        value = self._get(self.SCHEMA, 'sources')
        return [tuple(s) for s in value] if isinstance(value, list) else []

    @staticmethod
    def _to_layout(source):
        kind, ident = source[0], source[1]
        if kind == 'xkb':
            if _is_persian_xkb(ident):
                variant = ident.split('+', 1)[1] if '+' in ident else ''
                return Layout(True, keymaps.variant_to_table(variant), ident)
            return Layout(False, label=ident)
        # ibus (مثلاً m17n:fa:isiri)
        persian = ':fa' in ident or _is_persian_name(ident)
        return Layout(persian, 'pes', ident)

    def layouts(self):
        self._cached_sources = self._sources()
        return [self._to_layout(s) for s in self._cached_sources]

    def _mru(self):
        value = self._get(self.SCHEMA, 'mru-sources')
        return [tuple(s) for s in value] if isinstance(value, list) else []

    def current_index(self):
        sources = getattr(self, '_cached_sources', None) or self._sources()
        mru = self._mru()
        if mru and mru[0] in sources:
            return sources.index(mru[0])
        return 0 if sources else None

    def _shortcut(self):
        value = self._get('org.gnome.desktop.wm.keybindings', 'switch-input-source')
        for accel in value or []:
            keys = parse_accelerator(accel)
            if keys:
                return keys
        return None

    def select(self, index, layouts):
        sources = getattr(self, '_cached_sources', None) or self._sources()
        if not 0 <= index < len(sources):
            return False
        target = sources[index]
        mru = self._mru()
        if target not in mru:
            return False
        steps = mru.index(target)
        if steps == 0:
            return True
        keys = self._shortcut()
        if not keys:
            applog.write('GNOME: no switch-input-source shortcut configured')
            return False
        modifiers, key = keys
        # نگه‌داشتن modifier و n بار زدن کلید → n-امین زبان در فهرست MRU
        self._press_shortcut(modifiers, key, steps)
        deadline = time.monotonic() + 0.5
        while time.monotonic() < deadline:
            time.sleep(0.03)
            if self._mru()[:1] == [target]:
                return True
        return False


# نام کلیدها در میان‌برهای GNOME → کد evdev
_ACCEL_KEYS = {
    'space': 57, 'tab': 15, 'shift_l': 42, 'shift_r': 54, 'alt_l': 56, 'alt_r': 100,
    'control_l': 29, 'control_r': 97, 'super_l': 125, 'super_r': 126, 'caps_lock': 58,
    'iso_next_group': None, 'grave': 41, 'menu': 127,
}
_ACCEL_MODS = {'super': 125, 'shift': 42, 'alt': 56, 'control': 29, 'primary': 29,
               'ctrl': 29, 'meta': 125, 'mod4': 125, 'mod1': 56}


def parse_accelerator(accel):
    """«<Super>space» → ([125], 57)"""
    mods = [m.lower() for m in re.findall(r'<([^>]+)>', accel)]
    key = re.sub(r'<[^>]+>', '', accel).strip().lower()
    codes = []
    for m in mods:
        if m not in _ACCEL_MODS:
            return None
        codes.append(_ACCEL_MODS[m])
    if len(key) == 1 and key.isalpha():
        code = next((c for c, p, _, _ in keymaps.US_KEYS if p == key), None)
    else:
        code = _ACCEL_KEYS.get(key)
    if code is None:
        return None
    return codes, code


# ---------------------------------------------------------------- KDE

class KdeBackend(Backend):
    """KDE Plasma: سرویس D-Bus  org.kde.keyboard /Layouts"""
    name = 'kde'
    DEST = 'org.kde.keyboard'
    IFACE = 'org.kde.KeyboardLayouts'

    def __init__(self):
        self._tool = 'gdbus' if shutil.which('gdbus') else ('dbus-send' if shutil.which('dbus-send') else None)

    def _call(self, method, *args):
        if self._tool == 'gdbus':
            cmd = ['gdbus', 'call', '--session', '--dest', self.DEST, '--object-path', '/Layouts',
                   '--method', '%s.%s' % (self.IFACE, method)] + [str(a) for a in args]
        elif self._tool == 'dbus-send':
            cmd = ['dbus-send', '--session', '--print-reply', '--dest=' + self.DEST, '/Layouts',
                   '%s.%s' % (self.IFACE, method)] + ['uint32:%d' % a for a in args]
        else:
            return None
        return _run(cmd)

    def available(self):
        return self._tool is not None and bool(self.layouts())

    def layouts(self):
        out = self._call('getLayoutsList')
        if not out:
            return []
        strings = re.findall(r'"((?:[^"\\]|\\.)*)"', out) if self._tool == 'dbus-send' \
            else re.findall(r"'((?:[^'\\]|\\.)*)'", out)
        result = []
        for i in range(0, len(strings) - 2, 3):
            short, variant, display = strings[i:i + 3]
            persian = short.lower() == 'ir' or _is_persian_name(display)
            result.append(Layout(persian, keymaps.variant_to_table(variant or display), display or short))
        return result

    def current_index(self):
        out = self._call('getLayout')
        m = re.search(r'uint32\s+(\d+)', out or '')
        return int(m.group(1)) if m else None

    def select(self, index, layouts):
        out = self._call('setLayout', index)
        return out is not None and 'false' not in out


# ---------------------------------------------------------------- Sway

class SwayBackend(Backend):
    name = 'sway'

    def available(self):
        return shutil.which('swaymsg') is not None and bool(self.layouts())

    def _keyboard(self):
        out = _run(['swaymsg', '-t', 'get_inputs', '-r'])
        try:
            inputs = json.loads(out or '[]')
        except ValueError:
            return None
        for dev in inputs:
            if dev.get('type') == 'keyboard' and dev.get('xkb_layout_names') \
                    and 'TanbalType' not in dev.get('name', ''):
                return dev
        return None

    def layouts(self):
        dev = self._keyboard()
        if not dev:
            return []
        return [Layout(_is_persian_name(n), keymaps.variant_to_table(n), n) for n in dev['xkb_layout_names']]

    def current_index(self):
        dev = self._keyboard()
        return dev.get('xkb_active_layout_index') if dev else None

    def select(self, index, layouts):
        return _run(['swaymsg', 'input', 'type:keyboard', 'xkb_switch_layout', str(index)]) is not None


# ---------------------------------------------------------------- Hyprland

class HyprlandBackend(Backend):
    name = 'hyprland'

    def available(self):
        return shutil.which('hyprctl') is not None and bool(self.layouts())

    def _keyboards(self):
        out = _run(['hyprctl', 'devices', '-j'])
        try:
            devices = json.loads(out or '{}')
        except ValueError:
            return []
        return [k for k in devices.get('keyboards', []) if 'tanbaltype' not in k.get('name', '').lower()]

    def _main(self):
        keyboards = self._keyboards()
        for k in keyboards:
            if k.get('main'):
                return k
        return keyboards[0] if keyboards else None

    def layouts(self):
        k = self._main()
        if not k:
            return []
        names = [s.strip() for s in k.get('layout', '').split(',')]
        variants = [s.strip() for s in k.get('variant', '').split(',')]
        variants += [''] * (len(names) - len(variants))
        return [Layout(_is_persian_xkb(n), keymaps.variant_to_table(v), n) for n, v in zip(names, variants)]

    def current_index(self):
        k = self._main()
        if not k:
            return None
        active = k.get('active_keymap', '')
        names = [s.strip() for s in k.get('layout', '').split(',')]
        # active_keymap نام نمایشی است (مثل «Persian»)
        persian = _is_persian_name(active)
        for i, n in enumerate(names):
            if _is_persian_xkb(n) == persian:
                return i
        return None

    def select(self, index, layouts):
        ok = False
        for k in self._keyboards():
            if _run(['hyprctl', 'switchxkblayout', k['name'], str(index)]) is not None:
                ok = True
        return ok


# ---------------------------------------------------------------- X11

class _XkbStateRec(ctypes.Structure):
    _fields_ = [('group', ctypes.c_ubyte), ('locked_group', ctypes.c_ubyte),
                ('base_group', ctypes.c_ushort), ('latched_group', ctypes.c_ushort),
                ('mods', ctypes.c_ubyte), ('base_mods', ctypes.c_ubyte),
                ('latched_mods', ctypes.c_ubyte), ('locked_mods', ctypes.c_ubyte),
                ('compat_state', ctypes.c_ubyte), ('grab_mods', ctypes.c_ubyte),
                ('compat_grab_mods', ctypes.c_ubyte), ('lookup_mods', ctypes.c_ubyte),
                ('compat_lookup_mods', ctypes.c_ubyte), ('ptr_buttons', ctypes.c_ushort)]


class X11Backend(Backend):
    """XKB: هر چیدمان یک «group» است (مثلاً setxkbmap us,ir)."""
    name = 'x11'
    XKB_USE_CORE_KBD = 0x0100

    def __init__(self):
        self._lock = threading.Lock()
        self._dpy = None
        self._x = None
        lib = ctypes.util.find_library('X11')
        if not lib or not os.environ.get('DISPLAY'):
            return
        try:
            x = ctypes.CDLL(lib)
        except OSError:
            return
        x.XOpenDisplay.restype = ctypes.c_void_p
        x.XOpenDisplay.argtypes = [ctypes.c_char_p]
        x.XkbGetState.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.POINTER(_XkbStateRec)]
        x.XkbLockGroup.argtypes = [ctypes.c_void_p, ctypes.c_uint, ctypes.c_uint]
        x.XFlush.argtypes = [ctypes.c_void_p]
        x.XkbQueryExtension.argtypes = [ctypes.c_void_p] + [ctypes.POINTER(ctypes.c_int)] * 5
        x.XInternAtom.restype = ctypes.c_ulong
        x.XInternAtom.argtypes = [ctypes.c_void_p, ctypes.c_char_p, ctypes.c_int]
        x.XDefaultRootWindow.restype = ctypes.c_ulong
        x.XDefaultRootWindow.argtypes = [ctypes.c_void_p]
        x.XGetWindowProperty.argtypes = [
            ctypes.c_void_p, ctypes.c_ulong, ctypes.c_ulong, ctypes.c_long, ctypes.c_long, ctypes.c_int,
            ctypes.c_ulong, ctypes.POINTER(ctypes.c_ulong), ctypes.POINTER(ctypes.c_int),
            ctypes.POINTER(ctypes.c_ulong), ctypes.POINTER(ctypes.c_ulong),
            ctypes.POINTER(ctypes.POINTER(ctypes.c_ubyte))]
        x.XFree.argtypes = [ctypes.c_void_p]
        dpy = x.XOpenDisplay(None)
        if not dpy:
            return
        ints = [ctypes.c_int() for _ in range(3)] + [ctypes.c_int(1), ctypes.c_int(0)]  # XKB 1.0
        if not x.XkbQueryExtension(dpy, *[ctypes.byref(i) for i in ints]):
            return
        self._x, self._dpy = x, dpy

    def available(self):
        return self._dpy is not None and bool(self.layouts())

    def _rules_names(self):
        """ویژگی _XKB_RULES_NAMES پنجرهٔ ریشه: rules, model, layout, variant, options"""
        x, dpy = self._x, self._dpy
        atom = x.XInternAtom(dpy, b'_XKB_RULES_NAMES', 1)
        if not atom:
            return None
        actual_type, fmt = ctypes.c_ulong(), ctypes.c_int()
        nitems, after = ctypes.c_ulong(), ctypes.c_ulong()
        data = ctypes.POINTER(ctypes.c_ubyte)()
        status = x.XGetWindowProperty(dpy, x.XDefaultRootWindow(dpy), atom, 0, 1024, 0, 0,
                                      ctypes.byref(actual_type), ctypes.byref(fmt),
                                      ctypes.byref(nitems), ctypes.byref(after), ctypes.byref(data))
        if status != 0 or not data:
            return None
        raw = ctypes.string_at(data, nitems.value)
        x.XFree(data)
        return raw.decode('utf-8', 'replace').split('\0')

    def layouts(self):
        with self._lock:
            names = self._rules_names() if self._dpy else None
        if not names or len(names) < 4:
            return []
        layouts = names[2].split(',')
        variants = names[3].split(',') + [''] * len(layouts)
        return [Layout(_is_persian_xkb(l), keymaps.variant_to_table(v), l) for l, v in zip(layouts, variants)]

    def current_index(self):
        with self._lock:
            if not self._dpy:
                return None
            state = _XkbStateRec()
            if self._x.XkbGetState(self._dpy, self.XKB_USE_CORE_KBD, ctypes.byref(state)) != 0:
                return None
            return state.group

    def select(self, index, layouts):
        with self._lock:
            if not self._dpy:
                return False
            ok = bool(self._x.XkbLockGroup(self._dpy, self.XKB_USE_CORE_KBD, index))
            self._x.XFlush(self._dpy)
        return ok


# ---------------------------------------------------------------- انتخاب backend

def detect_backend(preferred, press_shortcut):
    env = os.environ
    desktop = env.get('XDG_CURRENT_DESKTOP', '').lower()
    session = env.get('XDG_SESSION_TYPE', '').lower()

    factories = {
        'gnome': lambda: GnomeBackend(press_shortcut),
        'kde': KdeBackend,
        'sway': SwayBackend,
        'hyprland': HyprlandBackend,
        'x11': X11Backend,
    }
    if preferred in factories:
        order = [preferred]
    else:
        order = []
        if env.get('HYPRLAND_INSTANCE_SIGNATURE'):
            order.append('hyprland')
        if env.get('SWAYSOCK'):
            order.append('sway')
        if 'kde' in desktop:
            order.append('kde')
        if any(d in desktop for d in ('gnome', 'unity', 'budgie', 'pantheon')):
            order.append('gnome')
        if session != 'wayland' and env.get('DISPLAY'):
            order.append('x11')
        for fallback in ('gnome', 'kde'):
            if fallback not in order:
                order.append(fallback)

    for name in order:
        try:
            backend = factories[name]()
            if backend.available():
                return backend
        except Exception as exc:  # یک backend خراب نباید مانع بقیه شود
            applog.write('Backend %s failed: %r' % (name, exc))
    return Backend()


class LayoutTracker:
    """وضعیت زبان کیبورد را نگه می‌دارد. پرس‌وجو از بعضی backendها (مثل gsettings) چند ده میلی‌ثانیه
    طول می‌کشد، پس در شروع هر کلمه در پس‌زمینه انجام می‌شود تا هنگام زدن Space آماده باشد."""

    def __init__(self, backend):
        self.backend = backend
        self._layouts = []
        self._layouts_time = 0.0
        self._current = None
        self._event = threading.Event()
        self._event.set()
        self._lock = threading.Lock()
        self.last_persian = None
        self.last_english = None

    def layouts(self, max_age=30.0):
        if not self._layouts or time.monotonic() - self._layouts_time > max_age:
            try:
                self._layouts = self.backend.layouts()
            except Exception as exc:
                applog.write('layouts() failed: %r' % exc)
            self._layouts_time = time.monotonic()
        return self._layouts

    def _query(self):
        try:
            layouts = self.layouts()
            index = self.backend.current_index()
            current = layouts[index] if index is not None and 0 <= index < len(layouts) else None
            if current is not None:
                if current.persian:
                    self.last_persian = index
                else:
                    self.last_english = index
        except Exception as exc:
            applog.write('Layout query failed: %r' % exc)
            current = None
        with self._lock:
            self._current = current
        self._event.set()

    def refresh_async(self):
        if not self._event.is_set():
            return
        self._event.clear()
        threading.Thread(target=self._query, daemon=True).start()

    def current(self, timeout=0.4):
        """زبان فعلی (Layout) یا None اگر معلوم نباشد."""
        self._event.wait(timeout)
        with self._lock:
            return self._current

    def persian_table(self):
        for layout in self.layouts():
            if layout.persian:
                return layout.table
        return None

    def switch(self, to_persian):
        """تعویض زبان برای اصلاح. خروجی: زبان جدید (Layout) یا None."""
        self._event.wait(0.5)
        layouts = self.layouts()
        candidates = [i for i, l in enumerate(layouts) if l.persian == to_persian]
        if not candidates:
            applog.write('Layout switch FAILED: no %s layout enabled' % ('Persian' if to_persian else 'English'))
            return None
        preferred = self.last_persian if to_persian else self.last_english
        index = preferred if preferred in candidates else candidates[0]
        ok = False
        try:
            ok = self.backend.select(index, layouts)
        except Exception as exc:
            applog.write('Layout switch error: %r' % exc)
        applog.write('Layout switch %s -> %s (%s)' % ('OK' if ok else 'FAILED', layouts[index], self.backend.name))
        with self._lock:
            self._current = layouts[index] if ok else None
        return self._current
