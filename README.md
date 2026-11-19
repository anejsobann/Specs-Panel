# Specs Panel

Windows dashboard for monitoring your PC on a second display.

## Getting started

Run `./Publish-Local.ps1` in PowerShell with the .NET 10 SDK, then open the `SpecsPanel.exe` at the printed path. Approve the Windows UAC prompt if it appears. Specs Panel uses administrator access to read CPU temperature sensors.

1. Connect your Samsung tablet through spacedesk.
2. In Windows display settings, choose **Extend these displays**. Use **Identify** to check the tablet's display number.
3. Select the tablet in Specs Panel and click **Fullscreen**. Press **Esc** or **F11** to return to windowed mode.

**System Specs** shows CPU, GPU, and RAM cards with live readings and charts covering the last 60 seconds.

CPU and GPU temperatures turn red at 80 °C. At 88 °C, Specs Panel shows a critical message and pulses all four edges red on a 3.2-second cycle, including in fullscreen. The alert clears 2 °C below each threshold to avoid switching repeatedly near the boundary.

**Processes** lists running processes and their PIDs. Sort by share of total CPU capacity, physical memory (working set), or read and write I/O throughput, which can include network and other devices. CPU and I/O rates need two samples. A dash indicates an unavailable reading.

**Sensors** shows detailed readings for your motherboard, drives, clock speeds, and fans when available.

[LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) uses the [PawnIO](https://github.com/namazso/PawnIO.Setup/releases) driver on supported Ryzen processors. If the driver is missing, use the download button under **System Specs**.

## Automatic local updates

After `./Publish-Local.ps1` builds successfully, an open instance running with administrator access automatically switches to the new build and keeps its selected tab, display, and fullscreen mode. The script creates a separate directory for each build under `Specs Panel/bin/Local`.
