# -*- coding: utf-8 -*-
"""اعلان دسکتاپ (به‌جای balloon tip ویندوز). اگر محیط گرافیکی نباشد، بی‌صدا نادیده گرفته می‌شود."""
import shutil
import subprocess


def send(title, body, icon='tanbaltype'):
    try:
        if shutil.which('notify-send'):
            cmd = ['notify-send', '-a', 'TanbalType', '-i', icon, '-t', '2000', title, body]
        elif shutil.which('gdbus'):
            cmd = ['gdbus', 'call', '--session', '--dest', 'org.freedesktop.Notifications',
                   '--object-path', '/org/freedesktop/Notifications',
                   '--method', 'org.freedesktop.Notifications.Notify',
                   'TanbalType', '0', icon, title, body, '[]', '{}', '2000']
        else:
            return
        # بدون انتظار: اعلان نباید حلقهٔ کیبورد را معطل کند
        subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                         stdin=subprocess.DEVNULL, start_new_session=True)
    except OSError:
        pass
