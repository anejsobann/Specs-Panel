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
    private readonly DataGridView processes = new();
    private readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    private readonly ComboBox processSort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label processStatus = new() { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProcessReader processReader = new();
    private List<ProcessUsage> processUsage = [];
    private bool samplingProcesses;
    private readonly Button cpuHelp = new() { AutoSize = true, FlatStyle = FlatStyle.Flat, Visible = false };
    private readonly Label[] values = new Label[3];
    private readonly Label[] devices = new Label[3];
    private readonly HardwareReader reader = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1500 };
    private Rectangle windowBounds;
    private bool full;
    private bool reading;
    private bool closing;

    public Dashboard(string[] args)
    {
        Text = "Specs Panel";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        ClientSize = new Size(1000, 620);
        MinimumSize = new Size(560, 370);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Background;
        ForeColor = Ink;
        Font = new Font("Segoe UI", 11);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(layout);

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        displays.Width = 220;
        header.Controls.Add(new Label { Text = "Specs Panel", AutoSize = true, Font = new Font(Font, FontStyle.Bold), Margin = new Padding(0, 6, 16, 6) });
        header.Controls.Add(displays);
        fullscreen.BackColor = Surface;
        header.Controls.Add(fullscreen);
        layout.Controls.Add(header, 0, 0);

        var specsPage = new TabPage("System Specs") { BackColor = Background, Padding = new Padding(4) };
        var processPage = new TabPage("Processes") { BackColor = Background, Padding = new Padding(4) };
        tabs.TabPages.AddRange([specsPage, processPage]);
        layout.Controls.Add(tabs, 0, 1);
        var specs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        specs.RowStyles.Add(new RowStyle(SizeType.Absolute, 138));
        specs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        specs.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        specs.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        specsPage.Controls.Add(specs);

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
        specs.Controls.Add(cards, 0, 0);

        ConfigureGrid(details, ("Skupina", 17), ("Naprava", 33), ("Senzor", 32), ("Meritev", 18));
        specs.Controls.Add(details, 0, 1);
        cpuHelp.BackColor = Surface;
        cpuHelp.Click += (_, _) => OpenCpuHelp();
        specs.Controls.Add(cpuHelp, 0, 2);
        specs.Controls.Add(status, 0, 3);
        ConfigureProcesses(processPage);

        displays.DropDown += (_, _) => RefreshDisplays();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        timer.Tick += async (_, _) => await RefreshDashboard();
        Shown += async (_, _) =>
        {
            RefreshDisplays();
            RestoreView(args);
            status.Text = "Iskanje senzorjev …";
            timer.Start();
            await RefreshDashboard();
        };
        FormClosed += (_, _) =>
        {
            closing = true;
            timer.Dispose();
            if (!reading)
                reader.Dispose();
        };
    }

    private static void ConfigureGrid(DataGridView grid, params (string Name, int Width)[] columns)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.GridColor = Background;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.BackColor = Background;
        grid.DefaultCellStyle.ForeColor = Ink;
        grid.RowTemplate.Height = 28;
        foreach (var (name, width) in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, FillWeight = width, SortMode = DataGridViewColumnSortMode.NotSortable });
    }

    private void ConfigureProcesses(TabPage page)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        page.Controls.Add(panel);

        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        filter.Controls.Add(new Label { Text = "Razvrsti po", AutoSize = true, Margin = new Padding(4, 6, 8, 4) });
        filter.Controls.Add(processSort);
        processSort.Items.AddRange(["CPU", "RAM", "I/O"]);
        processSort.SelectedIndex = 0;
        processSort.SelectedIndexChanged += (_, _) => RenderProcesses();
        panel.Controls.Add(filter, 0, 0);
        ConfigureGrid(processes, ("Proces", 44), ("PID", 12), ("CPU (%)", 14), ("RAM (MiB)", 15), ("I/O (MiB/s)", 15));
        processes.ColumnHeaderMouseClick += (_, eventArgs) =>
        {
            if (eventArgs.ColumnIndex is >= 2 and <= 4)
                processSort.SelectedIndex = eventArgs.ColumnIndex - 2;
        };
        panel.Controls.Add(processes, 0, 1);
        processStatus.Text = "Zbiranje prvih meritev …";
        panel.Controls.Add(processStatus, 0, 2);
    }

    private async Task RefreshDashboard()
    {
        if (LocalUpdate.TryRestart(ViewArguments()))
        {
            Close();
            return;
        }
        await Task.WhenAll(RefreshMetrics(), RefreshProcesses());
    }

    private async Task RefreshProcesses()
    {
        if (samplingProcesses)
            return;
        samplingProcesses = true;
        try
        {
            var snapshot = await Task.Run(processReader.Read);
            if (closing)
                return;
            processUsage = snapshot;
            RenderProcesses();
            processStatus.Text = $"{snapshot.Count} procesov  •  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (!closing)
                processStatus.Text = "Windows trenutno ne more prebrati procesov.";
        }
        finally
        {
            samplingProcesses = false;
        }
    }

    private void RenderProcesses()
    {
        var sort = processSort.SelectedIndex;
        var ordered = processUsage.OrderByDescending(row => sort switch
        {
            1 => row.MemoryMiB,
            2 => row.IoMiBPerSecond,
            _ => row.Cpu
        }).ThenBy(row => row.Id).ToArray();
        SetRowCount(processes, ordered.Length);
        for (var index = 0; index < ordered.Length; index++)
        {
            var row = ordered[index];
            processes.Rows[index].SetValues(row.Name, row.Id, Number(row.Cpu), Number(row.MemoryMiB), Number(row.IoMiBPerSecond));
        }
    }

    private static void SetRowCount(DataGridView grid, int count)
    {
        while (grid.Rows.Count > count)
            grid.Rows.RemoveAt(grid.Rows.Count - 1);
        if (grid.Rows.Count < count)
            grid.Rows.Add(count - grid.Rows.Count);
    }

    private static string Number(double? value) => value?.ToString("0.0") ?? "—";

    private string[] ViewArguments() =>
        [tabs.SelectedIndex.ToString(), Screen.FromControl(this).DeviceName, full ? "fullscreen" : "window"];

    private void RestoreView(string[] args)
    {
        if (args.Length != 3)
            return;
        tabs.SelectedIndex = args[0] == "1" ? 1 : 0;
        var display = displays.Items.OfType<DisplayChoice>().FirstOrDefault(choice => choice.Screen.DeviceName == args[1]);
        if (display is null)
            return;
        displays.SelectedItem = display;
        if (args[2] == "fullscreen")
            ToggleFullscreen();
        else if (Screen.FromControl(this).DeviceName != display.Screen.DeviceName)
            Location = display.Screen.WorkingArea.Location;
    }

    private void OpenCpuHelp()
    {
        try
        {
            var start = HardwareReader.PawnIoInstalled
                ? new System.Diagnostics.ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }
                : new System.Diagnostics.ProcessStartInfo("https://github.com/namazso/PawnIO.Setup/releases") { UseShellExecute = true };
            if (HardwareReader.PawnIoInstalled)
                foreach (var argument in ViewArguments())
                    start.ArgumentList.Add(argument);
            using var started = System.Diagnostics.Process.Start(start);
            if (HardwareReader.PawnIoInstalled && started is not null)
                Close();
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            status.Text = "Skrbniški zagon je bil preklican.";
        }
        catch (System.ComponentModel.Win32Exception)
        {
            status.Text = "Pomoči za temperaturo CPU ni bilo mogoče odpreti.";
        }
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
        public override string ToString() => Screen.DeviceName switch
        {
            @"\\.\DISPLAY1" => "MAIN MONITOR",
            @"\\.\DISPLAY2" => "ALT MONITOR",
            @"\\.\DISPLAY3" => "TABLET",
            _ => $"{Screen.DeviceName.Replace(@"\\.\DISPLAY", "Zaslon ")} • {Screen.Bounds.Width}×{Screen.Bounds.Height}"
        };
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
            var cpuAvailable = UpdateCard(0, metrics, "CPU", "°C", "CPU Package", "Core (Tctl/Tdie)", "Core (Tdie)", "Core (Tctl)", "Core Average", "Core Max");
            UpdateCard(1, metrics, "GPU", "°C", "GPU Core");
            UpdateCard(2, metrics, "RAM", "%", "Memory");
            SetRowCount(details, metrics.Count);
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                details.Rows[index].SetValues(metric.Group, metric.Device, metric.Name, Format(metric));
            }
            cpuHelp.Visible = !cpuAvailable && (!HardwareReader.PawnIoInstalled || !HardwareReader.IsAdministrator);
            cpuHelp.Text = HardwareReader.PawnIoInstalled ? "Zaženi kot skrbnik za temperaturo CPU" : "Odpri uradni gonilnik PawnIO za temperaturo CPU";
            status.Text = !cpuAvailable && !cpuHelp.Visible
                ? "Temperaturni senzor CPU ni vrnil podatka."
                : metrics.Any(metric => metric.Value.HasValue)
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

    private bool UpdateCard(int index, List<Metric> metrics, string group, string unit, params string[] preferredNames)
    {
        var options = metrics.Where(metric => metric.Group == group).ToArray();
        var available = options.Where(metric => metric.Unit == unit && metric.Value.HasValue &&
            (group != "RAM" || metric.Device != "Virtual Memory")).ToArray();
        var selected = preferredNames.Select(name => available.FirstOrDefault(metric => metric.Name == name))
            .FirstOrDefault(metric => metric is not null)
            ?? available.MaxBy(metric => metric.Value);
        values[index].Text = selected is null ? "—" : Format(selected);
        devices[index].Text = selected?.Device ?? options.FirstOrDefault()?.Device ?? "Ni podatka";
        return selected is not null;
    }

    private static string Format(Metric metric) => metric.Value is float value ? $"{value:0.#} {metric.Unit}" : "—";
}
