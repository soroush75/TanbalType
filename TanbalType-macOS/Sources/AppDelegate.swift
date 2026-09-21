import AppKit
import ApplicationServices
import Carbon

final class AppDelegate: NSObject, NSApplicationDelegate, NSMenuDelegate {
    private let service = CorrectionService()
    private lazy var eventTap = EventTap(service: service)

    private var statusItem: NSStatusItem!
    private let enabledItem = NSMenuItem()
    private let logItem = NSMenuItem()
    private let startupItem = NSMenuItem()
    private let permissionItem = NSMenuItem()
    private var permissionTimer: Timer?

    private var exceptionsWindow: WordListWindow?
    private var userWordsWindow: WordListWindow?

    func applicationDidFinishLaunching(_ notification: Notification) {
        // دو نسخهٔ هم‌زمان هر کلمه را دو بار اصلاح می‌کنند
        if let id = Bundle.main.bundleIdentifier,
           NSRunningApplication.runningApplications(withBundleIdentifier: id).count > 1 {
            NSApp.terminate(nil)
            return
        }

        AppLog.writeHeader()

        // لغت‌نامه (~۳۳۰ هزار کلمه) در پس‌زمینه بارگذاری می‌شود تا اولین کلید معطل نشود
        DispatchQueue.global(qos: .userInitiated).async {
            _ = Detector.persianWords.count
        }

        LayoutManager.refresh()
        DistributedNotificationCenter.default().addObserver(
            self, selector: #selector(inputSourceChanged),
            name: NSNotification.Name(kTISNotifySelectedKeyboardInputSourceChanged as String), object: nil)

        // با تغییر برنامهٔ فعال، بافر پاک می‌شود
        NSWorkspace.shared.notificationCenter.addObserver(
            self, selector: #selector(activeAppChanged),
            name: NSWorkspace.didActivateApplicationNotification, object: nil)

        eventTap.toggleRequested = { [weak self] in
            guard let self else { return }
            self.setEnabled(!self.service.enabled, notify: true)
        }

        buildMenu()
        startTap()
    }

    func applicationWillTerminate(_ notification: Notification) {
        eventTap.uninstall()
        AppLog.write("Application exit.")
    }

    // MARK: - Event tap & permission

    private func startTap() {
        if AXIsProcessTrusted() && eventTap.install() {
            permissionItem.isHidden = true
            updateStatusIcon()
            HUD.show("\(AppInfo.name) فعال شد — F10 برای فعال/غیرفعال", duration: 2.5)
            return
        }

        // درخواست دسترسی Accessibility (سیستم پنجرهٔ خودش را نشان می‌دهد)
        let options = [kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String: true] as CFDictionary
        _ = AXIsProcessTrustedWithOptions(options)

        permissionItem.isHidden = false
        updateStatusIcon()
        AppLog.write("Waiting for Accessibility permission")

        permissionTimer?.invalidate()
        permissionTimer = Timer.scheduledTimer(withTimeInterval: 1.5, repeats: true) { [weak self] timer in
            guard let self, AXIsProcessTrusted(), self.eventTap.install() else { return }
            timer.invalidate()
            self.permissionItem.isHidden = true
            self.updateStatusIcon()
            HUD.show("\(AppInfo.name) فعال شد — F10 برای فعال/غیرفعال", duration: 2.5)
        }
    }

    @objc private func openAccessibilitySettings() {
        let url = URL(string: "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility")!
        NSWorkspace.shared.open(url)
    }

    @objc private func inputSourceChanged() {
        LayoutManager.refresh()
    }

    @objc private func activeAppChanged() {
        service.reset()
    }

    // MARK: - Menu

    private func buildMenu() {
        statusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        updateStatusIcon()

        let menu = NSMenu()
        menu.delegate = self

        permissionItem.title = "⚠️ دسترسی Accessibility لازم است…"
        permissionItem.action = #selector(openAccessibilitySettings)
        permissionItem.target = self
        permissionItem.isHidden = true
        menu.addItem(permissionItem)

        enabledItem.action = #selector(toggleEnabled)
        enabledItem.target = self
        menu.addItem(enabledItem)

        logItem.title = "ذخیره log"
        logItem.action = #selector(toggleLogging)
        logItem.target = self
        menu.addItem(logItem)

        menu.addItem(item("لغات استثنا…", #selector(showExceptions)))
        menu.addItem(item("لغات شخصی…", #selector(showUserWords)))

        startupItem.title = "اجرا هنگام ورود به سیستم"
        startupItem.action = #selector(toggleStartup)
        startupItem.target = self
        menu.addItem(startupItem)

        menu.addItem(item("نمایش log", #selector(showLog)))
        menu.addItem(item("درباره برنامه", #selector(showAbout)))
        menu.addItem(.separator())
        menu.addItem(item("خروج", #selector(quit), key: "q"))

        statusItem.menu = menu
        refreshMenuState()
    }

    private func item(_ title: String, _ action: Selector, key: String = "") -> NSMenuItem {
        let item = NSMenuItem(title: title, action: action, keyEquivalent: key)
        item.target = self
        return item
    }

    func menuWillOpen(_ menu: NSMenu) {
        refreshMenuState()
    }

    private func refreshMenuState() {
        enabledItem.title = service.enabled ? "فعال (F10)" : "غیرفعال (F10)"
        enabledItem.state = service.enabled ? .on : .off
        logItem.state = AppLog.isEnabled ? .on : .off
        startupItem.state = LoginItem.isEnabled ? .on : .off
    }

    private func updateStatusIcon() {
        guard let button = statusItem?.button else { return }
        let active = service.enabled && eventTap.isInstalled
        let image = NSImage(systemSymbolName: "keyboard", accessibilityDescription: AppInfo.name)
        image?.isTemplate = true
        button.image = image
        button.appearsDisabled = !active
        button.toolTip = active ? "\(AppInfo.name) — فعال" : "\(AppInfo.name) — غیرفعال"
    }

    @objc private func toggleEnabled() {
        setEnabled(!service.enabled, notify: false)
    }

    private func setEnabled(_ enabled: Bool, notify: Bool) {
        service.enabled = enabled
        service.reset()
        refreshMenuState()
        updateStatusIcon()
        AppLog.write(enabled ? "Enabled" : "Disabled")

        // با میان‌بر، تنها بازخوردِ کاربر همین اعلان است
        if notify {
            HUD.show(enabled ? "\(AppInfo.name): فعال شد" : "\(AppInfo.name): غیرفعال شد")
        }
    }

    @objc private func toggleLogging() {
        AppLog.isEnabled.toggle()
        if AppLog.isEnabled {
            AppLog.writeHeader()
            AppLog.write("Log saving enabled by user. Key map: \(Mapper.sourceID)")
            DetectorSelfTest.run(layoutIDs: [Mapper.sourceID])
        }
        refreshMenuState()
    }

    @objc private func toggleStartup() {
        let enable = !LoginItem.isEnabled
        do {
            try LoginItem.set(enable)
            AppLog.write(enable ? "Login item registered." : "Login item removed.")
        } catch {
            AppLog.write("Failed to change login item: \(error)")
            Alerts.warn("تغییر تنظیمات اجرای خودکار با خطا مواجه شد.\n\(error.localizedDescription)", title: "خطا")
        }
        refreshMenuState()
    }

    @objc private func showExceptions() {
        if exceptionsWindow == nil {
            exceptionsWindow = .exceptions()
        }
        exceptionsWindow?.present()
    }

    @objc private func showUserWords() {
        if userWordsWindow == nil {
            userWordsWindow = .userWords()
        }
        userWordsWindow?.present()
    }

    @objc private func showLog() {
        AppLog.open()
    }

    @objc private func showAbout() {
        Alerts.info("\(AppInfo.name)\nنسخه: \(AppInfo.version)\nسازنده: \(AppInfo.developer)",
                    title: "درباره \(AppInfo.name)")
    }

    @objc private func quit() {
        NSApp.terminate(nil)
    }
}
