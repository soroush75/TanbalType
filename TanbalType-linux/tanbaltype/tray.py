# -*- coding: utf-8 -*-
"""آیکون کنار ساعت (AppIndicator) و پنجره‌های لغات شخصی/استثنا.

یک پروسهٔ جدا از سرویس کیبورد است و فقط از طریق socket با آن حرف می‌زند؛ پس اگر محیط دسکتاپ
آیکون tray نداشته باشد یا GTK نصب نباشد، اصلاح خودکار بدون مشکل کار می‌کند.
"""
import os
import subprocess

from . import cli, paths
from .daemon import send_command

_modules = None


def _load():
    global _modules
    if _modules is not None:
        return _modules
    _modules = False
    try:
        import gi
        gi.require_version('Gtk', '3.0')
        from gi.repository import GLib, Gtk
        indicator = None
        for name in ('AyatanaAppIndicator3', 'AppIndicator3'):
            try:
                gi.require_version(name, '0.1')
                indicator = __import__('gi.repository.' + name, fromlist=[name])
                break
            except (ValueError, ImportError):
                continue
        if indicator is not None:
            _modules = (GLib, Gtk, indicator)
    except (ImportError, ValueError):
        pass
    return _modules


def available():
    return bool(_load())


ICON_DIRS = [
    os.path.join(os.path.dirname(paths.PACKAGE_DIR), 'packaging', 'icons'),  # اجرا از سورس
]


class WordListWindow:
    def __init__(self, Gtk, title, hint, store):
        self.Gtk = Gtk
        self.store = store
        win = Gtk.Window(title=title)
        win.set_default_size(380, 460)
        win.set_icon_name('tanbaltype')
        win.connect('delete-event', lambda w, e: w.hide() or True)
        # تا وقتی این پنجره فعال است، کلماتِ تایپ‌شده در آن اصلاح نمی‌شوند
        win.connect('focus-in-event', lambda *_: send_command('suspend') and False)
        win.connect('focus-out-event', lambda *_: send_command('resume') and False)
        win.connect('hide', lambda *_: send_command('resume'))

        box = Gtk.Box(orientation=Gtk.Orientation.VERTICAL, spacing=8)
        box.set_border_width(12)
        label = Gtk.Label(label=hint)
        label.set_line_wrap(True)
        label.set_xalign(1.0)
        box.pack_start(label, False, False, 0)

        row = Gtk.Box(spacing=6)
        self.entry = Gtk.Entry()
        self.entry.connect('activate', self._add)
        add = Gtk.Button(label='افزودن')
        add.connect('clicked', self._add)
        row.pack_start(self.entry, True, True, 0)
        row.pack_start(add, False, False, 0)
        box.pack_start(row, False, False, 0)

        self.model = Gtk.ListStore(str)
        self.view = Gtk.TreeView(model=self.model)
        self.view.set_headers_visible(False)
        self.view.append_column(Gtk.TreeViewColumn('', Gtk.CellRendererText(), text=0))
        scroll = Gtk.ScrolledWindow()
        scroll.add(self.view)
        box.pack_start(scroll, True, True, 0)

        remove = Gtk.Button(label='حذف کلمهٔ انتخاب‌شده')
        remove.connect('clicked', self._remove)
        box.pack_start(remove, False, False, 0)

        win.add(box)
        self.window = win

    def _refresh(self):
        self.model.clear()
        for word in self.store.all():
            self.model.append([word])

    def _add(self, *_):
        word = self.entry.get_text().strip()
        if word and self.store.add(word):
            self.entry.set_text('')
            self._refresh()

    def _remove(self, *_):
        model, it = self.view.get_selection().get_selected()
        if it is not None:
            self.store.remove(model[it][0])
            self._refresh()

    def present(self):
        self._refresh()
        self.window.show_all()
        self.window.present()


class Tray:
    def __init__(self):
        GLib, Gtk, AppIndicator = _load()
        self.GLib, self.Gtk = GLib, Gtk
        Gtk.Widget.set_default_direction(Gtk.TextDirection.RTL)

        self.indicator = AppIndicator.Indicator.new(
            'tanbaltype', 'tanbaltype-symbolic', AppIndicator.IndicatorCategory.APPLICATION_STATUS)
        for d in ICON_DIRS:
            if os.path.isdir(d):
                self.indicator.set_icon_theme_path(d)
        self.indicator.set_status(AppIndicator.IndicatorStatus.ACTIVE)
        self.indicator.set_title(paths.APP_NAME)

        from .words import exceptions, user_words
        self.windows = {}
        self.stores = {'exceptions': exceptions, 'words': user_words}

        menu = Gtk.Menu()
        self.enabled_item = self._check(menu, 'فعال (F10)', self._toggle_enabled)
        self.log_item = self._check(menu, 'ذخیره log', self._toggle_log)
        self._item(menu, 'لغات استثنا…', lambda *_: self._words('exceptions'))
        self._item(menu, 'لغات شخصی…', lambda *_: self._words('words'))
        self.autostart_item = self._check(menu, 'اجرا هنگام ورود به سیستم', self._toggle_autostart)
        self._item(menu, 'نمایش log', self._show_log)
        self._item(menu, 'درباره برنامه', self._about)
        menu.append(Gtk.SeparatorMenuItem())
        self._item(menu, 'خروج', self._quit)
        menu.show_all()
        self.indicator.set_menu(menu)

        self._updating = False
        self._misses = 0
        self._refresh()
        GLib.timeout_add_seconds(2, self._refresh)

    def _item(self, menu, label, handler):
        item = self.Gtk.MenuItem(label=label)
        item.connect('activate', handler)
        menu.append(item)
        return item

    def _check(self, menu, label, handler):
        item = self.Gtk.CheckMenuItem(label=label)
        item.connect('toggled', handler)
        menu.append(item)
        return item

    def _status(self):
        reply = send_command('status', 1.0)
        if reply is None:
            return None
        return dict(part.split('=', 1) for part in reply.split() if '=' in part)

    def _refresh(self):
        status = self._status()
        if status is None:
            # سرویس بسته شده (مثلاً tanbaltype stop)؛ آیکون هم بسته می‌شود
            self._misses += 1
            if self._misses >= 3:
                self.Gtk.main_quit()
                return False
            return True
        self._misses = 0
        enabled = status.get('enabled') == '1'
        self._updating = True
        self.enabled_item.set_active(enabled)
        self.enabled_item.set_label('فعال (F10)' if enabled else 'غیرفعال (F10)')
        self.log_item.set_active(status.get('log') == '1')
        self.autostart_item.set_active(cli.autostart_enabled())
        self._updating = False
        self.indicator.set_icon_full('tanbaltype-symbolic' if enabled else 'tanbaltype-disabled-symbolic',
                                     'TanbalType — ' + ('فعال' if enabled else 'غیرفعال'))
        return True

    def _toggle_enabled(self, item):
        if not self._updating:
            send_command('enable' if item.get_active() else 'disable')
            self._refresh()

    def _toggle_log(self, item):
        if not self._updating:
            send_command('log-on' if item.get_active() else 'log-off', 10.0)

    def _toggle_autostart(self, item):
        if not self._updating:
            cli.set_autostart(item.get_active())

    def _words(self, kind):
        if kind not in self.windows:
            if kind == 'words':
                title = 'لغات شخصی'
                hint = ('هر لغتی که اینجا ثبت کنید، اگر با زبان اشتباه تایپ شود دقیقاً به همین شکل اصلاح '
                        'می‌شود (حتی اگر در لغت‌نامه نباشد).')
            else:
                title = 'لغات استثنا'
                hint = 'برنامه هرگز این کلمات را اصلاح نمی‌کند.'
            self.windows[kind] = WordListWindow(self.Gtk, title, hint, self.stores[kind])
        self.windows[kind].present()

    def _show_log(self, *_):
        if os.path.exists(paths.LOG_PATH):
            subprocess.Popen(['xdg-open', paths.LOG_PATH], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        else:
            self._message('فایل log پیدا نشد.\n\nمسیر:\n%s\n\nابتدا گزینهٔ «ذخیره log» را فعال کنید.'
                          % paths.LOG_PATH)

    def _about(self, *_):
        self._message('%s\nنسخه: %s\nسازنده: %s' % (paths.APP_NAME, paths.version(), paths.DEVELOPER))

    def _message(self, text):
        dialog = self.Gtk.MessageDialog(message_type=self.Gtk.MessageType.INFO,
                                        buttons=self.Gtk.ButtonsType.OK, text=text)
        dialog.set_title(paths.APP_NAME)
        dialog.run()
        dialog.destroy()

    def _quit(self, *_):
        send_command('quit')
        self.Gtk.main_quit()


def main():
    # فقط یک آیکون (tanbaltype start ممکن است چند بار اجرا شود)
    import fcntl
    lock = open(paths.runtime_path('tanbaltype-tray.lock'), 'w')
    try:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError:
        return 0
    tray = Tray()
    import signal
    signal.signal(signal.SIGINT, signal.SIG_DFL)
    tray.Gtk.main()
    return 0
