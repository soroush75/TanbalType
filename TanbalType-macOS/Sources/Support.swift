import AppKit
import ServiceManagement

enum AppInfo {
    static let name = "TanbalType"
    static let developer = "سروش سرمست"

    // نسخه از Info.plist خوانده می‌شود (build.sh آن را از تگ Version در TanbalType.csproj می‌گیرد)
    static var version: String {
        Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "?"
    }
}

enum Alerts {
    static func info(_ message: String, title: String = AppInfo.name) {
        show(message, title: title, style: .informational)
    }

    static func warn(_ message: String, title: String = AppInfo.name) {
        show(message, title: title, style: .warning)
    }

    static func confirm(_ message: String, title: String, yes: String = "بله", no: String = "خیر") -> Bool {
        NSApp.activate(ignoringOtherApps: true)
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = .warning
        alert.addButton(withTitle: yes)
        alert.addButton(withTitle: no)
        return alert.runModal() == .alertFirstButtonReturn
    }

    private static func show(_ message: String, title: String, style: NSAlert.Style) {
        NSApp.activate(ignoringOtherApps: true)
        let alert = NSAlert()
        alert.messageText = title
        alert.informativeText = message
        alert.alertStyle = style
        alert.addButton(withTitle: "باشه")
        alert.runModal()
    }
}

/// اجرای خودکار هنگام ورود به حساب کاربری (معادل پوشهٔ Startup ویندوز).
enum LoginItem {
    private static var legacyAgentURL: URL {
        FileManager.default.homeDirectoryForCurrentUser
            .appendingPathComponent("Library/LaunchAgents/\(Bundle.main.bundleIdentifier ?? "TanbalType").plist")
    }

    static var isEnabled: Bool {
        if #available(macOS 13.0, *) {
            return SMAppService.mainApp.status == .enabled
        }
        return FileManager.default.fileExists(atPath: legacyAgentURL.path)
    }

    static func set(_ enabled: Bool) throws {
        if #available(macOS 13.0, *) {
            if enabled {
                try SMAppService.mainApp.register()
            } else {
                try SMAppService.mainApp.unregister()
            }
            return
        }

        // macOS 11/12: یک LaunchAgent ساده در پوشهٔ کاربر
        if enabled {
            let plist: [String: Any] = [
                "Label": Bundle.main.bundleIdentifier ?? "TanbalType",
                "ProgramArguments": ["/usr/bin/open", "-a", Bundle.main.bundlePath],
                "RunAtLoad": true,
            ]
            let data = try PropertyListSerialization.data(fromPropertyList: plist, format: .xml, options: 0)
            try FileManager.default.createDirectory(
                at: legacyAgentURL.deletingLastPathComponent(), withIntermediateDirectories: true)
            try data.write(to: legacyAgentURL)
        } else if FileManager.default.fileExists(atPath: legacyAgentURL.path) {
            try FileManager.default.removeItem(at: legacyAgentURL)
        }
    }
}

/// اعلان کوتاه وسط-پایین صفحه (به‌جای balloon tip ویندوز)، بدون گرفتن فوکوس از برنامهٔ فعال.
final class HUD {
    private static var panel: NSPanel?
    private static var hideWork: DispatchWorkItem?

    static func show(_ text: String, duration: TimeInterval = 1.4) {
        let panel = self.panel ?? makePanel()
        self.panel = panel

        let label = panel.contentView?.viewWithTag(1) as? NSTextField
        label?.stringValue = text
        label?.sizeToFit()

        let width = max(180, (label?.frame.width ?? 100) + 48)
        let height: CGFloat = 54
        let screen = NSScreen.main?.visibleFrame ?? .zero
        panel.setFrame(NSRect(x: screen.midX - width / 2, y: screen.minY + screen.height * 0.18,
                              width: width, height: height), display: true)
        if let label {
            label.frame.origin = NSPoint(x: (width - label.frame.width) / 2, y: (height - label.frame.height) / 2)
        }

        panel.alphaValue = 1
        panel.orderFrontRegardless()

        hideWork?.cancel()
        let work = DispatchWorkItem {
            NSAnimationContext.runAnimationGroup({ ctx in
                ctx.duration = 0.3
                panel.animator().alphaValue = 0
            }, completionHandler: { panel.orderOut(nil) })
        }
        hideWork = work
        DispatchQueue.main.asyncAfter(deadline: .now() + duration, execute: work)
    }

    private static func makePanel() -> NSPanel {
        let panel = NSPanel(contentRect: NSRect(x: 0, y: 0, width: 200, height: 54),
                            styleMask: [.borderless, .nonactivatingPanel], backing: .buffered, defer: false)
        panel.level = .statusBar
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.ignoresMouseEvents = true
        panel.collectionBehavior = [.canJoinAllSpaces, .transient]

        let effect = NSVisualEffectView()
        effect.material = .hudWindow
        effect.state = .active
        effect.blendingMode = .behindWindow
        effect.wantsLayer = true
        effect.layer?.cornerRadius = 14
        effect.layer?.masksToBounds = true
        panel.contentView = effect

        let label = NSTextField(labelWithString: "")
        label.font = .systemFont(ofSize: 16, weight: .semibold)
        label.alignment = .center
        label.tag = 1
        effect.addSubview(label)
        return panel
    }
}
