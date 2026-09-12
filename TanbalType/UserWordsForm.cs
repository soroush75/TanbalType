namespace TanbalType;

/// <summary>
/// پنجرهٔ مدیریت لغات شخصی: کاربر لغتِ فارسی یا انگلیسیِ موردنظرش را ثبت می‌کند و برنامه
/// هر بار که همان کلمه با چیدمانِ اشتباه تایپ شود، آن را دقیقاً به همین شکل اصلاح می‌کند.
/// </summary>
internal sealed class UserWordsForm : Form
{
    private readonly ListView _list;
    private readonly TextBox _input;
    private readonly Label _preview;

    public UserWordsForm()
    {
        Text = "لغات شخصی";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        ClientSize = new Size(420, 420);
        Font = new Font("Segoe UI", 9.5f);

        var hint = new Label
        {
            Text = "لغتِ فارسی یا انگلیسیِ خودتان را اضافه کنید. از این پس هر وقت همان کلمه "
                 + "با چیدمانِ اشتباه تایپ شود، برنامه آن را دقیقاً به همین شکل اصلاح می‌کند.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 48,
            TextAlign = ContentAlignment.MiddleRight,
        };

        _input = new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(8),
        };
        _input.TextChanged += (_, _) => UpdatePreview();
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddCurrentInput();
            }
        };

        // پیش‌نمایشِ زنده: کاربر پیش از ثبت می‌بیند چه چیزی قرار است اصلاح شود
        _preview = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 26,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = SystemColors.GrayText,
        };

        var addButton = new Button
        {
            Text = "افزودن",
            Dock = DockStyle.Top,
            Height = 32,
        };
        addButton.Click += (_, _) => AddCurrentInput();

        _list = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            GridLines = true,
        };
        _list.Columns.Add("لغتِ درست", 165);
        _list.Columns.Add("شکلِ اشتباه (چیدمانِ دیگر)", 210);

        var removeButton = new Button
        {
            Text = "حذف موردِ انتخاب‌شده",
            Dock = DockStyle.Bottom,
            Height = 32,
        };
        removeButton.Click += (_, _) => RemoveSelected();

        var closeButton = new Button
        {
            Text = "بستن",
            Dock = DockStyle.Bottom,
            Height = 32,
        };
        closeButton.Click += (_, _) => Close();

        // ترتیب افزودن برای Dock مهم است (آخرین مورد نزدیک‌ترین به لبه)
        Controls.Add(_list);
        Controls.Add(removeButton);
        Controls.Add(closeButton);
        Controls.Add(addButton);
        Controls.Add(_preview);
        Controls.Add(_input);
        Controls.Add(hint);

        Load += (_, _) => Reload();
    }

    private void Reload()
    {
        _list.BeginUpdate();
        _list.Items.Clear();
        foreach (var word in UserWords.GetAll())
        {
            var item = new ListViewItem(word) { Tag = word };
            item.SubItems.Add(Mapper.SwapLayout(word));
            _list.Items.Add(item);
        }
        _list.EndUpdate();
    }

    private void UpdatePreview()
    {
        var word = _input.Text.Trim();
        _preview.Text = word.Length == 0
            ? string.Empty
            : $"شکلِ اشتباه: {Mapper.SwapLayout(word)}";
    }

    private void AddCurrentInput()
    {
        var word = _input.Text.Trim();
        if (word.Length == 0)
            return;

        // بافرِ برنامه با هر فاصله/Enter خالی می‌شود، پس فقط تک‌کلمه قابل اصلاح است
        if (word.Any(char.IsWhiteSpace))
        {
            Warn("هر ورودی باید یک کلمهٔ تنها باشد (بدون فاصله).");
            return;
        }

        // اصلاح روی کلمهٔ تک‌حرفی انجام نمی‌شود، پس ثبتش هم بی‌اثر است
        if (word.Length < 2)
        {
            Warn("کلمه باید حداقل دو حرف داشته باشد.");
            return;
        }

        if (Mapper.SwapLayout(word) == word)
        {
            Warn("این کلمه روی چیدمانِ دیگر شکلِ متفاوتی ندارد، پس اصلاحی برایش معنا ندارد.");
            return;
        }

        if (!ResolveExceptionConflict(word))
            return;

        if (UserWords.Add(word))
        {
            AppLog.Write($"UserWords: added '{word}'");
            Reload();
        }

        _input.Clear();
        _input.Focus();
    }

    /// <summary>
    /// یک کلمه نمی‌تواند هم «استثنا» باشد (هرگز اصلاح نشود) و هم «لغت شخصی» (همیشه اصلاح شود).
    /// استثناها در تشخیص مقدم‌اند، پس اگر تداخلی هست باید اول برداشته شود.
    /// </summary>
    private bool ResolveExceptionConflict(string word)
    {
        var swapped = Mapper.SwapLayout(word);
        var conflict =
            UserExceptions.IsException(word) ? word :
            UserExceptions.IsException(swapped) ? swapped :
            null;

        if (conflict is null)
            return true;

        var answer = MessageBox.Show(
            this,
            $"«{conflict}» در فهرست «لغات استثنا» هم هست و آنجا از اصلاح کنار گذاشته شده.\n" +
            "تا وقتی آنجا باشد این اصلاح انجام نمی‌شود. از فهرست استثناها برداشته شود؟",
            "تداخل با لغات استثنا",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return false;

        UserExceptions.Remove(conflict);
        AppLog.Write($"UserWords: removed conflicting exception '{conflict}'");
        return true;
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not string word)
            return;

        if (UserWords.Remove(word))
        {
            AppLog.Write($"UserWords: removed '{word}'");
            Reload();
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
