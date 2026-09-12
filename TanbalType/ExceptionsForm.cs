namespace TanbalType;

/// <summary>
/// پنجرهٔ مدیریت لغات استثنا: کاربر می‌تواند لغاتی اضافه کند که برنامه هرگز
/// روی آن‌ها اصلاح خودکار (فارسی/انگلیسی) انجام ندهد.
/// </summary>
internal sealed class ExceptionsForm : Form
{
    private readonly ListBox _list;
    private readonly TextBox _input;

    public ExceptionsForm()
    {
        Text = "لغات استثنا";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        ClientSize = new Size(360, 380);
        Font = new Font("Segoe UI", 9.5f);

        var hint = new Label
        {
            Text = "لغتی که تایپ می‌کنید (فارسی یا انگلیسی) دیگر اصلاح خودکار نمی‌شود.",
            AutoSize = false,
            Dock = DockStyle.Top,
            Height = 40,
            TextAlign = ContentAlignment.MiddleRight,
        };

        _input = new TextBox
        {
            Dock = DockStyle.Top,
            Margin = new Padding(8),
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddCurrentInput();
            }
        };

        var addButton = new Button
        {
            Text = "افزودن",
            Dock = DockStyle.Top,
            Height = 32,
        };
        addButton.Click += (_, _) => AddCurrentInput();

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
        };

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
        Controls.Add(closeButton);
        Controls.Add(removeButton);
        Controls.Add(addButton);
        Controls.Add(_input);
        Controls.Add(hint);

        Load += (_, _) => Reload();
    }

    private void Reload()
    {
        _list.Items.Clear();
        foreach (var word in UserExceptions.GetAll())
            _list.Items.Add(word);
    }

    private void AddCurrentInput()
    {
        var word = _input.Text.Trim();
        if (word.Length == 0)
            return;

        if (UserExceptions.Add(word))
        {
            AppLog.Write($"UserExceptions: added '{word}'");
            Reload();
        }

        _input.Clear();
        _input.Focus();
    }

    private void RemoveSelected()
    {
        if (_list.SelectedItem is not string word)
            return;

        if (UserExceptions.Remove(word))
        {
            AppLog.Write($"UserExceptions: removed '{word}'");
            Reload();
        }
    }
}
