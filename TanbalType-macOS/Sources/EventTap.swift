import AppKit

/// معادل low-level keyboard hook ویندوز: همهٔ کلیدهای سیستم را قبل از رسیدن به برنامهٔ فعال می‌بیند.
/// نیاز به دسترسی Accessibility دارد.
final class EventTap {
    private let service: CorrectionService
    private var tap: CFMachPort?
    private var runLoopSource: CFRunLoopSource?
    private var toggleKeyHeld = false

    /// کلیدهایی که کاربر در حین اصلاح زده؛ پس از پایان اصلاح به همان ترتیب دوباره ارسال می‌شوند
    /// تا لابه‌لای متنِ اصلاح‌شده قرار نگیرند.
    private var heldEvents: [CGEvent] = []

    /// با فشردن F10 صدا زده می‌شود (روی نخ اصلی).
    var toggleRequested: (() -> Void)?

    var isInstalled: Bool { tap != nil }

    init(service: CorrectionService) {
        self.service = service
        service.correctionFinished = { [weak self] in self?.replayHeldEvents() }
    }

    func install() -> Bool {
        if tap != nil { return true }

        let mask = CGEventMask(1 << CGEventType.keyDown.rawValue) | CGEventMask(1 << CGEventType.keyUp.rawValue)
        let callback: CGEventTapCallBack = { _, type, event, refcon in
            guard let refcon else { return Unmanaged.passUnretained(event) }
            let tap = Unmanaged<EventTap>.fromOpaque(refcon).takeUnretainedValue()
            return tap.handle(type: type, event: event)
        }

        guard let tap = CGEvent.tapCreate(
            tap: .cgSessionEventTap,
            place: .headInsertEventTap,
            options: .defaultTap,
            eventsOfInterest: mask,
            callback: callback,
            userInfo: Unmanaged.passUnretained(self).toOpaque())
        else {
            AppLog.write("CGEvent.tapCreate failed (Accessibility permission?)")
            return false
        }

        let source = CFMachPortCreateRunLoopSource(kCFAllocatorDefault, tap, 0)
        CFRunLoopAddSource(CFRunLoopGetMain(), source, .commonModes)
        CGEvent.tapEnable(tap: tap, enable: true)
        self.tap = tap
        runLoopSource = source
        AppLog.write("Event tap installed.")
        return true
    }

    func uninstall() {
        guard let tap else { return }
        CGEvent.tapEnable(tap: tap, enable: false)
        if let runLoopSource {
            CFRunLoopRemoveSource(CFRunLoopGetMain(), runLoopSource, .commonModes)
        }
        CFMachPortInvalidate(tap)
        self.tap = nil
        runLoopSource = nil
    }

    private func handle(type: CGEventType, event: CGEvent) -> Unmanaged<CGEvent>? {
        // macOS اگر callback دیر جواب بدهد tap را غیرفعال می‌کند؛ دوباره روشنش می‌کنیم
        if type == .tapDisabledByTimeout || type == .tapDisabledByUserInput {
            AppLog.write("Event tap disabled (\(type.rawValue)), re-enabling")
            if let tap { CGEvent.tapEnable(tap: tap, enable: true) }
            return Unmanaged.passUnretained(event)
        }

        guard type == .keyDown || type == .keyUp, !InputSimulator.isSynthetic(event) else {
            return Unmanaged.passUnretained(event)
        }

        let code = UInt16(event.getIntegerValueField(.keyboardEventKeycode))

        // میان‌بر پیش از بررسی Enabled سنجیده می‌شود تا در حالت غیرفعال هم بتواند برنامه را برگرداند
        if code == KeyCodes.f10 && handleToggleHotkey(type: type, event: event) {
            return nil
        }

        if service.isCorrecting {
            if let copy = event.copy() {
                heldEvents.append(copy)
                return nil
            }
            return Unmanaged.passUnretained(event)
        }

        if type == .keyDown && service.handleKeyDown(event) {
            return nil
        }

        return Unmanaged.passUnretained(event)
    }

    private func replayHeldEvents() {
        let events = heldEvents
        heldEvents.removeAll()
        // دوباره از ابتدای مسیر رویدادها عبور می‌کنند و مثل تایپ عادی پردازش می‌شوند
        for event in events {
            event.post(tap: .cghidEventTap)
        }
    }

    /// F10 تنها (بدون Cmd/Ctrl/Option/Shift) برنامه را فعال/غیرفعال می‌کند و خودِ کلید مصرف می‌شود.
    /// ترکیب‌هایی مثل Shift+F10 دست‌نخورده عبور می‌کنند.
    private func handleToggleHotkey(type: CGEventType, event: CGEvent) -> Bool {
        if type == .keyDown {
            let flags = event.flags
            if flags.contains(.maskCommand) || flags.contains(.maskControl)
                || flags.contains(.maskAlternate) || flags.contains(.maskShift) {
                return false
            }

            if toggleKeyHeld {
                return true // نگه‌داشتن کلید (auto-repeat) نباید پشت‌سرهم سوییچ کند
            }
            toggleKeyHeld = true
            DispatchQueue.main.async { [weak self] in self?.toggleRequested?() }
            return true
        }

        // keydown مصرف نشده بود (مثلاً Shift+F10)، پس keyup هم باید عبور کند
        if !toggleKeyHeld {
            return false
        }
        toggleKeyHeld = false
        return true
    }
}
