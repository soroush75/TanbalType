import AppKit

/// پنجرهٔ مدیریت یک فهرست لغت (لغات استثنا / لغات شخصی).
final class WordListWindow: NSWindowController, NSTableViewDataSource, NSTableViewDelegate, NSTextFieldDelegate {
    struct Config {
        let title: String
        let hint: String
        /// ستون دوم «شکلِ اشتباه» و پیش‌نمایش زنده (برای لغات شخصی)
        let showsSwapped: Bool
        let load: () -> [String]
        /// خروجی false یعنی افزودن انجام نشد (مثلاً ورودی نامعتبر بود)
        let add: (String) -> Bool
        let remove: (String) -> Void
    }

    private let config: Config
    private var words: [String] = []
    private let input = NSTextField()
    private let preview = NSTextField(labelWithString: "")
    private let table = NSTableView()

    init(_ config: Config) {
        self.config = config
        let window = NSWindow(contentRect: NSRect(x: 0, y: 0, width: 440, height: 460),
                              styleMask: [.titled, .closable, .resizable], backing: .buffered, defer: false)
        window.title = config.title
        window.minSize = NSSize(width: 360, height: 360)
        window.isReleasedWhenClosed = false
        super.init(window: window)
        buildUI()
        reload()
        window.center()
    }

    required init?(coder: NSCoder) { fatalError("not supported") }

    func present() {
        reload()
        NSApp.activate(ignoringOtherApps: true)
        window?.makeKeyAndOrderFront(nil)
        window?.makeFirstResponder(input)
    }

    private func buildUI() {
        guard let content = window?.contentView else { return }
        content.userInterfaceLayoutDirection = .rightToLeft

        let hint = NSTextField(wrappingLabelWithString: config.hint)
        hint.alignment = .right
        hint.baseWritingDirection = .rightToLeft

        input.placeholderString = "لغت را وارد کنید و Enter بزنید"
        input.alignment = .natural
        input.target = self
        input.action = #selector(addClicked)
        input.delegate = self

        preview.textColor = .secondaryLabelColor
        preview.alignment = .right
        preview.isHidden = !config.showsSwapped

        let addButton = NSButton(title: "افزودن", target: self, action: #selector(addClicked))
        addButton.keyEquivalent = ""

        let inputRow = NSStackView(views: [input, addButton])
        inputRow.orientation = .horizontal
        inputRow.spacing = 8

        let wordColumn = NSTableColumn(identifier: .init("word"))
        wordColumn.title = config.showsSwapped ? "لغتِ درست" : "لغت"
        wordColumn.width = 170
        table.addTableColumn(wordColumn)
        if config.showsSwapped {
            let swapColumn = NSTableColumn(identifier: .init("swapped"))
            swapColumn.title = "شکلِ اشتباه (چیدمانِ دیگر)"
            swapColumn.width = 200
            table.addTableColumn(swapColumn)
        } else {
            table.headerView = nil
        }
        table.usesAlternatingRowBackgroundColors = true
        table.allowsMultipleSelection = false
        table.dataSource = self
        table.delegate = self
        table.columnAutoresizingStyle = .uniformColumnAutoresizingStyle

        let scroll = NSScrollView()
        scroll.documentView = table
        scroll.hasVerticalScroller = true
        scroll.borderType = .bezelBorder

        let removeButton = NSButton(title: "حذف موردِ انتخاب‌شده", target: self, action: #selector(removeClicked))
        let closeButton = NSButton(title: "بستن", target: self, action: #selector(closeClicked))
        closeButton.keyEquivalent = "\u{1b}"
        let buttons = NSStackView(views: [removeButton, closeButton])
        buttons.orientation = .horizontal
        buttons.spacing = 8

        let stack = NSStackView(views: [hint, inputRow, preview, scroll, buttons])
        stack.orientation = .vertical
        stack.alignment = .leading
        stack.spacing = 10
        stack.edgeInsets = NSEdgeInsets(top: 16, left: 16, bottom: 16, right: 16)
        stack.translatesAutoresizingMaskIntoConstraints = false
        content.addSubview(stack)

        NSLayoutConstraint.activate([
            stack.topAnchor.constraint(equalTo: content.topAnchor),
            stack.bottomAnchor.constraint(equalTo: content.bottomAnchor),
            stack.leadingAnchor.constraint(equalTo: content.leadingAnchor),
            stack.trailingAnchor.constraint(equalTo: content.trailingAnchor),
            hint.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -32),
            inputRow.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -32),
            preview.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -32),
            scroll.widthAnchor.constraint(equalTo: stack.widthAnchor, constant: -32),
        ])
        scroll.setContentHuggingPriority(.defaultLow, for: .vertical)
        scroll.setContentCompressionResistancePriority(.defaultLow, for: .vertical)
    }

    private func reload() {
        words = config.load()
        table.reloadData()
    }

    // MARK: - Actions

    @objc private func addClicked() {
        let word = input.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        if word.isEmpty { return }
        if config.add(word) {
            reload()
        }
        input.stringValue = ""
        updatePreview()
        window?.makeFirstResponder(input)
    }

    @objc private func removeClicked() {
        let row = table.selectedRow
        guard row >= 0, row < words.count else { return }
        config.remove(words[row])
        reload()
    }

    @objc private func closeClicked() {
        window?.close()
    }

    func controlTextDidChange(_ obj: Notification) {
        updatePreview()
    }

    // پیش‌نمایشِ زنده: کاربر پیش از ثبت می‌بیند چه چیزی قرار است اصلاح شود
    private func updatePreview() {
        guard config.showsSwapped else { return }
        let word = input.stringValue.trimmingCharacters(in: .whitespacesAndNewlines)
        preview.stringValue = word.isEmpty ? "" : "شکلِ اشتباه: \(Mapper.swapLayout(word))"
    }

    // MARK: - Table

    func numberOfRows(in tableView: NSTableView) -> Int {
        words.count
    }

    func tableView(_ tableView: NSTableView, viewFor tableColumn: NSTableColumn?, row: Int) -> NSView? {
        guard let column = tableColumn else { return nil }
        let id = column.identifier
        let cell = tableView.makeView(withIdentifier: id, owner: self) as? NSTextField
            ?? {
                let field = NSTextField(labelWithString: "")
                field.identifier = id
                field.alignment = .right
                field.lineBreakMode = .byTruncatingTail
                return field
            }()
        let word = words[row]
        cell.stringValue = id.rawValue == "swapped" ? Mapper.swapLayout(word) : word
        return cell
    }
}

extension WordListWindow {
    static func exceptions() -> WordListWindow {
        WordListWindow(Config(
            title: "لغات استثنا",
            hint: "لغتی که اینجا اضافه می‌کنید (فارسی یا انگلیسی) دیگر اصلاح خودکار نمی‌شود.",
            showsSwapped: false,
            load: UserExceptions.all,
            add: { word in
                guard UserExceptions.add(word) else { return false }
                AppLog.write("UserExceptions: added '\(word)'")
                return true
            },
            remove: { word in
                if UserExceptions.remove(word) {
                    AppLog.write("UserExceptions: removed '\(word)'")
                }
            }))
    }

    static func userWords() -> WordListWindow {
        WordListWindow(Config(
            title: "لغات شخصی",
            hint: "لغتِ فارسی یا انگلیسیِ خودتان را اضافه کنید. از این پس هر وقت همان کلمه "
                + "با چیدمانِ اشتباه تایپ شود، برنامه آن را دقیقاً به همین شکل اصلاح می‌کند.",
            showsSwapped: true,
            load: UserWords.all,
            add: addUserWord,
            remove: { word in
                if UserWords.remove(word) {
                    AppLog.write("UserWords: removed '\(word)'")
                }
            }))
    }

    private static func addUserWord(_ word: String) -> Bool {
        // بافرِ برنامه با هر فاصله/Enter خالی می‌شود، پس فقط تک‌کلمه قابل اصلاح است
        if word.contains(where: \.isWhitespace) {
            Alerts.warn("هر ورودی باید یک کلمهٔ تنها باشد (بدون فاصله).", title: "لغات شخصی")
            return false
        }

        // اصلاح روی کلمهٔ تک‌حرفی انجام نمی‌شود، پس ثبتش هم بی‌اثر است
        if word.count < 2 {
            Alerts.warn("کلمه باید حداقل دو حرف داشته باشد.", title: "لغات شخصی")
            return false
        }

        let swapped = Mapper.swapLayout(word)
        if swapped == word {
            Alerts.warn("این کلمه روی چیدمانِ دیگر شکلِ متفاوتی ندارد، پس اصلاحی برایش معنا ندارد.",
                        title: "لغات شخصی")
            return false
        }

        // یک کلمه نمی‌تواند هم «استثنا» باشد و هم «لغت شخصی»؛ استثناها در تشخیص مقدم‌اند
        let conflict = UserExceptions.isException(word) ? word
            : UserExceptions.isException(swapped) ? swapped : nil
        if let conflict {
            let remove = Alerts.confirm(
                "«\(conflict)» در فهرست «لغات استثنا» هم هست و آنجا از اصلاح کنار گذاشته شده.\n"
                    + "تا وقتی آنجا باشد این اصلاح انجام نمی‌شود. از فهرست استثناها برداشته شود؟",
                title: "تداخل با لغات استثنا")
            if !remove { return false }
            UserExceptions.remove(conflict)
            AppLog.write("UserWords: removed conflicting exception '\(conflict)'")
        }

        guard UserWords.add(word) else { return false }
        AppLog.write("UserWords: added '\(word)'")
        return true
    }
}
