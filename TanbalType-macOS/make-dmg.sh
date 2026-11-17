#!/bin/bash
# ساخت فایل نصبی DMG برای TanbalType — پنجره‌ای که کاربر در آن برنامه را روی
# میان‌بر Applications می‌کشد.
#
#   ./make-dmg.sh                  → build/TanbalType-macOS-<version>.dmg
#   ./make-dmg.sh --app <path>     → از یک .app در مسیر دیگر
#   ./make-dmg.sh --out <path>     → نام/مسیر دلخواه برای خروجی
#
# build.sh خودش در پایانِ بیلد این اسکریپت را صدا می‌زند؛ جدا اجرا کردنش وقتی
# به‌کار می‌آید که .app را از قبل ساخته باشید و فقط بخواهید بسته‌بندی را دوباره
# انجام دهید.
#
# فقط روی macOS کار می‌کند: به hdiutil نیاز دارد و برای چیدنِ پنجره به Finder.
# اگر امضای Developer ID دارید، با تنظیم CODESIGN_IDENTITY خودِ DMG هم امضا
# می‌شود:
#
#   CODESIGN_IDENTITY="Developer ID Application: NAME (TEAMID)" ./make-dmg.sh
set -euo pipefail
cd "$(dirname "$0")"

APP="build/TanbalType.app"
DMG=""

while [[ $# -gt 0 ]]; do
    case "$1" in
        --app)  APP="${2:?--app needs a path}";  shift 2 ;;
        --out)  DMG="${2:?--out needs a path}";  shift 2 ;;
        -h|--help)
            # توضیحِ بالای همین فایل: از خط دوم تا اولین خطی که کامنت نیست.
            awk 'NR>1 && /^#/ { sub(/^# ?/, ""); print; next } NR>1 { exit }' "$0"
            exit 0 ;;
        *) echo "make-dmg.sh: گزینهٔ ناشناخته: $1" >&2; exit 2 ;;
    esac
done

# ---------- پیش‌نیازها ----------
if [[ "$(uname -s)" != "Darwin" ]]; then
    echo "make-dmg.sh فقط روی macOS اجرا می‌شود (به hdiutil و Finder نیاز دارد)." >&2
    exit 1
fi
command -v hdiutil >/dev/null || { echo "hdiutil پیدا نشد." >&2; exit 1; }

if [[ ! -d "$APP" ]]; then
    echo "برنامه پیدا نشد: $APP" >&2
    echo "اول ./build.sh را اجرا کنید." >&2
    exit 1
fi

PLIST="$APP/Contents/Info.plist"
VERSION=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$PLIST")
[[ -n "$VERSION" ]] || { echo "نسخه از $PLIST خوانده نشد." >&2; exit 1; }

BUILD=build
VOLNAME="TanbalType $VERSION"
[[ -n "$DMG" ]] || DMG="$BUILD/TanbalType-macOS-$VERSION.dmg"
STAGE="$BUILD/dmg-stage"
TEMP_DMG="$BUILD/.TanbalType-rw.dmg"

# چیدمان پنجره. این عددها باید با tools/gen_dmg_background.py یکی باشند،
# وگرنه فلشِ پس‌زمینه به آیکن‌ها نمی‌خورد.
WIN_WIDTH=640
WIN_HEIGHT=400
ICON_SIZE=128
APP_X=160;  APP_Y=185
LINK_X=480; LINK_Y=185

echo "==> DMG برای TanbalType $VERSION"

# ---------- اگر وسط کار بیرون افتادیم، ایمیج را جدا کن ----------
DEVICE=""
cleanup() {
    if [[ -n "$DEVICE" ]] && hdiutil info 2>/dev/null | grep -q "^$DEVICE"; then
        hdiutil detach "$DEVICE" -force >/dev/null 2>&1 || true
    fi
}
trap cleanup EXIT

# ---------- محتویات ----------
rm -rf "$STAGE"
mkdir -p "$STAGE"
# COPYFILE_DISABLE جلوی ساختهٔ فایل‌های ._ را می‌گیرد (داخل DMG به‌درد نمی‌خورند).
COPYFILE_DISABLE=1 ditto "$APP" "$STAGE/TanbalType.app"
ln -s /Applications "$STAGE/Applications"

BACKGROUND_NAME=""
if [[ -f Resources/dmg-background.png ]]; then
    mkdir -p "$STAGE/.background"
    # tiffutil دو دقت را در یک فایل می‌گذارد تا روی Retina تار نشود.
    if command -v tiffutil >/dev/null && [[ -f "Resources/dmg-background@2x.png" ]]; then
        tiffutil -cathidpicheck Resources/dmg-background.png "Resources/dmg-background@2x.png" \
            -out "$STAGE/.background/background.tiff" >/dev/null
        BACKGROUND_NAME="background.tiff"
    else
        cp Resources/dmg-background.png "$STAGE/.background/background.png"
        BACKGROUND_NAME="background.png"
    fi
fi

# آیکن خودِ دیسک
[[ -f Resources/AppIcon.icns ]] && cp Resources/AppIcon.icns "$STAGE/.VolumeIcon.icns"

# ---------- ایمیج قابل‌نوشتن ----------
SIZE_MB=$(( $(du -sm "$STAGE" | cut -f1) + 32 ))
rm -f "$TEMP_DMG"
hdiutil create -srcfolder "$STAGE" -volname "$VOLNAME" -fs HFS+ \
    -fsargs "-c c=64,a=16,e=16" -format UDRW -size "${SIZE_MB}m" "$TEMP_DMG" >/dev/null

ATTACH=$(hdiutil attach -readwrite -noverify -noautoopen "$TEMP_DMG")
# `|| true` لازم است: با pipefail، بسته‌شدنِ زودهنگامِ لوله توسط head (یا
# نیافتنِ grep) کل انتساب را ناموفق می‌کند و اسکریپت پیش از پیامِ خطای پایین
# بیرون می‌افتد.
DEVICE=$(printf '%s\n' "$ATTACH" | grep -E '^/dev/' | head -1 | awk '{print $1}' || true)
MOUNT=$(printf '%s\n' "$ATTACH" | grep -E '^/dev/' | grep '/Volumes/' | head -1 | cut -f3- || true)
[[ -n "$MOUNT" && -d "$MOUNT" ]] || { echo "ایمیج mount نشد." >&2; exit 1; }

# اگر نسخه‌ای از همین DMG از قبل mount باشد، macOS نامِ دیسک را «… 1» می‌کند؛
# پس نامِ واقعی را از مسیرِ mount می‌گیریم تا Finder سراغ دیسکِ درست برود.
VOLUME_NAME=$(basename "$MOUNT")

# ---------- چیدمان پنجره ----------
# اگر Finder در دسترس نباشد (مثلاً روی یک سرور بیلد بدون نشست گرافیکی)، DMG
# ساخته می‌شود ولی بدون پس‌زمینه و چیدمان — پس شکست اینجا کشنده نیست.
if ! osascript <<APPLESCRIPT >/dev/null 2>&1
tell application "Finder"
    tell disk "$VOLUME_NAME"
        open
        set current view of container window to icon view
        set toolbar visible of container window to false
        set statusbar visible of container window to false
        set the bounds of container window to {200, 120, $((200 + WIN_WIDTH)), $((120 + WIN_HEIGHT))}
        set opts to the icon view options of container window
        set arrangement of opts to not arranged
        set icon size of opts to $ICON_SIZE
        set text size of opts to 12
        $( [[ -n "$BACKGROUND_NAME" ]] && echo "set background picture of opts to file \".background:$BACKGROUND_NAME\"" )
        set position of item "TanbalType.app" of container window to {$APP_X, $APP_Y}
        set position of item "Applications" of container window to {$LINK_X, $LINK_Y}
        close
        open
        update without registering applications
        delay 2
    end tell
end tell
APPLESCRIPT
then
    echo "    (هشدار: Finder پاسخ نداد؛ DMG بدون چیدمانِ پنجره ساخته می‌شود)"
fi

# دیسک را «آیکن سفارشی دارد» علامت بزن
if [[ -f "$MOUNT/.VolumeIcon.icns" ]] && command -v SetFile >/dev/null; then
    SetFile -a C "$MOUNT" || true
fi

chmod -Rf go-w "$MOUNT" 2>/dev/null || true
sync

# ---------- فشرده‌سازی ----------
# گاهی Finder هنوز دیسک را رها نکرده؛ چند بار تلاش می‌کنیم.
for attempt in 1 2 3 4 5; do
    if hdiutil detach "$DEVICE" >/dev/null 2>&1; then
        DEVICE=""
        break
    fi
    [[ $attempt -eq 5 ]] && { hdiutil detach "$DEVICE" -force >/dev/null 2>&1 || true; DEVICE=""; }
    sleep 2
done

rm -f "$DMG"
hdiutil convert "$TEMP_DMG" -format UDZO -imagekey zlib-level=9 -o "$DMG" >/dev/null
rm -f "$TEMP_DMG"
rm -rf "$STAGE"

if [[ -n "${CODESIGN_IDENTITY:-}" ]]; then
    echo "==> امضای DMG"
    codesign --force --sign "$CODESIGN_IDENTITY" "$DMG"
fi

echo "==> $DMG"
shasum -a 256 "$DMG"
