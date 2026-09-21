#!/bin/sh
# نصب‌کنندهٔ عمومی TanbalType برای همهٔ توزیع‌های لینوکس
# (Debian، Ubuntu، Mint، Fedora، openSUSE، Arch، Manjaro، ... — دسکتاپ یا سرور)
#
#   sudo ./install.sh              نصب (یا به‌روزرسانی)
#   sudo ./install.sh --no-tray    بدون نصب بسته‌های آیکون کنار ساعت (مثلاً روی سرور)
#   sudo ./install.sh --uninstall  حذف کامل
set -e
cd "$(dirname "$0")"

PREFIX=/usr/local
LIBDIR=$PREFIX/lib/tanbaltype
MANIFEST=$PREFIX/share/tanbaltype/installed-files.txt

if [ "$(id -u)" != 0 ]; then
    if command -v sudo >/dev/null 2>&1; then
        exec sudo sh "$0" "$@"
    fi
    echo "این اسکریپت باید با کاربر root اجرا شود." >&2
    exit 1
fi

say() { printf '==> %s\n' "$*"; }

reload_udev() {
    modprobe uinput >/dev/null 2>&1 || true
    if command -v udevadm >/dev/null 2>&1; then
        udevadm control --reload-rules >/dev/null 2>&1 || true
        udevadm trigger --subsystem-match=misc --sysname-match=uinput >/dev/null 2>&1 || true
        udevadm trigger --subsystem-match=input --action=change >/dev/null 2>&1 || true
    fi
}

uninstall() {
    if [ ! -f "$MANIFEST" ]; then
        echo "TanbalType با این نصب‌کننده نصب نشده است." >&2
        exit 1
    fi
    pkill -f "tanbaltype( run| tray|\.cli)" >/dev/null 2>&1 || true
    while IFS= read -r f; do
        rm -f "$f"
    done < "$MANIFEST"
    rm -f "$MANIFEST"
    rm -rf "$LIBDIR" "$PREFIX/share/tanbaltype" "$PREFIX/share/doc/tanbaltype"
    reload_udev
    say "TanbalType حذف شد. (تنظیمات کاربران در ~/.config/tanbaltype باقی مانده است)"
}

# نصب بسته با مدیر بستهٔ توزیع
pkg_install() {
    if command -v apt-get >/dev/null 2>&1; then
        DEBIAN_FRONTEND=noninteractive apt-get install -y "$@"
    elif command -v dnf >/dev/null 2>&1; then
        dnf install -y "$@"
    elif command -v yum >/dev/null 2>&1; then
        yum install -y "$@"
    elif command -v zypper >/dev/null 2>&1; then
        zypper --non-interactive install "$@"
    elif command -v pacman >/dev/null 2>&1; then
        pacman -S --needed --noconfirm "$@"
    elif command -v apk >/dev/null 2>&1; then
        apk add "$@"
    else
        return 1
    fi
}

ensure_python() {
    if command -v python3 >/dev/null 2>&1 &&
        python3 -c 'import sys; sys.exit(sys.version_info < (3, 8))' 2>/dev/null; then
        return
    fi
    say "نصب Python 3 ..."
    if command -v pacman >/dev/null 2>&1; then
        pkg_install python || true
    else
        pkg_install python3 || true
    fi
    if ! command -v python3 >/dev/null 2>&1; then
        echo "Python 3.8 یا جدیدتر لازم است؛ آن را نصب کنید و دوباره اجرا کنید." >&2
        exit 1
    fi
}

has_desktop() {
    ls /usr/share/xsessions/*.desktop /usr/share/wayland-sessions/*.desktop >/dev/null 2>&1
}

install_tray_deps() {
    # بسته‌های اختیاری: آیکون کنار ساعت و اعلان‌ها. نبودنشان مانع کار برنامه نیست.
    if python3 -c 'import gi; gi.require_version("Gtk", "3.0"); from gi.repository import Gtk' 2>/dev/null &&
        python3 -c '
import gi
for n in ("AyatanaAppIndicator3", "AppIndicator3"):
    try:
        gi.require_version(n, "0.1"); __import__("gi.repository." + n); raise SystemExit(0)
    except (ValueError, ImportError): pass
raise SystemExit(1)' 2>/dev/null; then
        return
    fi
    say "نصب بسته‌های آیکون کنار ساعت ..."
    if command -v apt-get >/dev/null 2>&1; then
        pkg_install python3-gi gir1.2-gtk-3.0 gir1.2-ayatanaappindicator3-0.1 libnotify-bin ||
            pkg_install python3-gi gir1.2-gtk-3.0 gir1.2-appindicator3-0.1 libnotify-bin || true
    elif command -v dnf >/dev/null 2>&1 || command -v yum >/dev/null 2>&1; then
        pkg_install python3-gobject gtk3 libayatana-appindicator-gtk3 libnotify || true
    elif command -v zypper >/dev/null 2>&1; then
        pkg_install python3-gobject python3-gobject-Gdk typelib-1_0-AyatanaAppIndicator3-0_1 libnotify-tools || true
    elif command -v pacman >/dev/null 2>&1; then
        pkg_install python-gobject gtk3 libayatana-appindicator libnotify || true
    fi
}

WITH_TRAY=auto
for arg in "$@"; do
    case "$arg" in
        --uninstall) uninstall; exit 0 ;;
        --no-tray) WITH_TRAY=no ;;
        --tray) WITH_TRAY=yes ;;
        *) echo "گزینهٔ ناشناخته: $arg" >&2; exit 1 ;;
    esac
done

ensure_python

# نصب قبلی (برای به‌روزرسانی تمیز)
if [ -f "$MANIFEST" ]; then
    while IFS= read -r f; do rm -f "$f"; done < "$MANIFEST"
    rm -rf "$LIBDIR"
fi

say "کپی فایل‌ها در $PREFIX ..."
mkdir -p "$(dirname "$MANIFEST")"
python3 tools/stage.py --root / --prefix "$PREFIX" --manifest "$MANIFEST.new"
mv "$MANIFEST.new" "$MANIFEST"
python3 -m compileall -q "$LIBDIR" >/dev/null 2>&1 || true

say "تنظیم دسترسی به کیبورد (udev + uinput) ..."
reload_udev
if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -q -t "$PREFIX/share/icons/hicolor" >/dev/null 2>&1 || true
fi

if [ "$WITH_TRAY" = yes ] || { [ "$WITH_TRAY" = auto ] && has_desktop; }; then
    install_tray_deps
fi

say "آزمون الگوریتم تشخیص ..."
"$PREFIX/bin/tanbaltype" selftest | tail -1

cat <<'EOF'

✅ TanbalType نصب شد.

  • از منوی برنامه‌ها «TanbalType» را اجرا کنید، یا در ترمینال:  tanbaltype start
  • از این به بعد هنگام ورود به سیستم خودکار اجرا می‌شود (tanbaltype autostart off برای خاموش کردن)
  • F10 برای فعال/غیرفعال کردن
  • روی سرور یا SSH:  tanbaltype convert "sghl"   →  سلام

اگر برنامه گفت «به کیبورد دسترسی ندارد»، یک بار از سیستم خارج و دوباره وارد شوید.
EOF
