# -*- coding: utf-8 -*-
"""مسیر فایل‌ها طبق استاندارد XDG."""
import configparser
import os

APP_NAME = 'TanbalType'
DEVELOPER = 'سروش سرمست'

PACKAGE_DIR = os.path.dirname(os.path.abspath(__file__))


def _xdg(var, fallback):
    value = os.environ.get(var, '')
    return value if os.path.isabs(value) else os.path.expanduser(fallback)


CONFIG_DIR = os.path.join(_xdg('XDG_CONFIG_HOME', '~/.config'), 'tanbaltype')
STATE_DIR = os.path.join(_xdg('XDG_STATE_HOME', '~/.local/state'), 'tanbaltype')
LOG_PATH = os.path.join(STATE_DIR, 'tanbaltype.log')
CONFIG_PATH = os.path.join(CONFIG_DIR, 'config.ini')
AUTOSTART_DIR = os.path.join(_xdg('XDG_CONFIG_HOME', '~/.config'), 'autostart')


def runtime_path(name):
    runtime = os.environ.get('XDG_RUNTIME_DIR', '')
    if os.path.isdir(runtime):
        return os.path.join(runtime, name)
    return os.path.join('/tmp', '%s-%d' % (name, os.getuid()))


SOCKET_PATH = runtime_path('tanbaltype.sock')


def data_file(name):
    """فایل داده (مثل PersianWords.txt): کنار بسته، در سورس مخزن، یا در /usr/share."""
    candidates = [
        os.environ.get('TANBALTYPE_DATA', ''),
        PACKAGE_DIR,
        os.path.join(os.path.dirname(os.path.dirname(PACKAGE_DIR)), 'TanbalType'),  # سورس مخزن
        '/usr/share/tanbaltype',
        '/usr/local/share/tanbaltype',
    ]
    for directory in candidates:
        if directory:
            path = os.path.join(directory, name)
            if os.path.isfile(path):
                return path
    return None


def version():
    try:
        from . import _version
        return _version.VERSION
    except ImportError:
        return 'dev'


DEFAULT_CONFIG = {
    # auto | gnome | kde | x11 | sway | hyprland | none
    'backend': 'auto',
    # auto | pes | winkeys
    'persian_layout': 'auto',
    'toggle_key': 'F10',
    'tray': 'true',
    'notifications': 'true',
}


def load_config():
    parser = configparser.ConfigParser()
    parser['tanbaltype'] = dict(DEFAULT_CONFIG)
    try:
        parser.read(CONFIG_PATH, encoding='utf-8')
    except (configparser.Error, OSError):
        pass
    return parser['tanbaltype']


def write_default_config():
    if os.path.exists(CONFIG_PATH):
        return
    os.makedirs(CONFIG_DIR, exist_ok=True)
    with open(CONFIG_PATH, 'w', encoding='utf-8') as f:
        f.write('''# تنظیمات TanbalType — بعد از تغییر، برنامه را دوباره اجرا کنید (tanbaltype restart)
[tanbaltype]
# روش تشخیص و تعویض زبان کیبورد: auto | gnome | kde | x11 | sway | hyprland
backend = auto
# چیدمان فارسی: auto | pes (استاندارد لینوکس، «پ» روی m) | winkeys (مثل ویندوز، «پ» روی \\)
persian_layout = auto
# کلید فعال/غیرفعال کردن: F1 تا F12 یا none
toggle_key = F10
# آیکون کنار ساعت (در صورت نصب بودن python3-gi و AppIndicator)
tray = true
# اعلان هنگام فعال/غیرفعال شدن
notifications = true
''')
