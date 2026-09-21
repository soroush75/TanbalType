# TanbalType FA — نسخهٔ لینوکس 🐧⌨️

![Platform](https://img.shields.io/badge/Platform-Linux-yellow.svg)
![Python](https://img.shields.io/badge/Python-3.8%2B-blue.svg)

<div align="right" dir="rtl">

نسخهٔ لینوکسِ همان برنامهٔ TanbalType: اگر روی حالت انگلیسی «sghl» تایپ کنید، با زدن Space به «سلام» تبدیل می‌شود (و بالعکس) و زبان کیبورد هم خودکار عوض می‌شود. روی X11 و Wayland کار می‌کند و به هیچ کتابخانهٔ جانبی جز Python 3 نیاز ندارد.

## ✨ قابلیت‌ها
همهٔ قابلیت‌های نسخهٔ ویندوز و مک:
* اصلاح دوطرفه (فارسی ↔ انگلیسی) با همان لغت‌نامهٔ ۸۹ هزار کلمه‌ای و همان الگوریتم تشخیص
* لغات شخصی و لغات استثنا (از منوی آیکون یا خط فرمان)
* محافظت از آدرس سایت‌ها و ایمیل‌ها، و عدم اصلاح کلمات دارای عدد (رمز، کد و ...)
* کلید میان‌بر **F10** برای فعال/غیرفعال کردن
* اجرای خودکار هنگام ورود به سیستم
* ذخیرهٔ log برای عیب‌یابی (پیش‌فرض خاموش)
* **روی سرور و SSH:** دستور `tanbaltype convert` متن را در ترمینال تبدیل می‌کند

### محیط‌های پشتیبانی‌شده
| محیط دسکتاپ | روش تعویض زبان |
|---|---|
| GNOME (اوبونتو، دبیان، فدورا، ...) — X11 و Wayland | gsettings + میان‌بر تعویض زبان (Super+Space) |
| KDE Plasma — X11 و Wayland | D-Bus |
| Cinnamon (لینوکس مینت)، MATE، XFCE، LXQt، Budgie و هر محیط X11 دیگر | XKB |
| Sway / Hyprland | swaymsg / hyprctl |

چیدمان‌های فارسی: **Persian** (استاندارد لینوکس — «پ» روی کلید m) و **Persian (Windows)** («پ» روی کلید `\`). برنامه خودش تشخیص می‌دهد کدام را فعال کرده‌اید.

## 🚀 نصب

### Debian، Ubuntu، Linux Mint، Pop!_OS، ...
فایل `tanbaltype_1.4.0_all.deb` را دانلود کنید و:

</div>

```bash
sudo apt install ./tanbaltype_1.4.0_all.deb
```

<div align="right" dir="rtl">

(یا روی فایل دو بار کلیک کنید تا با Software Center نصب شود.)

### همهٔ توزیع‌ها (Fedora، openSUSE، Arch، Manjaro، ...)
فایل `TanbalType-linux-1.4.0.tar.gz` را دانلود کنید و:

</div>

```bash
tar xf TanbalType-linux-1.4.0.tar.gz
cd TanbalType-linux-1.4.0
sudo ./install.sh
```

<div align="right" dir="rtl">

نصب‌کننده Python و بسته‌های آیکون کنار ساعت را با مدیر بستهٔ همان توزیع (apt، dnf، zypper، pacman) نصب می‌کند. حذف: `sudo ./install.sh --uninstall`

* **Fedora / openSUSE (بستهٔ RPM):** `rpmbuild -tb TanbalType-linux-1.4.0.tar.gz`
* **Arch / Manjaro:** فایل‌های `PKGBUILD` و `tanbaltype.install` را کنار tar.gz بگذارید و `makepkg -si`

### سرور (بدون محیط گرافیکی)
روی سرور همان روش‌های بالا کار می‌کند. برای این‌که بسته‌های گرافیکی نصب نشوند:

</div>

```bash
sudo apt install --no-install-recommends ./tanbaltype_1.4.0_all.deb
# یا
sudo ./install.sh --no-tray
```

<div align="right" dir="rtl">

> **نکته:** وقتی با SSH به سرور وصل می‌شوید، کلیدها روی کامپیوتر خودتان زده می‌شوند، نه روی سرور؛ پس اصلاح خودکار باید روی کامپیوتر خودتان نصب باشد. روی سرور دستور `convert` در دسترس است:

</div>

```bash
tanbaltype convert "sghl ofdv"          # → سلام خبیر
echo "اثممخ" | tanbaltype convert        # → hello
tanbaltype convert --swap "sghl"         # بدون تشخیص، همه را برعکس کن
```

<div align="right" dir="rtl">

### اولین اجرا
از منوی برنامه‌ها **TanbalType** را اجرا کنید (یا `tanbaltype start`). از این به بعد هنگام ورود به سیستم خودکار اجرا می‌شود.

پیش‌نیاز: یک چیدمان فارسی و یک چیدمان انگلیسی در تنظیمات کیبورد سیستم (Settings › Keyboard › Input Sources) فعال باشد.

اگر پیغام «به کیبورد دسترسی ندارد» دیدید، یک بار از سیستم خارج و دوباره وارد شوید. اگر باز هم ماند: `sudo tanbaltype setup`

* **GNOME:** برای دیدن آیکون کنار ساعت، افزونهٔ AppIndicator لازم است (در اوبونتو از قبل نصب است؛ در دبیان/فدورا: `gnome-shell-extension-appindicator`). بدون آیکون هم برنامه کار می‌کند.
* **Sway / Hyprland:** این خط را به تنظیمات اضافه کنید: `exec tanbaltype start`

## ⌨️ خط فرمان

</div>

| دستور | کار |
|---|---|
| `tanbaltype start` / `stop` / `restart` | اجرا / توقف |
| `tanbaltype status` | وضعیت (روش تعویض زبان، چیدمان فارسی، کیبوردها) |
| `tanbaltype enable` / `disable` / `toggle` | فعال/غیرفعال (مثل F10) |
| `tanbaltype words add GitHub` | افزودن لغت شخصی (`remove` برای حذف، بدون آرگومان: فهرست) |
| `tanbaltype exceptions add word` | افزودن لغت استثنا |
| `tanbaltype convert TEXT` | تبدیل متن |
| `tanbaltype autostart on` / `off` | اجرای خودکار هنگام ورود |
| `tanbaltype log on` / `off` / `show` | ذخیره و نمایش log |
| `tanbaltype run -v` | اجرا در ترمینال با نمایش همهٔ رویدادها (عیب‌یابی) |

<div align="right" dir="rtl">

## 📁 فایل‌ها
| مسیر | محتوا |
|---|---|
| `~/.config/tanbaltype/config.ini` | تنظیمات (روش تعویض زبان، چیدمان فارسی، کلید میان‌بر) |
| `~/.config/tanbaltype/userwords.txt` | لغات شخصی |
| `~/.config/tanbaltype/exceptions.txt` | لغات استثنا |
| `~/.local/state/tanbaltype/tanbaltype.log` | log (فقط وقتی فعال باشد) |

## 🔒 امنیت و حریم خصوصی
* برنامه با کاربر عادی اجرا می‌شود، نه root. یک قانون udev (`70-tanbaltype.rules`) فقط به کاربری که پشت سیستم وارد شده اجازهٔ دسترسی به کیبورد و `/dev/uinput` می‌دهد.
* هیچ داده‌ای به اینترنت فرستاده نمی‌شود. log پیش‌فرض خاموش است.
* لینوکس (برخلاف مک) کادر رمز عبور را به برنامه‌ها اعلام نمی‌کند؛ ولی رمزها معمولاً با Enter تمام می‌شوند و کلمات دارای عدد هم اصلاح نمی‌شوند. اگر رمزی بدون عدد دارید، آن را به «لغات استثنا» اضافه کنید یا با F10 برنامه را موقتاً خاموش کنید.

## 🛠 برای توسعه‌دهنده
* منطق تشخیص (`tanbaltype/detector.py`) پورت مستقیم `TanbalType/Detector.cs` است.
* **فهرست لغات فقط یک جا نگهداری می‌شود:** `build.sh` فهرست‌ها را از `Detector.cs` و `DetectorSelfTest.cs` استخراج می‌کند (`tools/gen_detector_data.py`) و `PersianWords.txt` را از پوشهٔ نسخهٔ ویندوز برمی‌دارد.
* ساخت بسته‌ها (روی هر سیستمی با Python 3، حتی مک): `./build.sh` ← پوشهٔ `build/`
* اجرا از سورس بدون نصب: `python3 -m tanbaltype run -v`
* آزمون‌ها: `python3 -m tanbaltype selftest` و `python3 -m unittest discover -s tests`

</div>

| نسخهٔ ویندوز | نسخهٔ لینوکس |
|---|---|
| `SetWindowsHookEx` (WH_KEYBOARD_LL) | خواندن `/dev/input/event*` با EVIOCGRAB — `evdev.py` |
| `SendInput` | کیبورد مجازی `/dev/uinput` — `evdev.py` |
| `ActivateKeyboardLayout` | gsettings / D-Bus / XKB / swaymsg / hyprctl — `layout.py` |
| `NotifyIcon` | AppIndicator (پروسهٔ جدا) — `tray.py` |
| Balloon tip | `notify-send` — `notify.py` |
| Shortcut در پوشهٔ Startup | `/etc/xdg/autostart` — `cli.py` |

<div align="right" dir="rtl">

**چطور کار می‌کند:** کیبوردها grab می‌شوند (همهٔ کلیدها اول به برنامه می‌رسند) و بی‌درنگ از کیبورد مجازی عبور داده می‌شوند. این‌طوری برنامه می‌تواند Space را تا پایان اصلاح نگه دارد و کلیدهایی که حین اصلاح زده می‌شوند به ترتیب بعد از متن اصلاح‌شده قرار می‌گیرند. اگر برنامه به هر دلیل بسته شود، هستهٔ لینوکس grab را خودکار آزاد می‌کند و کیبورد عادی کار می‌کند. کیبوردهایی که تاچ‌پد/موس هم دارند grab نمی‌شوند و فقط شنیده می‌شوند (اصلاح فقط با Space).

</div>
