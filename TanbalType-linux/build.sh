#!/bin/sh
# ساخت بسته‌های نصبی TanbalType برای لینوکس. روی هر سیستمی با Python 3 اجرا می‌شود (حتی macOS).
#
#   ./build.sh   → در پوشهٔ build/:
#     tanbaltype_VERSION_all.deb        Debian، Ubuntu، Mint، Pop!_OS، ...
#     TanbalType-linux-VERSION.tar.gz   همهٔ توزیع‌ها (install.sh داخلش) + منبع ساخت RPM و Arch
#     tanbaltype-VERSION-1.noarch.rpm   Fedora، openSUSE، ... (فقط اگر rpmbuild نصب باشد)
set -eu
cd "$(dirname "$0")"

VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' ../TanbalType/TanbalType.csproj | head -1)
BUILD=build
NAME=TanbalType-linux-$VERSION

echo "==> TanbalType $VERSION"

# فهرست‌های لغت از سورس C# (منبع واحد) ساخته می‌شوند
python3 tools/gen_detector_data.py

echo "==> tests"
python3 -m tanbaltype selftest | tail -1
python3 -m unittest discover -s tests 2>&1 | tail -1

rm -rf "$BUILD"
mkdir -p "$BUILD"

# ---------------------------------------------------------------- tar.gz (نصب‌کنندهٔ عمومی)
SRC="$BUILD/$NAME"
mkdir -p "$SRC/tanbaltype" "$SRC/tools" "$SRC/packaging"
cp tanbaltype/*.py tanbaltype/detector_data.json "$SRC/tanbaltype/"
echo "VERSION = '$VERSION'" > "$SRC/tanbaltype/_version.py"
cp tools/stage.py "$SRC/tools/"
cp -R packaging/icons packaging/*.desktop packaging/tanbaltype.sh packaging/modules-load.conf "$SRC/packaging/"
cp ../TanbalType/PersianWords.txt README.md install.sh "$SRC/"
cp ../LICENSE "$SRC/"
sed "s/@VERSION@/$VERSION/" packaging/rpm/tanbaltype.spec > "$SRC/tanbaltype.spec"
chmod +x "$SRC/install.sh"
# Arch: PKGBUILD کنار tar.gz
sed "s/@VERSION@/$VERSION/" packaging/arch/PKGBUILD > "$BUILD/PKGBUILD"
cp packaging/arch/tanbaltype.install "$BUILD/"
python3 - "$BUILD" "$NAME" <<'EOF'
import os, sys, tarfile
build, name = sys.argv[1:3]
def clean(info):
    info.uid = info.gid = 0
    info.uname = info.gname = 'root'
    if '__pycache__' in info.name or info.name.endswith('.DS_Store'):
        return None
    if info.isfile():
        info.mode = 0o755 if info.name.endswith(('.sh', '/tanbaltype.sh')) else 0o644
    return info
with tarfile.open(os.path.join(build, name + '.tar.gz'), 'w:gz', format=tarfile.GNU_FORMAT) as tar:
    tar.add(os.path.join(build, name), arcname=name, filter=clean)
EOF
echo "==> $BUILD/$NAME.tar.gz"

# ---------------------------------------------------------------- deb
STAGE="$BUILD/stage-deb"
python3 tools/stage.py --root "$STAGE" --prefix /usr >/dev/null
# نسخهٔ واقعی در بسته (نه dev)
python3 tools/mkdeb.py "$STAGE" "$VERSION" "$BUILD/tanbaltype_${VERSION}_all.deb"
rm -rf "$STAGE"

# ---------------------------------------------------------------- rpm (اختیاری)
if command -v rpmbuild >/dev/null 2>&1; then
    TOP="$(pwd)/$BUILD/rpmbuild"
    mkdir -p "$TOP/SOURCES"
    cp "$BUILD/$NAME.tar.gz" "$TOP/SOURCES/"
    rpmbuild --define "_topdir $TOP" -bb "$SRC/tanbaltype.spec" >/dev/null
    find "$TOP/RPMS" -name '*.rpm' -exec cp {} "$BUILD/" \;
    rm -rf "$TOP"
    echo "==> $(ls "$BUILD"/*.rpm)"
else
    echo "==> rpm: skipped (rpmbuild not installed; on Fedora: sudo dnf install rpm-build)"
fi

rm -rf "$SRC"
ls -la "$BUILD"
