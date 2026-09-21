# TanbalType FA — نسخهٔ macOS 🍎⌨️

![Platform](https://img.shields.io/badge/Platform-macOS%2011%2B-lightgrey.svg)
![Swift](https://img.shields.io/badge/Swift-5-orange.svg)

<div align="right" dir="rtl">

نسخهٔ مکِ همان برنامهٔ TanbalType: اگر روی حالت انگلیسی «sghl» تایپ کنید، با زدن Space به «سلام» تبدیل می‌شود (و بالعکس) و زبان کیبورد هم خودکار عوض می‌شود. برنامه در نوار منوی بالای صفحه (کنار ساعت) اجرا می‌شود و آیکون Dock ندارد.

## ✨ قابلیت‌ها
همهٔ قابلیت‌های نسخهٔ ویندوز:
* اصلاح دوطرفه (فارسی ↔ انگلیسی) با همان لغت‌نامهٔ ۸۹ هزار کلمه‌ای و همان الگوریتم تشخیص
* لغات شخصی و لغات استثنا (از منوی آیکون)
* محافظت از آدرس سایت‌ها و ایمیل‌ها، و عدم اصلاح کلمات دارای عدد (رمز، کد و ...)
* کلید میان‌بر **F10** برای فعال/غیرفعال کردن
* اجرای خودکار هنگام ورود به سیستم
* ذخیرهٔ log برای عیب‌یابی (پیش‌فرض خاموش)

تفاوت‌های نسخهٔ مک:
* **پشتیبانی از همهٔ چیدمان‌های فارسی مک:** چیدمان‌های فارسیِ مک با ویندوز فرق دارند (مثلاً در چیدمان استاندارد مک «پ» روی کلید `m` است). برنامه نگاشت کلیدها را از روی همان چیدمانِ فارسی‌ای که در سیستم فعال کرده‌اید می‌سازد. چیدمان پیشنهادی: **Persian - ISIRI 2901** (استاندارد).
* **کادر رمز عبور:** macOS هنگام تایپ رمز «Secure Input» را روشن می‌کند و برنامه در آن حالت هیچ کلیدی را نمی‌بیند و اصلاح نمی‌کند.
* کلیدهایی که حین اصلاح زده شوند نگه داشته و بعد از اصلاح به همان ترتیب ارسال می‌شوند تا لابه‌لای متن اصلاح‌شده قرار نگیرند.

## 🚀 نصب و اجرا

### پیش‌نیاز
* macOS 11 (Big Sur) یا جدیدتر — Apple Silicon و Intel
* فعال بودن یک چیدمان فارسی و یک چیدمان انگلیسی در
  System Settings › Keyboard › Input Sources

### ساخت از روی سورس
به Xcode یا Command Line Tools نیاز دارید (`xcode-select --install`):

</div>

```bash
cd TanbalType-macOS
./build.sh --install
```

<div align="right" dir="rtl">

خروجی `build/TanbalType.app` است (و یک فایل zip برای انتقال به مک دیگر). گزینهٔ `--install` برنامه را در پوشهٔ Applications کپی می‌کند.

### اولین اجرا — دسترسی Accessibility
برنامه برای دیدن و اصلاح کلیدها به دسترسی **Accessibility** نیاز دارد:
1. برنامه را اجرا کنید؛ macOS پنجرهٔ درخواست دسترسی را نشان می‌دهد.
2. در System Settings › Privacy & Security › **Accessibility** تیک TanbalType را بزنید.
3. چند ثانیه بعد برنامه خودکار فعال می‌شود (نیازی به اجرای مجدد نیست).

> **نکته:** برنامه با امضای ad-hoc ساخته می‌شود. بعد از هر build جدید، macOS دسترسی قبلی را معتبر نمی‌داند؛
> TanbalType را با دکمهٔ «−» از فهرست Accessibility حذف و دوباره اضافه کنید.
>
> اگر فایل zip را روی مک دیگری باز کردید و macOS اجازهٔ اجرا نداد، به System Settings › Privacy & Security
> بروید و دکمهٔ **Open Anyway** را بزنید (یا دستور `xattr -dr com.apple.quarantine /Applications/TanbalType.app` را اجرا کنید).

### کلید F10
در بیشتر مک‌ها کلیدهای F به‌صورت پیش‌فرض کار رسانه‌ای (صدا، نور و ...) انجام می‌دهند. برای استفاده از میان‌بر، یا **fn+F10** را بزنید، یا در System Settings › Keyboard گزینهٔ «Use F1, F2, etc. keys as standard function keys» را روشن کنید.

## 📁 فایل‌ها
| مسیر | محتوا |
|---|---|
| `~/Library/Application Support/TanbalType/userwords.txt` | لغات شخصی |
| `~/Library/Application Support/TanbalType/exceptions.txt` | لغات استثنا |
| `~/Library/Logs/TanbalType/TanbalType.log` | log (فقط وقتی فعال باشد) |

## 🛠 برای توسعه‌دهنده
* منطق تشخیص (`Sources/Detector.swift`) پورت مستقیم `TanbalType/Detector.cs` است.
* **فهرست لغات فقط یک جا نگهداری می‌شود:** `build.sh` هر بار فهرست‌ها را از `Detector.cs` و `DetectorSelfTest.cs` استخراج می‌کند (`tools/gen_detector_data.py`) و `PersianWords.txt` را هم از پوشهٔ نسخهٔ ویندوز برمی‌دارد. پس تغییر فهرست‌ها در نسخهٔ ویندوز خودکار به نسخهٔ مک هم می‌رسد.
* build در پایان self-test تشخیص را اجرا می‌کند و اگر موردی خطا بدهد build شکست می‌خورد. برای گزارش روی همهٔ چیدمان‌های فارسی نصب‌شده:

</div>

```bash
build/TanbalType.app/Contents/MacOS/TanbalType --selftest-all
```

<div align="right" dir="rtl">

| نسخهٔ ویندوز | نسخهٔ مک |
|---|---|
| `SetWindowsHookEx` (WH_KEYBOARD_LL) | `CGEvent.tapCreate` — `EventTap.swift` |
| `SendInput` | `CGEvent.post` — `InputSimulator.swift` |
| `ActivateKeyboardLayout` / Alt+Shift | `TISSelectInputSource` — `LayoutManager.swift` |
| `NotifyIcon` | `NSStatusItem` — `AppDelegate.swift` |
| Balloon tip | HUD — `Support.swift` |
| Shortcut در پوشهٔ Startup | `SMAppService` — `Support.swift` |

</div>
