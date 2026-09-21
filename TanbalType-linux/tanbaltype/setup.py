# -*- coding: utf-8 -*-
"""تنظیم دسترسی به کیبورد (با sudo اجرا می‌شود؛ نصب‌کننده‌ها همین را صدا می‌زنند).

* قانون udev: کاربری که پشت سیستم وارد شده (seat فعال) به کیبوردها و /dev/uinput دسترسی دارد.
  (برچسب uaccess از systemd-logind؛ نیازی به گروه input یا اجرای برنامه با root نیست)
* بارگذاری ماژول uinput هنگام بوت.
"""
import os
import pwd
import grp
import subprocess
import sys

UDEV_RULE_PATH = '/etc/udev/rules.d/70-tanbaltype.rules'
MODULES_PATH = '/etc/modules-load.d/tanbaltype.conf'

UDEV_RULE = '''# TanbalType: دسترسی کاربرِ واردشده (seat فعال) به کیبوردها، موس و /dev/uinput
# شمارهٔ فایل باید کمتر از 73 باشد تا uaccess اعمال شود.
KERNEL=="uinput", SUBSYSTEM=="misc", TAG+="uaccess", OPTIONS+="static_node=uinput"
SUBSYSTEM=="input", KERNEL=="event*", ENV{ID_INPUT_KEYBOARD}=="1", TAG+="uaccess"
SUBSYSTEM=="input", KERNEL=="event*", ENV{ID_INPUT_MOUSE}=="1", TAG+="uaccess"
SUBSYSTEM=="input", KERNEL=="event*", ENV{ID_INPUT_TOUCHPAD}=="1", TAG+="uaccess"
'''


def _run(*cmd):
    try:
        subprocess.run(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)
    except OSError:
        pass


def apply_rules():
    """بعد از نصب/به‌روزرسانی: ماژول و قانون udev را فوراً فعال می‌کند (بدون نیاز به ری‌استارت)."""
    _run('modprobe', 'uinput')
    _run('udevadm', 'control', '--reload-rules')
    _run('udevadm', 'trigger', '--subsystem-match=misc', '--sysname-match=uinput')
    _run('udevadm', 'trigger', '--subsystem-match=input', '--action=change')
    _run('udevadm', 'settle', '--timeout=5')


def main():
    if os.geteuid() != 0:
        print('این دستور باید با sudo اجرا شود:  sudo tanbaltype setup', file=sys.stderr)
        return 1

    installed = [os.path.join(d, '70-tanbaltype.rules')
                 for d in ('/etc/udev/rules.d', '/usr/lib/udev/rules.d', '/lib/udev/rules.d')]
    if not any(os.path.exists(p) for p in installed):
        os.makedirs(os.path.dirname(UDEV_RULE_PATH), exist_ok=True)
        with open(UDEV_RULE_PATH, 'w', encoding='utf-8') as f:
            f.write(UDEV_RULE)
        print('نوشته شد: ' + UDEV_RULE_PATH)
    if not any(os.path.exists(os.path.join(d, 'tanbaltype.conf'))
               for d in ('/etc/modules-load.d', '/usr/lib/modules-load.d', '/lib/modules-load.d')):
        os.makedirs(os.path.dirname(MODULES_PATH), exist_ok=True)
        with open(MODULES_PATH, 'w', encoding='utf-8') as f:
            f.write('uinput\n')
        print('نوشته شد: ' + MODULES_PATH)
    apply_rules()

    # راه جایگزین برای سیستم‌هایی که systemd-logind ندارند: عضویت در گروه input
    user = os.environ.get('SUDO_USER')
    if user and user != 'root':
        try:
            members = grp.getgrnam('input').gr_mem
            pwd.getpwnam(user)
        except KeyError:
            members = None
        if members is not None and user not in members:
            _run('usermod', '-aG', 'input', user)
            print('کاربر %s به گروه input اضافه شد.' % user)
        # گروه input به‌طور پیش‌فرض فقط اجازهٔ خواندن دارد؛ uinput هم لازم است
        _write_group_rule()
    print('انجام شد. یک بار از سیستم خارج و دوباره وارد شوید، سپس:  tanbaltype start')
    return 0


def _write_group_rule():
    path = '/etc/udev/rules.d/71-tanbaltype-group.rules'
    if os.path.exists(path):
        return
    with open(path, 'w', encoding='utf-8') as f:
        f.write('# TanbalType (راه جایگزین): اعضای گروه input به /dev/uinput دسترسی دارند\n'
                'KERNEL=="uinput", SUBSYSTEM=="misc", GROUP="input", MODE="0660"\n')
    apply_rules()


if __name__ == '__main__':
    sys.exit(main())
