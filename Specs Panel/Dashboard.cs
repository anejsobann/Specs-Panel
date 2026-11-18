namespace SpecsPanel;

internal sealed class Dashboard : Form
{
    private static readonly Color Background = Color.FromArgb(15, 20, 29);
    private static readonly Color Surface = Color.FromArgb(25, 34, 47);
    private static readonly Color Ink = Color.FromArgb(235, 242, 250);
    private static readonly Color Muted = Color.FromArgb(166, 183, 202);
    private static readonly Color Accent = Color.FromArgb(104, 231, 199);
    private readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ComboBox displays = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 190 };
    private readonly Button fullscreen = new() { Text = "Celozaslonsko", AutoSize = true, FlatStyle = FlatStyle.Flat };
    private readonly DataGridView details = new();
    private readonly Label[] values = new Label[3];
    private readonly Label[] devices = new Label[3];
    private readonly HardwareReader reader = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1500 };
    private Rectangle windowBounds;
    private bool full;
    private bool reading;
    private bool closing;

    public Dashboard()
    {
        Text = "Specs Panel";
        ClientSize = new Size(1000, 620);
        MinimumSize = new Size(560, 370);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Background;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 11);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        Controls.Add(layout);

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        displays.Width = 220;
        header.Controls.Add(new Label { Text = "Specs Panel", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 16, 6) });
        header.Controls.Add(displays);
        fullscreen.BackColor = Surface;
        header.Controls.Add(fullscreen);
        layout.Controls.Add(header, 0, 0);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1 };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        string[] titles = ["CPU", "GPU", "RAM"];
        for (var index = 0; index < titles.Length; index++)
        {
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / titles.Length));
            var card = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = Surface, Padding = new Padding(10), RowCount = 3, ColumnCount = 1 };
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            card.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            card.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
            card.Controls.Add(new Label { Text = titles[index], Dock = DockStyle.Fill, ForeColor = Muted }, 0, 0);
            values[index] = new Label { Text = "—", Dock = DockStyle.Fill, ForeColor = Accent, Font = new Font("Segoe UI", 22, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            devices[index] = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, ForeColor = Muted };
            card.Controls.Add(values[index], 0, 1);
            card.Controls.Add(devices[index], 0, 2);
            cards.Controls.Add(card, index, 0);
        }
        layout.Controls.Add(cards, 0, 1);

        details.Dock = DockStyle.Fill;
        details.ReadOnly = true;
        details.AllowUserToAddRows = false;
        details.AllowUserToDeleteRows = false;
        details.RowHeadersVisible = false;
        details.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        details.BackgroundColor = Surface;
        details.BorderStyle = BorderStyle.None;
        details.GridColor = Background;
        details.EnableHeadersVisualStyles = false;
        details.ColumnHeadersDefaultCellStyle.BackColor = Surface;
        details.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        details.DefaultCellStyle.BackColor = Background;
        details.DefaultCellStyle.ForeColor = Ink;
        details.RowTemplate.Height = 28;
        foreach (var (name, width) in new[] { ("Skupina", 17), ("Naprava", 33), ("Senzor", 32), ("Meritev", 18) })
        {
            details.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = name,
                FillWeight = width,
                SortMode = DataGridViewColumnSortMode.NotSortable
            });
        }
        layout.Controls.Add(details, 0, 2);
        layout.Controls.Add(status, 0, 3);

        displays.DropDown += (_, _) => RefreshDisplays();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        timer.Tick += async (_, _) => await RefreshMetrics();
        Shown += async (_, _) =>
        {
            RefreshDisplays();
            status.Text = "Iskanje senzorjev …";
            timer.Start();
            await RefreshMetrics();
        };
        FormClosed += (_, _) =>
        {
            closing = true;
            timer.Dispose();
            if (!reading)
                reader.Dispose();
        };
    }

    private void RefreshDisplays()
    {
        var previous = (displays.SelectedItem as DisplayChoice)?.Screen.DeviceName ?? Screen.FromControl(this).DeviceName;
        var screens = Screen.AllScreens;
        displays.Items.Clear();
        foreach (var screen in screens)
            displays.Items.Add(new DisplayChoice(screen));
        var index = Array.FindIndex(screens, screen => screen.DeviceName == previous);
        if (screens.Length > 0)
            displays.SelectedIndex = Math.Max(0, index);
    }

    private sealed record DisplayChoice(Screen Screen)
    {
        public override string ToString() => $"{Screen.DeviceName.Replace(@"\\.\DISPLAY", "Zaslon ")} • {Screen.Bounds.Width}×{Screen.Bounds.Height}";
    }

    private void ToggleFullscreen()
    {
        if (full)
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(560, 370);
            Bounds = windowBounds;
        }
        else
        {
            var selectedName = (displays.SelectedItem as DisplayChoice)?.Screen.DeviceName;
            var screen = Screen.AllScreens.FirstOrDefault(candidate => candidate.DeviceName == selectedName)
                ?? Screen.FromControl(this);
            windowBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            WindowState = FormWindowState.Normal;
            MinimumSize = Size.Empty;
            FormBorderStyle = FormBorderStyle.None;
            Location = screen.Bounds.Location;
            Bounds = screen.Bounds;
        }
        full = !full;
        fullscreen.Text = full ? "Zapri celozaslonski prikaz" : "Celozaslonsko";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (keyData == Keys.Escape && full))
        {
            ToggleFullscreen();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private async Task RefreshMetrics()
    {
        if (reading)
            return;
        reading = true;
        try
        {
            var metrics = await Task.Run(reader.Read);
            if (closing)
                return;
            UpdateCard(0, metrics, "CPU", "°C", "CPU Package", "Core (Tctl/Tdie)", "Core Average", "Core Max");
            UpdateCard(1, metrics, "GPU", "°C", "GPU Core");
            UpdateCard(2, metrics, "RAM", "%", "Memory");
            while (details.Rows.Count > metrics.Count)
                details.Rows.RemoveAt(details.Rows.Count - 1);
            while (details.Rows.Count < metrics.Count)
                details.Rows.Add();
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                details.Rows[index].SetValues(metric.Group, metric.Device, metric.Name, Format(metric));
            }
            status.Text = metrics.Any(metric => metric.Value.HasValue)
                ? $"Zadnja meritev ob {DateTime.Now:HH:mm:ss}"
                : "Meritve niso na voljo.";
        }
        catch (Exception error)
        {
            if (!closing)
                status.Text = $"Napaka pri branju senzorjev ({error.GetType().Name}).";
        }
        finally
        {
            reading = false;
            if (closing)
                reader.Dispose();
        }
    }

    private void UpdateCard(int index, List<Metric> metrics, string group, string unit, params string[] preferredNames)
    {
        var options = metrics.Where(metric => metric.Group == group).ToArray();
        var available = options.Where(metric => metric.Unit == unit && metric.Value.HasValue &&
            (group != "RAM" || metric.Device != "Virtual Memory")).ToArray();
        var selected = preferredNames.Select(name => available.FirstOrDefault(metric => metric.Name == name))
            .FirstOrDefault(metric => metric is not null)
            ?? available.MaxBy(metric => metric.Value);
        values[index].Text = selected is null ? "—" : Format(selected);
        devices[index].Text = selected?.Device ?? options.FirstOrDefault()?.Device ?? "Ni podatka";
    }

    private static string Format(Metric metric) => metric.Value is float value ? $"{value:0.#} {metric.Unit}" : "—";
}
