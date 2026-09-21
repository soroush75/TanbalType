# -*- coding: utf-8 -*-
"""ساخت بستهٔ .deb بدون نیاز به dpkg-deb (قابل اجرا روی هر سیستمی که Python دارد).

فایل deb یک آرشیو ar است با سه عضو: debian-binary، control.tar.gz و data.tar.xz.
    python3 tools/mkdeb.py STAGE_DIR VERSION OUTPUT.deb
"""
import hashlib
import io
import os
import sys
import tarfile
import time

HERE = os.path.dirname(os.path.abspath(__file__))
DEBIAN = os.path.join(os.path.dirname(HERE), 'packaging', 'debian')
MTIME = int(os.environ.get('SOURCE_DATE_EPOCH', time.time()))


def _tarinfo(name, size=0, mode=0o644, kind=tarfile.REGTYPE):
    info = tarfile.TarInfo(name)
    info.size = size
    info.mode = mode
    info.type = kind
    info.uid = info.gid = 0
    info.uname = info.gname = 'root'
    info.mtime = MTIME
    return info


def build_data(stage):
    """data.tar.xz با مالکیت root و ترتیب ثابت؛ خروجی: (bytes, md5sums, اندازه به KB, conffiles)"""
    buf = io.BytesIO()
    md5 = []
    conffiles = []
    total = 0
    with tarfile.open(fileobj=buf, mode='w:xz', format=tarfile.GNU_FORMAT) as tar:
        tar.addfile(_tarinfo('./', mode=0o755, kind=tarfile.DIRTYPE))
        for dirpath, dirnames, filenames in os.walk(stage):
            dirnames.sort()
            rel_dir = os.path.relpath(dirpath, stage)
            if rel_dir != '.':
                tar.addfile(_tarinfo('./%s/' % rel_dir, mode=0o755, kind=tarfile.DIRTYPE))
            for name in sorted(filenames):
                path = os.path.join(dirpath, name)
                rel = os.path.relpath(path, stage)
                with open(path, 'rb') as f:
                    data = f.read()
                mode = 0o755 if os.stat(path).st_mode & 0o111 else 0o644
                tar.addfile(_tarinfo('./' + rel, len(data), mode), io.BytesIO(data))
                md5.append('%s  %s\n' % (hashlib.md5(data).hexdigest(), rel))
                total += len(data)
                if rel.startswith('etc/'):
                    conffiles.append('/' + rel + '\n')
    return buf.getvalue(), ''.join(md5), (total + 1023) // 1024, ''.join(conffiles)


def build_control(version, size, md5sums, conffiles):
    buf = io.BytesIO()
    with tarfile.open(fileobj=buf, mode='w:gz', format=tarfile.GNU_FORMAT) as tar:
        tar.addfile(_tarinfo('./', mode=0o755, kind=tarfile.DIRTYPE))

        def add(name, text, mode=0o644):
            data = text.encode('utf-8')
            tar.addfile(_tarinfo('./' + name, len(data), mode), io.BytesIO(data))

        control = open(os.path.join(DEBIAN, 'control'), encoding='utf-8').read()
        add('control', control.replace('@VERSION@', version).replace('@SIZE@', str(size)))
        add('md5sums', md5sums)
        if conffiles:
            add('conffiles', conffiles)
        for script in ('postinst', 'prerm', 'postrm'):
            add(script, open(os.path.join(DEBIAN, script), encoding='utf-8').read(), 0o755)
    return buf.getvalue()


def ar_member(name, data):
    header = '%-16s%-12d%-6d%-6d%-8s%-10d`\n' % (name, MTIME, 0, 0, '100644', len(data))
    assert len(header) == 60
    return header.encode('ascii') + data + (b'\n' if len(data) % 2 else b'')


def main():
    stage, version, output = sys.argv[1:4]
    data, md5sums, size, conffiles = build_data(stage)
    control = build_control(version, size, md5sums, conffiles)
    with open(output, 'wb') as f:
        f.write(b'!<arch>\n')
        f.write(ar_member('debian-binary', b'2.0\n'))
        f.write(ar_member('control.tar.gz', control))
        f.write(ar_member('data.tar.xz', data))
    print('==> %s (%d KB)' % (output, os.path.getsize(output) // 1024))


if __name__ == '__main__':
    main()
