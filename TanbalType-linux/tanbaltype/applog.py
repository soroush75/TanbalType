# -*- coding: utf-8 -*-
"""log برای عیب‌یابی. پیش‌فرض خاموش است و فقط با انتخاب کاربر فعال می‌شود
(چون با فعال بودنش متن تایپ‌شده هم ذخیره می‌شود)."""
import datetime
import os
import sys
import threading

from . import paths

enabled = False
# در self-test و حالت --verbose خروجی روی ترمینال چاپ می‌شود
print_to_stdout = False

_lock = threading.Lock()


def write(message):
    if print_to_stdout:
        print(message, flush=True)
        return
    if not enabled:
        return
    now = datetime.datetime.now().strftime('%Y-%m-%d %H:%M:%S.%f')[:-3]
    with _lock:
        try:
            os.makedirs(os.path.dirname(paths.LOG_PATH), exist_ok=True)
            with open(paths.LOG_PATH, 'a', encoding='utf-8') as f:
                f.write('%s %s\n' % (now, message))
        except OSError:
            pass  # نوشتن log نباید برنامه را مختل کند


def error(message):
    """خطاهای مهم همیشه روی stderr هم چاپ می‌شوند (در journal دیده می‌شوند)."""
    if not print_to_stdout:
        print('tanbaltype: ' + message, file=sys.stderr, flush=True)
    write(message)


def write_header():
    write('=== TanbalType log started ===')
    write('Version: %s, Python %s' % (paths.version(), sys.version.split()[0]))
    session = ', '.join('%s=%s' % (k, os.environ.get(k, '')) for k in
                        ('XDG_SESSION_TYPE', 'XDG_CURRENT_DESKTOP', 'DESKTOP_SESSION'))
    write('Session: ' + session)
