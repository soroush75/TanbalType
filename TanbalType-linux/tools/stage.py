# -*- coding: utf-8 -*-
"""کپی فایل‌های برنامه در ساختار استاندارد لینوکس (مشترک بین بستهٔ deb/rpm و نصب‌کنندهٔ عمومی).

    python3 tools/stage.py --root DIR --prefix /usr        ← برای ساخت بسته
    sudo python3 tools/stage.py --root / --prefix /usr/local --manifest FILE   ← نصب مستقیم
"""
import argparse
import os
import shutil
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.dirname(HERE)  # پوشهٔ TanbalType-linux (یا ریشهٔ فایل tar.gz)
sys.path.insert(0, SRC)


def find_words():
    for path in (os.path.join(SRC, 'PersianWords.txt'),
                 os.path.join(os.path.dirname(SRC), 'TanbalType', 'PersianWords.txt')):
        if os.path.isfile(path):
            return path
    sys.exit('PersianWords.txt not found')


def read_version():
    import re
    csproj = os.path.join(os.path.dirname(SRC), 'TanbalType', 'TanbalType.csproj')
    if os.path.isfile(csproj):
        m = re.search(r'<Version>(.*?)</Version>', open(csproj, encoding='utf-8-sig').read())
        if m:
            return m.group(1)
    version_file = os.path.join(SRC, 'tanbaltype', '_version.py')
    if os.path.isfile(version_file):
        m = re.search(r"VERSION = '(.*?)'", open(version_file, encoding='utf-8').read())
        if m:
            return m.group(1)
    return '0.0.0'


class Stager:
    def __init__(self, root, prefix):
        self.root = root
        self.prefix = prefix
        self.files = []

    def dest(self, path):
        return os.path.join(self.root, path.lstrip('/'))

    def _record(self, path):
        self.files.append(path)

    def copy(self, src, path, mode=0o644):
        dst = self.dest(path)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(src, dst)
        os.chmod(dst, mode)
        self._record(path)

    def write(self, path, content, mode=0o644):
        dst = self.dest(path)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        with open(dst, 'w', encoding='utf-8') as f:
            f.write(content)
        os.chmod(dst, mode)
        self._record(path)


def stage(root, prefix):
    from tanbaltype import setup as tt_setup

    s = Stager(root, prefix)
    system = prefix == '/usr'
    libdir = prefix + '/lib/tanbaltype'
    version = read_version()

    pkg = os.path.join(SRC, 'tanbaltype')
    for name in sorted(os.listdir(pkg)):
        if name.endswith('.py') and name != '_version.py' or name == 'detector_data.json':
            s.copy(os.path.join(pkg, name), '%s/tanbaltype/%s' % (libdir, name))
    s.write('%s/tanbaltype/_version.py' % libdir, "VERSION = '%s'\n" % version)

    s.copy(find_words(), prefix + '/share/tanbaltype/PersianWords.txt')

    launcher = open(os.path.join(SRC, 'packaging', 'tanbaltype.sh'), encoding='utf-8').read()
    s.write(prefix + '/bin/tanbaltype', launcher.replace('@LIBDIR@', libdir), 0o755)

    s.copy(os.path.join(SRC, 'packaging', 'tanbaltype.desktop'), prefix + '/share/applications/tanbaltype.desktop')
    s.copy(os.path.join(SRC, 'packaging', 'tanbaltype-autostart.desktop'), '/etc/xdg/autostart/tanbaltype.desktop')
    icons = os.path.join(SRC, 'packaging', 'icons')
    for name in sorted(os.listdir(icons)):
        s.copy(os.path.join(icons, name), prefix + '/share/icons/hicolor/scalable/apps/' + name)

    # udev روی /usr/local/lib/udev نگاه نمی‌کند؛ نصب دستی در /etc انجام می‌شود
    rules_dir = '/usr/lib/udev/rules.d' if system else '/etc/udev/rules.d'
    modules_dir = '/usr/lib/modules-load.d' if system else '/etc/modules-load.d'
    s.write(rules_dir + '/70-tanbaltype.rules', tt_setup.UDEV_RULE)
    s.copy(os.path.join(SRC, 'packaging', 'modules-load.conf'), modules_dir + '/tanbaltype.conf')

    for doc in ('README.md', 'LICENSE'):
        for base in (SRC, os.path.dirname(SRC)):
            path = os.path.join(base, doc)
            if os.path.isfile(path):
                s.copy(path, prefix + '/share/doc/tanbaltype/' + doc)
                break
    return s.files, version


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True)
    parser.add_argument('--prefix', default='/usr')
    parser.add_argument('--manifest')
    args = parser.parse_args()
    files, version = stage(args.root, args.prefix)
    if args.manifest:
        with open(args.manifest, 'w', encoding='utf-8') as f:
            f.write(''.join(p + '\n' for p in files))
    print('staged %d files (version %s)' % (len(files), version))


if __name__ == '__main__':
    main()
