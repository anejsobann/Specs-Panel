using System.Diagnostics;

namespace SpecsPanel;

internal sealed class Dashboard : Form
{
    private static readonly Color Background = Color.FromArgb(9, 15, 25);
    private static readonly Color Surface = Color.FromArgb(20, 30, 45);
    private static readonly Color Ink = Color.FromArgb(237, 245, 255);
    private static readonly Color Muted = Color.FromArgb(163, 180, 202);
    private static readonly Color Accent = Color.FromArgb(99, 241, 209);
    private readonly Label status = new() { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Label clock = new() { AutoSize = true, ForeColor = Accent, AccessibleName = "Local time" };
    private readonly ThermalFrame thermalFrame = new();
    private readonly Button displays = new() { Text = "Display  ▾", AutoSize = true, MinimumSize = new Size(165, 0), TextAlign = ContentAlignment.MiddleLeft };
    private readonly ContextMenuStrip displayMenu = new() { ShowImageMargin = false, ShowCheckMargin = true };
    private readonly ContextMenuStrip titleMenu = new() { ShowImageMargin = false };
    private readonly ToolStripMenuItem temperatureTest = new("Test Temperature Alert");
    private Screen? selectedDisplay;
    private readonly Button fullscreen = new() { Text = "Fullscreen", AutoSize = true, FlatStyle = FlatStyle.Flat };
    private readonly DataGridView details = new();
    private readonly DataGridView processes = new();
    private readonly Panel[] pages = [new(), new(), new()];
    private readonly Button[] navigation = new Button[3];
    private int selectedPage;
    private readonly ComboBox processSort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Label processStatus = new() { Dock = DockStyle.Fill, ForeColor = Muted, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ProcessReader processReader = new();
    private List<ProcessUsage> processUsage = [];
    private bool samplingProcesses;
    private readonly Button cpuHelp = new() { AutoSize = true, FlatStyle = FlatStyle.Flat, Visible = false };
    private readonly CancellationTokenSource pawnIoDownload = new();
    private readonly CancellationTokenSource releaseUpdate = new();
    private readonly System.Windows.Forms.Timer updateTimer = new() { Interval = 30 * 60 * 1000 };
    private readonly Button updateButton = new() { Text = "Update", AutoSize = true, Visible = false };
    private ReleaseUpdate.Manifest? offeredUpdate;
    private string? pawnIoInstaller;
    private readonly MetricCard cpu = new("CPU", "°C", Accent);
    private readonly MetricCard gpu = new("GPU", "°C", Color.FromArgb(109, 184, 255));
    private readonly MetricCard ram = new("RAM", "%", Color.FromArgb(187, 155, 255));
    private readonly HardwareReader reader = new();
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 1500 };
    private readonly System.Windows.Forms.Timer visualTimer = new() { Interval = 250 };
    private TemperatureAlert cpuAlert;
    private TemperatureAlert gpuAlert;
    private long? temperatureTestStarted;
    private string measurementStatus = "Detecting sensors…";
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
        Font = new Font("Bahnschrift", 11);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        thermalFrame.Controls.Add(layout);
        Controls.Add(thermalFrame);
        layout.Controls.Add(CreateHeader(), 0, 0);

        var nav = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 0, 10) };
        var pageHost = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };
        string[] pageNames = ["System Specs", "Processes", "Sensors"];
        for (var index = 0; index < pages.Length; index++)
        {
            var pageIndex = index;
            var button = new Button { Text = pageNames[index], AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
            StyleButton(button);
            button.Click += (_, _) => SelectPage(pageIndex);
            navigation[index] = button;
            nav.Controls.Add(button);
            pages[index].Dock = DockStyle.Fill;
            pageHost.Controls.Add(pages[index]);
        }
        layout.Controls.Add(nav, 0, 1);
        layout.Controls.Add(pageHost, 0, 2);
        SelectPage(0);

        var specs = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        specs.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        specs.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        pages[0].Controls.Add(specs);

        var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        cards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var index = 0; index < 3; index++)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100 / 3f));
        ram.Margin = Padding.Empty;
        cards.Controls.Add(cpu, 0, 0);
        cards.Controls.Add(gpu, 1, 0);
        cards.Controls.Add(ram, 2, 0);
        specs.Controls.Add(cards, 0, 0);

        ConfigureGrid(details, ("Group", 17), ("Device", 33), ("Sensor", 32), ("Reading", 18));
        pages[2].Controls.Add(details);
        StyleButton(cpuHelp);
        cpuHelp.Click += (_, _) => OpenCpuHelp();
        specs.Controls.Add(cpuHelp, 0, 1);
        status.AutoEllipsis = true;
        layout.Controls.Add(status, 0, 3);
        ConfigureProcesses(pages[1]);

        displays.Click += (_, _) => OpenDisplayMenu();
        fullscreen.Click += (_, _) => ToggleFullscreen();
        updateButton.Click += async (_, _) => await InstallReleaseUpdateAsync();
        updateTimer.Tick += async (_, _) => await CheckReleaseUpdateAsync();
        timer.Tick += async (_, _) => await RefreshDashboard();
        visualTimer.Tick += (_, _) =>
        {
            UpdateClock();
            UpdateTemperatureTest();
            thermalFrame.Animate();
        };
        Shown += async (_, _) =>
        {
            RefreshDisplays();
            RestoreView(args);
            RefreshThermalPresentation();
            UpdateClock();
            visualTimer.Start();
            timer.Start();
            updateTimer.Start();
            await Task.WhenAll(RefreshDashboard(), DownloadPawnIoAsync(), CheckReleaseUpdateAsync());
        };
        FormClosed += (_, _) =>
        {
            closing = true;
            updateTimer.Dispose();
            releaseUpdate.Cancel();
            releaseUpdate.Dispose();
            pawnIoDownload.Cancel();
            pawnIoDownload.Dispose();
            timer.Dispose();
            if (!reading)
                reader.Dispose();
        };
        Disposed += (_, _) =>
        {
            visualTimer.Dispose();
            displayMenu.Dispose();
            titleMenu.Dispose();
        };
    }

    private Panel CreateHeader()
    {
        var header = new Panel { Anchor = AnchorStyles.Left | AnchorStyles.Right, Height = 44, Margin = new Padding(0, 0, 0, 8) };
        var brand = new Label { Text = "Specs Panel", AutoSize = true, Font = new Font(Font.FontFamily, 19, FontStyle.Bold), Cursor = Cursors.Hand };
        ConfigureMenu(titleMenu);
        temperatureTest.ForeColor = Ink;
        temperatureTest.Padding = new Padding(6);
        temperatureTest.Click += (_, _) => StartTemperatureTest();
        titleMenu.Items.Add(temperatureTest);
        titleMenu.Opening += (_, _) => temperatureTest.Enabled = !temperatureTestStarted.HasValue && !HasCriticalTemperature;
        brand.ContextMenuStrip = titleMenu;
        brand.MouseClick += (_, args) =>
        {
            if (args.Button == MouseButtons.Left)
                titleMenu.Show(brand, new Point(0, brand.Height));
        };
        brand.AccessibleDescription = "Opens the temperature alert test menu.";
        clock.Font = new Font(Font.FontFamily, 16, FontStyle.Bold);
        UpdateClock();

        StyleButton(displays);
        displays.AccessibleName = "Select display";
        ConfigureMenu(displayMenu);
        StyleButton(fullscreen);
        StyleButton(updateButton);
        var controls = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false };
        controls.Controls.AddRange([updateButton, displays, fullscreen]);
        header.Controls.AddRange([brand, clock, controls]);

        header.Layout += (_, _) =>
        {
            var rowHeight = Math.Max(Math.Max(brand.Height, controls.Height), clock.Height);
            var clockX = (header.Width - clock.Width) / 2;
            var controlsX = Math.Max(0, header.Width - controls.Width);
            var singleRow = clockX >= brand.Right + 8 && clockX + clock.Width + 8 <= controlsX;
            var height = singleRow ? rowHeight : rowHeight + clock.Height + 4;
            if (header.Height != height)
                header.Height = height;

            brand.Location = new Point(0, (rowHeight - brand.Height) / 2);
            controls.Location = new Point(controlsX, (rowHeight - controls.Height) / 2);
            clock.Location = new Point(Math.Max(0, clockX), singleRow ? (rowHeight - clock.Height) / 2 : rowHeight + 4);
        };
        return header;
    }

    private void ConfigureMenu(ContextMenuStrip menu)
    {
        menu.Renderer = new DisplayMenuRenderer();
        menu.ForeColor = Ink;
        menu.Font = Font;
        menu.Padding = new Padding(3);
    }

    private void UpdateClock()
    {
        var now = DateTime.Now.ToString("HH:mm:ss");
        if (clock.Text != now)
            clock.Text = now;
    }

    private static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(55, 73, 96);
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(32, 48, 65);
        button.BackColor = Surface;
        button.ForeColor = Ink;
        button.Padding = new Padding(8, 3, 8, 3);
        button.UseVisualStyleBackColor = false;
        button.Cursor = Cursors.Hand;
    }

    private static void StylePicker(ComboBox picker)
    {
        picker.FlatStyle = FlatStyle.Flat;
        picker.BackColor = Surface;
        picker.ForeColor = Ink;
        picker.DrawMode = DrawMode.OwnerDrawFixed;
        picker.ItemHeight = 25;
        picker.DrawItem += (_, args) =>
        {
            var highlighted = (args.State & DrawItemState.Selected) != 0 &&
                (args.State & DrawItemState.ComboBoxEdit) == 0;
            using var fill = new SolidBrush(highlighted ? Color.FromArgb(25, 61, 69) : Surface);
            args.Graphics.FillRectangle(fill, args.Bounds);
            if (args.Index < 0)
                return;
            var textBounds = Rectangle.Inflate(args.Bounds, -6, 0);
            TextRenderer.DrawText(args.Graphics, picker.GetItemText(picker.Items[args.Index]), picker.Font,
                textBounds, Ink, TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        };
    }

    private void SelectPage(int index)
    {
        selectedPage = index;
        for (var page = 0; page < pages.Length; page++)
        {
            var active = page == index;
            pages[page].Visible = active;
            navigation[page].ForeColor = active ? Accent : Muted;
            navigation[page].BackColor = active ? Color.FromArgb(17, 48, 51) : Surface;
            navigation[page].FlatAppearance.BorderColor = active ? Accent : Color.FromArgb(55, 73, 96);
            navigation[page].AccessibleDescription = active ? "Selected tab" : "";
        }
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
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Accent;
        grid.DefaultCellStyle.BackColor = Background;
        grid.DefaultCellStyle.ForeColor = Ink;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(25, 61, 69);
        grid.DefaultCellStyle.SelectionForeColor = Ink;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(13, 22, 35);
        grid.RowTemplate.Height = 30;
        foreach (var (name, width) in columns)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = name, FillWeight = width, SortMode = DataGridViewColumnSortMode.NotSortable });
    }

    private void ConfigureProcesses(Panel page)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        page.Controls.Add(panel);

        var filter = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        filter.Controls.Add(new Label { Text = "Sort by", AutoSize = true, Margin = new Padding(4, 6, 8, 4) });
        filter.Controls.Add(processSort);
        StylePicker(processSort);
        processSort.AccessibleName = "Sort processes by";
        processSort.Items.AddRange(["CPU", "RAM", "I/O"]);
        processSort.SelectedIndex = 0;
        processSort.SelectedIndexChanged += (_, _) => RenderProcesses();
        panel.Controls.Add(filter, 0, 0);
        ConfigureGrid(processes, ("Process", 44), ("PID", 12), ("CPU (%)", 14), ("RAM (MiB)", 15), ("I/O (MiB/s)", 15));
        processes.ColumnHeaderMouseClick += (_, eventArgs) =>
        {
            if (eventArgs.ColumnIndex is >= 2 and <= 4)
                processSort.SelectedIndex = eventArgs.ColumnIndex - 2;
        };
        panel.Controls.Add(processes, 0, 1);
        processStatus.Text = "Collecting process data…";
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
            var countLabel = snapshot.Count == 1 ? "process" : "processes";
            processStatus.Text = $"{snapshot.Count} {countLabel}  •  {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            if (!closing)
                processStatus.Text = "Windows couldn't read the process list.";
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
        [selectedPage.ToString(), Screen.FromControl(this).DeviceName, full ? "fullscreen" : "window"];

    private void RestoreView(string[] args)
    {
        if (args.Length != 3)
            return;
        if (int.TryParse(args[0], out var page) && page >= 0 && page < pages.Length)
            SelectPage(page);
        var display = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName == args[1]);
        if (display is null)
            return;
        SelectDisplay(display);
        if (args[2] == "fullscreen")
            ToggleFullscreen();
        else if (Screen.FromControl(this).DeviceName != display.DeviceName)
            Location = display.WorkingArea.Location;
    }

    private async Task CheckReleaseUpdateAsync()
    {
        if (!updateButton.Enabled)
            return;

        try
        {
            var available = await ReleaseUpdate.CheckAsync(releaseUpdate.Token);
            if (closing || !updateButton.Enabled)
                return;
            offeredUpdate = available;
            updateButton.Visible = available is not null;
            if (available is not null)
                updateButton.AccessibleName = $"Update to {available.Version}";
        }
        catch (Exception error) when (error is HttpRequestException or System.Text.Json.JsonException
            or IOException or OperationCanceledException)
        {
            Debug.WriteLine($"Update check failed: {error.Message}");
        }
    }

    private async Task InstallReleaseUpdateAsync()
    {
        if (offeredUpdate is null)
            return;

        updateButton.Enabled = false;
        updateButton.Text = "Updating…";
        try
        {
            if (!await ReleaseUpdate.InstallAsync(offeredUpdate, ViewArguments(), releaseUpdate.Token))
                throw new IOException("The updated application could not start.");
            if (!closing)
                Close();
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException
            or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception)
        {
            if (!closing)
                MessageBox.Show(this, $"The update could not be completed.\n\n{error.Message}", "Specs Panel",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            if (!closing)
            {
                updateButton.Enabled = true;
                updateButton.Text = "Update";
            }
        }
    }

    private async Task DownloadPawnIoAsync()
    {
        if (HardwareReader.PawnIoInstalled)
            return;

        cpuHelp.Enabled = false;
        try
        {
            pawnIoInstaller = await PawnIoDownload.GetInstallerAsync(pawnIoDownload.Token);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException
            or UnauthorizedAccessException or OperationCanceledException)
        {
            Debug.WriteLine($"PawnIO download failed: {error.Message}");
        }
        finally
        {
            if (!closing)
                cpuHelp.Enabled = true;
        }
    }

    private void OpenCpuHelp()
    {
        try
        {
            var installed = HardwareReader.PawnIoInstalled;
            var start = installed
                ? new ProcessStartInfo(Application.ExecutablePath) { UseShellExecute = true, Verb = "runas" }
                : new ProcessStartInfo(PawnIoDownload.ReleasesUrl) { UseShellExecute = true };
            if (installed)
                foreach (var argument in ViewArguments())
                    start.ArgumentList.Add(argument);
            else if (File.Exists(pawnIoInstaller))
                start = new ProcessStartInfo("explorer.exe", $"/select,\"{pawnIoInstaller}\"") { UseShellExecute = true };
            using var started = Process.Start(start);
            if (installed && started is not null)
                Close();
        }
        catch (System.ComponentModel.Win32Exception error) when (error.NativeErrorCode == 1223)
        {
            SetMeasurementStatus("Administrator startup was cancelled.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            SetMeasurementStatus("Couldn't open CPU temperature help.");
        }
    }

    private Screen[] RefreshDisplays()
    {
        var previous = selectedDisplay?.DeviceName ?? Screen.FromControl(this).DeviceName;
        var screens = Screen.AllScreens;
        var selection = screens.FirstOrDefault(screen => screen.DeviceName == previous) ?? screens.FirstOrDefault();
        if (selection is not null)
            SelectDisplay(selection);
        return screens;
    }

    private void SelectDisplay(Screen display)
    {
        selectedDisplay = display;
        displays.Text = $"{new DisplayChoice(display)}  ▾";
    }

    private void OpenDisplayMenu()
    {
        var screens = RefreshDisplays();
        while (displayMenu.Items.Count > 0)
        {
            var oldItem = displayMenu.Items[0];
            displayMenu.Items.RemoveAt(0);
            oldItem.Dispose();
        }
        foreach (var screen in screens)
        {
            var item = new ToolStripMenuItem(new DisplayChoice(screen).ToString())
            {
                Checked = screen.DeviceName == selectedDisplay?.DeviceName,
                ForeColor = Ink,
                Padding = new Padding(4, 6, 12, 6)
            };
            item.Click += (_, _) => SelectDisplay(screen);
            displayMenu.Items.Add(item);
        }
        if (displayMenu.Items.Count > 0)
        {
            displayMenu.MinimumSize = new Size(displays.Width, 0);
            displayMenu.Show(displays, new Point(0, displays.Height));
        }
    }

    private sealed class DisplayMenuRenderer() : ToolStripProfessionalRenderer(new DisplayMenuColors())
    {
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs args)
        {
            if (!args.Item.Selected && !args.Item.Pressed)
                return;

            var row = new Rectangle(1, 0, args.Item.Width - 2, args.Item.Height - 1);
            if (row.Width <= 0 || row.Height <= 0)
                return;

            var background = args.Item.Pressed ? Color.FromArgb(30, 70, 76) : Color.FromArgb(24, 54, 64);
            using var fill = new SolidBrush(background);
            using var border = new Pen(Color.FromArgb(56, 101, 111));
            args.Graphics.FillRectangle(fill, row);
            args.Graphics.DrawRectangle(border, row);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs args)
        {
            var bounds = args.ImageRectangle;
            var markSize = Math.Min(bounds.Width, bounds.Height);
            if (markSize < 4)
                return;

            float x = bounds.Left + bounds.Width / 2f;
            float y = bounds.Top + bounds.Height / 2f;
            using var pen = new Pen(Accent, Math.Max(1.5f, markSize / 8f))
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round,
                LineJoin = System.Drawing.Drawing2D.LineJoin.Round
            };
            args.Graphics.DrawLine(pen, x - markSize * .28f, y, x - markSize * .08f, y + markSize * .18f);
            args.Graphics.DrawLine(pen, x - markSize * .08f, y + markSize * .18f, x + markSize * .3f, y - markSize * .2f);
        }
    }

    private sealed class DisplayMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Surface;
        public override Color MenuBorder => Color.FromArgb(55, 73, 96);
        public override Color ImageMarginGradientBegin => Surface;
        public override Color ImageMarginGradientMiddle => Surface;
        public override Color ImageMarginGradientEnd => Surface;
    }

    private sealed record DisplayChoice(Screen Screen)
    {
        public override string ToString() => Screen.DeviceName switch
        {
            @"\\.\DISPLAY1" => "MAIN MONITOR",
            @"\\.\DISPLAY2" => "ALT MONITOR",
            @"\\.\DISPLAY10" => "TABLET",
            _ => $"{Screen.DeviceName.Replace(@"\\.\DISPLAY", "Display ")} • {Screen.Bounds.Width}×{Screen.Bounds.Height}"
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
            var selectedName = selectedDisplay?.DeviceName;
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
        fullscreen.Text = full ? "Exit Fullscreen" : "Fullscreen";
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
            var cpuAvailable = UpdateCards(metrics);
            SetRowCount(details, metrics.Count);
            for (var index = 0; index < metrics.Count; index++)
            {
                var metric = metrics[index];
                details.Rows[index].SetValues(metric.Group, metric.Device, metric.Name, Format(metric));
            }
            var showCpuHelp = !cpuAvailable && (!HardwareReader.PawnIoInstalled || !HardwareReader.IsAdministrator);
            cpuHelp.Visible = showCpuHelp;
            cpuHelp.Text = HardwareReader.PawnIoInstalled ? "Run as administrator to read CPU temperature"
                : !cpuHelp.Enabled ? "Downloading PawnIO…"
                : File.Exists(pawnIoInstaller) ? "Show PawnIO installer"
                : "Download PawnIO for CPU temperature";
            SetMeasurementStatus(!cpuAvailable && !showCpuHelp
                ? "CPU temperature sensor returned no reading."
                : metrics.Any(metric => metric.Value.HasValue)
                ? $"Last updated at {DateTime.Now:HH:mm:ss}"
                : "Sensor readings are unavailable.");
        }
        catch (Exception error)
        {
            if (!closing)
                SetMeasurementStatus($"Couldn't read sensors ({error.GetType().Name}).");
        }
        finally
        {
            reading = false;
            if (closing)
                reader.Dispose();
        }
    }

    private bool UpdateCards(List<Metric> metrics)
    {
        var cpuMetrics = metrics.Where(metric => metric.Group == "CPU").ToArray();
        var gpuMetrics = metrics.Where(metric => metric.Group == "GPU").ToArray();
        var memory = metrics.Where(metric => metric.Group == "RAM" && metric.Device != "Virtual Memory").ToArray();
        var cpuTemperature = SelectMetric(cpuMetrics, "°C", "CPU Package", "Core (Tctl/Tdie)", "Core (Tdie)", "Core (Tctl)", "Core Average", "Core Max");
        var gpuTemperature = SelectMetric(gpuMetrics, "°C", "GPU Core");
        var memoryLoad = SelectMetric(memory, "%", "Memory");

        var cpuName = cpuTemperature?.Device ?? cpuMetrics.FirstOrDefault()?.Device ?? "CPU not detected";
        var gpuName = gpuTemperature?.Device ?? gpuMetrics.FirstOrDefault()?.Device ?? "GPU not detected";
        var cpuLoad = cpuMetrics.FirstOrDefault(metric => metric.Device == cpuName && metric.Name == "CPU Total" && metric.Unit == "%");
        var gpuLoad = gpuMetrics.FirstOrDefault(metric => metric.Device == gpuName && metric.Name == "GPU Core" && metric.Unit == "%");
        cpuAlert = ThermalAlerts.Update(cpuTemperature?.Value, cpuAlert);
        gpuAlert = ThermalAlerts.Update(gpuTemperature?.Value, gpuAlert);
        cpu.SetReading(cpuTemperature?.Value, cpuName, $"Load  {Format(cpuLoad)}", cpuAlert);
        gpu.SetReading(gpuTemperature?.Value, gpuName, $"Load  {Format(gpuLoad)}", gpuAlert);

        var used = memory.FirstOrDefault(metric => metric.Device == memoryLoad?.Device && metric.Name == "Memory Used" && metric.Unit == "GB")?.Value;
        var free = memory.FirstOrDefault(metric => metric.Device == memoryLoad?.Device && metric.Name == "Memory Available" && metric.Unit == "GB")?.Value;
        var capacity = used.HasValue && free.HasValue ? $"{used:0.#} / {used + free:0.#} GB" : "—";
        ram.SetReading(memoryLoad?.Value, "Physical memory", $"In use  {capacity}");
        return cpuTemperature is not null;
    }

    private bool HasCriticalTemperature => cpuAlert == TemperatureAlert.Critical || gpuAlert == TemperatureAlert.Critical;

    private void StartTemperatureTest()
    {
        if (temperatureTestStarted.HasValue || HasCriticalTemperature)
            return;
        temperatureTestStarted = Stopwatch.GetTimestamp();
        RefreshThermalPresentation();
    }

    private void UpdateTemperatureTest()
    {
        if (temperatureTestStarted is not long start || Stopwatch.GetElapsedTime(start) < TimeSpan.FromSeconds(15))
            return;
        temperatureTestStarted = null;
        RefreshThermalPresentation();
    }

    private void SetMeasurementStatus(string message)
    {
        measurementStatus = message;
        RefreshThermalPresentation();
    }

    private void RefreshThermalPresentation()
    {
        var critical = HasCriticalTemperature;
        if (critical)
            temperatureTestStarted = null;
        var testing = temperatureTestStarted.HasValue;
        cpu.SetTemperaturePreview(testing ? 89f : null);

        var pulsing = critical || testing;
        thermalFrame.SetCritical(pulsing);
        var interval = pulsing ? 50 : 250;
        if (visualTimer.Interval != interval)
            visualTimer.Interval = interval;

        status.ForeColor = pulsing ? Color.FromArgb(255, 116, 126) : Muted;
        status.Text = critical ? CriticalTemperatureMessage()
            : testing ? "TEST ONLY • SIMULATED CPU TEMPERATURE (89 °C)"
            : measurementStatus;
    }

    private string CriticalTemperatureMessage() => (cpuAlert, gpuAlert) switch
    {
        (TemperatureAlert.Critical, TemperatureAlert.Critical) => "CRITICAL TEMPERATURE • CPU and GPU",
        (TemperatureAlert.Critical, _) => "CRITICAL TEMPERATURE • CPU",
        _ => "CRITICAL TEMPERATURE • GPU"
    };

    private static Metric? SelectMetric(IEnumerable<Metric> options, string unit, params string[] preferredNames)
    {
        var available = options.Where(metric => metric.Unit == unit && metric.Value.HasValue).ToArray();
        return preferredNames.Select(name => available.FirstOrDefault(metric => metric.Name == name))
            .FirstOrDefault(metric => metric is not null)
            ?? available.MaxBy(metric => metric.Value);
    }

    private static string Format(Metric? metric) => metric?.Value is float value ? $"{value:0.#} {metric.Unit}" : "—";
}
