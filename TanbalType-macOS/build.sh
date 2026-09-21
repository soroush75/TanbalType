#!/bin/bash
# ساخت TanbalType.app برای macOS (Apple Silicon + Intel).
# پیش‌نیاز: Xcode یا Command Line Tools  (xcode-select --install)
#
#   ./build.sh            → build/TanbalType.app  و  فایل‌های zip و dmg در build/
#   ./build.sh --install  → علاوه بر آن، برنامه را در /Applications کپی می‌کند
set -euo pipefail
cd "$(dirname "$0")"

CS_DIR=../TanbalType
VERSION=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CS_DIR/TanbalType.csproj" | head -1)
BUNDLE_ID=io.github.soroush75.TanbalType
MIN_MACOS=11.0
BUILD=build
APP=$BUILD/TanbalType.app

echo "==> TanbalType $VERSION"

# فهرست‌های لغت از سورس C# (منبع واحد) ساخته می‌شوند
python3 tools/gen_detector_data.py

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

ARCH_BINS=()
for ARCH in arm64 x86_64; do
    echo "==> compiling $ARCH"
    swiftc -O -swift-version 5 -module-name TanbalType \
        -target "$ARCH-apple-macos$MIN_MACOS" \
        Sources/*.swift Sources/Generated/*.swift \
        -o "$BUILD/TanbalType-$ARCH"
    ARCH_BINS+=("$BUILD/TanbalType-$ARCH")
done
lipo -create "${ARCH_BINS[@]}" -output "$APP/Contents/MacOS/TanbalType"
rm -f "${ARCH_BINS[@]}"

cp "$CS_DIR/PersianWords.txt" "$APP/Contents/Resources/"
cp Resources/AppIcon.icns "$APP/Contents/Resources/"

cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key><string>TanbalType</string>
    <key>CFBundleDisplayName</key><string>TanbalType</string>
    <key>CFBundleIdentifier</key><string>$BUNDLE_ID</string>
    <key>CFBundleExecutable</key><string>TanbalType</string>
    <key>CFBundleIconFile</key><string>AppIcon</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>$VERSION</string>
    <key>CFBundleVersion</key><string>$VERSION</string>
    <key>CFBundleDevelopmentRegion</key><string>fa</string>
    <key>LSMinimumSystemVersion</key><string>$MIN_MACOS</string>
    <key>LSUIElement</key><true/>
    <key>NSHumanReadableCopyright</key><string>GPLv3 — سروش سرمست</string>
</dict>
</plist>
PLIST

# امضای ad-hoc (برای اجرای محلی کافی است؛ برای توزیع عمومی امضای Developer ID لازم است)
codesign --force --deep --sign - "$APP"

echo "==> self-test"
"$APP/Contents/MacOS/TanbalType" --selftest | grep "SelfTest"

ZIP="$BUILD/TanbalType-macOS-$VERSION.zip"
rm -f "$ZIP"
ditto -c -k --keepParent "$APP" "$ZIP"
# فایل نصبی DMG: برنامه + میان‌بر Applications برای نصب با کشیدن و رها کردن
DMG="$BUILD/TanbalType-macOS-$VERSION.dmg"
STAGE="$BUILD/dmg"
rm -rf "$STAGE" "$DMG"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -s /Applications "$STAGE/Applications"
hdiutil create -volname "TanbalType $VERSION" -srcfolder "$STAGE" -fs HFS+ -format UDZO -ov "$DMG" >/dev/null
rm -rf "$STAGE"

echo "==> $APP"
echo "==> $ZIP"
echo "==> $DMG"

if [[ "${1:-}" == "--install" ]]; then
    osascript -e 'quit app "TanbalType"' 2>/dev/null || true
    rm -rf /Applications/TanbalType.app
    cp -R "$APP" /Applications/
    echo "==> installed to /Applications/TanbalType.app"
fi
