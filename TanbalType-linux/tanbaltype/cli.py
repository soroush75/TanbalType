# -*- coding: utf-8 -*-
"""خط فرمان tanbaltype."""
import argparse
import os
import re
import subprocess
import sys
import time
import traceback

from . import applog, paths

HELP = '''TanbalType — اصلاح خودکار متنی که با زبان اشتباه کیبورد تایپ شده (فارسی ⇄ انگلیسی)

دستورها:
  start                 اجرای برنامه در پس‌زمینه (به‌همراه آیکون کنار ساعت)
  stop | restart        توقف / اجرای مجدد
  status                وضعیت برنامه
  enable | disable | toggle
                        فعال/غیرفعال کردن (معادل کلید F10)
  words [add|remove W]  لغات شخصی (بدون آرگومان: نمایش فهرست)
  exceptions [add|remove W]
                        لغات استثنا (بدون آرگومان: نمایش فهرست)
  convert [TEXT]        تبدیل متن (یا ورودی استاندارد): «sghl» → «سلام» — مناسب سرور و SSH
  autostart [on|off]    اجرای خودکار هنگام ورود به سیستم
  log [on|off|show]     ذخیرهٔ log برای عیب‌یابی (پیش‌فرض خاموش)
  selftest              آزمون الگوریتم تشخیص
  run [-v]              اجرای سرویس در همین ترمینال (برای عیب‌یابی)
  tray                  فقط آیکون کنار ساعت
  setup                 (با sudo) تنظیم دوبارهٔ دسترسی به کیبورد
  version
'''


def _spawn(*args):
    env = dict(os.environ)
    root = os.path.dirname(paths.PACKAGE_DIR)
    env['PYTHONPATH'] = root + (os.pathsep + env['PYTHONPATH'] if env.get('PYTHONPATH') else '')
    subprocess.Popen([sys.executable, '-m', 'tanbaltype'] + list(args), env=env,
                     stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                     start_new_session=True, close_fds=True, cwd='/')


def _daemon():
    from . import daemon
    return daemon


def cmd_run(args):
    config = paths.load_config()
    if args.verbose:
        applog.print_to_stdout = True
    daemon = _daemon()
    # اگر سرویس به‌خاطر خطایی غیرمنتظره بسته شد، چند بار دوباره اجرا می‌شود
    crashes = []
    while True:
        try:
            return daemon.run(config)
        except Exception:
            applog.error('crashed:\n' + traceback.format_exc())
            now = time.monotonic()
            crashes = [t for t in crashes if now - t < 60] + [now]
            if len(crashes) >= 5:
                return 1
            time.sleep(1)


def _tray_possible(config):
    if not config.getboolean('tray', True):
        return False
    if not (os.environ.get('DISPLAY') or os.environ.get('WAYLAND_DISPLAY')):
        return False
    from . import tray
    return tray.available()


def cmd_start(args):
    daemon = _daemon()
    config = paths.load_config()
    paths.write_default_config()
    if daemon.send_command('ping', 0.5) != 'pong':
        _spawn('run')
        # منتظر آماده شدن سرویس (برای گزارش خطای دسترسی در همین ترمینال)
        for _ in range(40):
            time.sleep(0.1)
            if daemon.send_command('ping', 0.3) == 'pong':
                break
        else:
            print('TanbalType اجرا نشد. برای دیدن علت:  tanbaltype run -v', file=sys.stderr)
            return 1
    if _tray_possible(config):
        _spawn('tray')
    if sys.stdout.isatty():
        print('TanbalType اجرا شد — F10 برای فعال/غیرفعال')
    return 0


def cmd_simple(request):
    reply = _daemon().send_command(request)
    if reply is None:
        print('TanbalType اجرا نیست (tanbaltype start)', file=sys.stderr)
        return 1
    print(reply)
    return 0


def cmd_stop(args):
    daemon = _daemon()
    if daemon.send_command('quit') is None:
        return 0
    for _ in range(30):
        time.sleep(0.1)
        if daemon.send_command('ping', 0.3) is None:
            return 0
    return 1


def cmd_restart(args):
    cmd_stop(args)
    return cmd_start(args)


def cmd_words(args, store):
    if args.action in (None, 'list'):
        for w in store.all():
            print(w)
        return 0
    if not args.word:
        print('کلمه را بنویسید', file=sys.stderr)
        return 1
    for word in args.word:
        ok = store.add(word) if args.action == 'add' else store.remove(word)
        if not ok:
            print('%s: %s' % (word, 'قبلاً ثبت شده' if args.action == 'add' else 'در فهرست نیست'), file=sys.stderr)
    return 0


def cmd_convert(args):
    from . import detector, mapper
    mapper.configure(args.layout)
    text = ' '.join(args.text) if args.text else sys.stdin.read()

    def convert(match):
        token = match.group(0)
        if args.swap:
            return mapper.swap_layout(token)
        persian = mapper.count_persian(token) > 0
        return detector.detect_wrong_layout(token, persian) or token

    sys.stdout.write(re.sub(r'\S+', convert, text))
    if args.text:
        sys.stdout.write('\n')
    return 0


def cmd_log(args):
    if args.action == 'show':
        if not os.path.exists(paths.LOG_PATH):
            print('فایل log پیدا نشد: %s\nابتدا log را فعال کنید: tanbaltype log on' % paths.LOG_PATH)
            return 1
        with open(paths.LOG_PATH, encoding='utf-8', errors='replace') as f:
            sys.stdout.write(f.read())
        return 0
    if args.action in ('on', 'off'):
        code = cmd_simple('log-' + args.action)
        if code == 0:
            print(paths.LOG_PATH)
        return code
    print(paths.LOG_PATH)
    return 0


def cmd_selftest(args):
    from . import selftest
    applog.print_to_stdout = True
    return 0 if selftest.run() == 0 else 1


AUTOSTART_SYSTEM = '/etc/xdg/autostart/tanbaltype.desktop'
AUTOSTART_USER = os.path.join(paths.AUTOSTART_DIR, 'tanbaltype.desktop')


def autostart_enabled():
    if os.path.exists(AUTOSTART_USER):
        with open(AUTOSTART_USER, encoding='utf-8') as f:
            return not re.search(r'^\s*(Hidden\s*=\s*true|X-GNOME-Autostart-enabled\s*=\s*false)',
                                 f.read(), re.M | re.I)
    return os.path.exists(AUTOSTART_SYSTEM)


def set_autostart(enable):
    os.makedirs(paths.AUTOSTART_DIR, exist_ok=True)
    if enable and os.path.exists(AUTOSTART_SYSTEM):
        if os.path.exists(AUTOSTART_USER):
            os.remove(AUTOSTART_USER)
        return
    with open(AUTOSTART_USER, 'w', encoding='utf-8') as f:
        f.write('[Desktop Entry]\nType=Application\nName=TanbalType\nExec=tanbaltype start\n'
                'Icon=tanbaltype\nX-GNOME-Autostart-enabled=%s\nHidden=%s\n'
                % (str(enable).lower(), str(not enable).lower()))


def cmd_autostart(args):
    if args.action in ('on', 'off'):
        set_autostart(args.action == 'on')
    print('autostart=%s' % ('on' if autostart_enabled() else 'off'))
    return 0


def cmd_setup(args):
    from . import setup
    return setup.main()


def cmd_tray(args):
    from . import tray
    if not tray.available():
        print('برای آیکون کنار ساعت بسته‌های python3-gi و gir1.2-ayatanaappindicator3-0.1 لازم است.',
              file=sys.stderr)
        return 1
    return tray.main()


def main(argv=None):
    parser = argparse.ArgumentParser(prog='tanbaltype', usage=HELP, add_help=False)
    parser.add_argument('-h', '--help', action='store_true')
    sub = parser.add_subparsers(dest='command')

    p = sub.add_parser('run')
    p.add_argument('-v', '--verbose', action='store_true')
    for name in ('start', 'stop', 'restart', 'status', 'enable', 'disable', 'toggle',
                 'selftest', 'tray', 'setup', 'version'):
        sub.add_parser(name)
    for name in ('words', 'exceptions'):
        p = sub.add_parser(name)
        p.add_argument('action', nargs='?', choices=['list', 'add', 'remove'])
        p.add_argument('word', nargs='*')
    p = sub.add_parser('convert')
    p.add_argument('-s', '--swap', action='store_true', help='بدون تشخیص، همهٔ کلمات را برعکس کن')
    p.add_argument('-l', '--layout', default='pes', choices=['pes', 'winkeys'])
    p.add_argument('text', nargs='*')
    p = sub.add_parser('autostart')
    p.add_argument('action', nargs='?', choices=['on', 'off'])
    p = sub.add_parser('log')
    p.add_argument('action', nargs='?', choices=['on', 'off', 'show'])

    args = parser.parse_args(argv)
    if args.help:
        print(HELP)
        return 0
    command = args.command or 'start'

    from .words import exceptions, user_words
    handlers = {
        'run': cmd_run,
        'start': cmd_start,
        'stop': cmd_stop,
        'restart': cmd_restart,
        'status': lambda a: cmd_simple('status'),
        'enable': lambda a: cmd_simple('enable'),
        'disable': lambda a: cmd_simple('disable'),
        'toggle': lambda a: cmd_simple('toggle'),
        'words': lambda a: cmd_words(a, user_words),
        'exceptions': lambda a: cmd_words(a, exceptions),
        'convert': cmd_convert,
        'autostart': cmd_autostart,
        'log': cmd_log,
        'selftest': cmd_selftest,
        'setup': cmd_setup,
        'tray': cmd_tray,
        'version': lambda a: print('TanbalType %s' % paths.version()) or 0,
    }
    return handlers[command](args)
