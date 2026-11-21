# Touch Bar for Windows (MacBook Pro Edition)

<p align="center">
  <a href="https://github.com/Gleb200/MacBook-TouchBar-Windows/releases"><img src="https://img.shields.io/badge/release-v1.0.1-blue.svg" alt="Release" /></a>
  <a href="LICENSE.txt"><img src="https://img.shields.io/badge/License-MIT-green.svg" alt="License: MIT" /></a>
  <img src="https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-informational.svg" alt="Platform: Windows 10 | 11 (x64)" />
  <img src="https://img.shields.io/badge/.NET-Core%203.0%20%2F%203.1-purple.svg" alt=".NET Core 3.0 / 3.1" />
</p>

**A feature-rich, high-performance Touch Bar host and Control Center for Apple MacBook Pro running Windows 10/11 through Boot Camp.**

Renders custom UI directly to the Touch Bar hardware framebuffer at smooth 60 FPS with zero lag, bringing native media control, intelligent audio device tracking, an adaptive running apps switcher dock with touch gestures, interactive pets, multi-source CPU thermals, and MacBook hardware integrations.

Supports all Touch Bar MacBook Pro models (13", 15", 16" from 2016 through 2020 with Apple T1 and T2 Security Chips).

---

## Screenshots

### 1. Overall Touch Bar View
<p align="center">
  <img src="https://github.com/user-attachments/assets/0efa5b2b-2d07-4e09-91bd-c5a8a8b97fa9" width="600" alt="Touch Bar Overview" /><br>
  <em>General view of the Touch Bar in action: ESC button, adaptive dock, volume/brightness controls, and companion pet.</em>
</p>

### 2. Brightness Slider
<p align="center">
  <img src="https://github.com/user-attachments/assets/f0aff591-0a11-4999-b7d6-9ccc245dfabd" width="600" alt="Brightness Slider" /><br>
  <em>Touch Bar brightness slider drawer with real-time level adjustment and auto-brightness toggle.</em>
</p>

### 3. Touch ID & Windows Hello Prompt
<p align="center">
  <img src="https://github.com/user-attachments/assets/39a4bf55-028d-4758-9390-70e75ee13161" width="600" alt="Touch ID Prompt" /><br>
  <em>macOS-style Touch ID / Windows Hello biometric verification prompt on the Touch Bar.</em>
</p>

> [!IMPORTANT]
> **Touch ID Sensor in Windows:** To enable the physical Touch ID fingerprint sensor on Apple T2 MacBook Pro under Windows, install the open-source driver from **[Osaul4ik/apple-t2-touchid-windows](https://github.com/Osaul4ik/apple-t2-touchid-windows)**.

### 4. Media Playback (Now Playing)
<p align="center">
  <img src="https://github.com/user-attachments/assets/5b9bbb5e-1bb8-4d51-a767-75ca384a60ac" width="600" alt="Media Playback Strip" /><br>
  <em>Live SMTC media strip displaying current track name, artist, and playback controls with companion pet dancing to music.</em>
</p>

### 5. Control Center & Settings Application
<p align="center">
  <img src="https://github.com/user-attachments/assets/62e02bac-4903-44ab-bd7b-dca24ab40fbf" width="520" alt="TouchBar Settings" /><br>
  <em>TouchBarSettings.exe GUI Control Center for customizing widgets, pets, hotkeys, and display options.</em>
</p>


---

## Key Features

### 1. Direct 60 FPS Hardware Framebuffer Rendering
* Driven by the low-level `DFRDisplayKm.sys` kernel driver and `Avalonia.DfrFrameBuffer.dll`.
* Writes directly to the USB framebuffer endpoint (`\\.\DFRDisplayKm`) at 60 FPS with minimal CPU overhead (~0.1% – 0.3%).
* Dedicated digitizer touch processing with sub-pixel touch tracking and zero-latency feedback.

### 2. Intelligent Windows Core Audio Control
* **Dynamic Audio Device Tracking (`IMMNotificationClient`):**
  Automatically monitors default audio render endpoint changes. When external speakers (e.g. JBL Link Portable, Bluetooth speaker), USB DAC, or headphones are connected or disconnected, the volume slider instantly switches to control the active device instead of remaining stuck on built-in Apple Audio Device speakers.
* **Bi-directional Real-Time Volume Sync (`IAudioEndpointVolumeCallback`):**
  Adjustments made via hardware buttons, keyboard shortcuts, or Windows taskbar flyouts instantly reflect on the Touch Bar slider.
* **Control Center Capsule Slider with Dual-Gesture Speaker Icon:**
  * **Tap Speaker Icon ($X \le 38\text{ px}$):** Toggles Mute / Unmute instantly without altering the current volume level. Icon turns red (`\uE74F`), label reads "Mute", and track dims.
  * **Swipe to Adjust:** Dragging from the left edge smoothly slides volume from 0% to 100%. Sliding up automatically unmutes the audio.

### 3. Brightness Control & Power Menu
* **Touch Bar Brightness Drawer:**
  * Smooth capsule slider for display brightness.
  * Auto-Brightness toggle button directly inside the drawer.
* **Power Management Drawer:**
  * Quick Lock (`LockWorkStation`).
  * Restart (`shutdown /r /t 0`).
  * Shut Down with two-step confirmation ("Shut Down" $\rightarrow$ "Confirm?") to prevent accidental taps.

### 4. Adaptive App Switcher / Dock (macOS-Style)
* **Live Running Applications:**
  Discovers top-level non-minimized desktop windows, extracts crisp high-DPI icons, and displays active window highlight indicators.
* **Window Switching & Minimizing:**
  * Tap an inactive app to bring it to the foreground (bypasses Windows foreground lock restrictions via simulated key events).
  * Tap the active app tab to minimize its window to the taskbar.
* **Adaptive Shrinking (Dynamic Tab Width):**
  Tabs dynamically scale down from 160 px down to 65 px as more apps open, preserving clean readability and icon alignment without immediately requiring scroll.
* **Horizontal Touch Scroll & Auto-Focus:**
  * Smooth horizontal pan gesture ($\ge 8\text{ px}$) with inertia to browse overflowed tabs.
  * Auto-scrolls the dock strip to always keep the foreground window visible.

### 5. 10 Interactive Companion Pets
* **10 Animated Character Skins:**
  * **Eevee, Pikachu, Charmander, Bulbasaur, Squirtle, Gengar, Snorlax, Lucario, Marisa, Reimu.**
* **Rich Animations:**
  * Walking with direction flips, idle breathing, and sleeping when the system is inactive.
  * **Music Dancing:** Characters dance whenever media playback is active!
  * **Touch Gestures:** Drag and drop pets along the Touch Bar; tap to trigger affection emotes (hearts, notes, sparks).
* **Static Pinning Mode (📌):**
  Lock the character in place so it stays stationary and only moves when dragged with a finger.

### 6. Media Control Strip (Now Playing / SMTC)
* Deep integration with Windows System Media Transport Controls (`Windows.Media.Control`).
* Supports Spotify, Apple Music, YouTube Music, Chrome, Edge, and media players.
* Displays live Track Title, Artist, and playback controls (Previous, Play/Pause, Next).
* Smooth crossfade transition between the Dock and Media strip.

### 7. Cascading Multi-Source CPU Thermals (Auto-Fallback)
An automatic multi-source thermal engine that gracefully falls back across sensor providers without UI freezes or WMI locks on Apple T2 chips:
1. **[WMF Control T2 Edition](https://github.com/Gleb200/WMF-Control-T2-Edition) / Macs Fan Control (Shared Memory):**
   When [WMF Control T2 Edition](https://github.com/Gleb200/WMF-Control-T2-Edition) is running in the background, Touch Bar reads CPU temperature directly from fast inter-process Shared Memory (`Local\WMF_CpuTemp` / `Global\WMF_CpuTemp`). This provides instantaneous updates with zero CPU overhead without duplicating queries to the Apple T2 SMC bus. If WMF Control is not running or not installed, the engine seamlessly switches to built-in fallback sources.
2. **LibreHardwareMonitorLib (Direct MSRs):**
   Reads Intel DTS MSRs directly via Ring0 driver with TjMax Distance filtering.
3. **Core Temp (Shared Memory):**
   Reads `CoreTempMappingObject`.
4. **AIDA64 (Shared Memory XML):**
   Reads `AIDA64_SensorValues`.
5. **Windows ACPI WMI:**
   Safe query to `MSAcpi_ThermalZoneTemperature` with an asynchronous 200 ms timeout protection.
* **Anti-Flicker Buffer:** Holds the last valid temperature for up to 3.5 seconds across transient bus misses.

### 8. MacBook Pro Hardware Integrations
* **Virtual ESC Key:**
  Essential left-aligned Escape key for 2016–2019 MacBook Pro models lacking a physical Escape key.
* **Global Fn Row (F1–F12):**
  Holding the physical `Fn` key on the MacBook keyboard flips the bar into standard F1–F12 keys.
* **System Overlays:**
  * Windows Hello / Touch ID biometric prompt banner.
  * Administrator UAC elevation dialog confirm buttons.
  * MagSafe / USB-C charging connection banner.

### 9. Fluent Windows 11 Settings Center (`TouchBarSettings.exe`)
* Modern dark-themed WPF configuration utility.
* Bilingual: full English and Russian localization.
* Widget reordering and visibility toggles.
* Live synchronization with `config.json` without requiring application restarts.

---

## Installation

> [!IMPORTANT]
> **MacBook Pro 2018–2020 (Apple T2 Chip) Notice:**
> Apple T2 machines require two prerequisite steps before third-party kernel drivers can run in Windows Boot Camp: disabling Apple Secure Boot in macOS Recovery, and enabling Windows Test Signing. Follow the steps below carefully.

### Step 1: Prerequisites (One-Time Setup)

1. **Turn off Apple Secure Boot:**
   * Restart your Mac and hold `Cmd + R` until the Apple logo appears to enter **macOS Recovery**.
   * In the top menu bar, open **Utilities** $\rightarrow$ **Startup Security Utility**.
   * Enter your macOS administrator password.
   * Under **Secure Boot**, select **No Security**.
   * Under **Allowed Boot Media**, ensure **Allow booting from external or removable media** is selected.
   * Click the Apple logo $\rightarrow$ Restart $\rightarrow$ Boot into **Windows**.

2. **Enable Windows Test Mode (Test Signing):**
   * In Windows, open **Command Prompt (cmd.exe)** as **Administrator** (Right-click $\rightarrow$ Run as administrator).
   * Run the command:
     ```cmd
     bcdedit -set TESTSIGNING ON
     ```
   * **Reboot your computer.**
   * *(After rebooting, a small "Test Mode" watermark will appear in the bottom-right corner of the Windows desktop. If you ever want to turn off test mode, run `bcdedit -set TESTSIGNING OFF` and reboot).*

---

### Step 2: Driver & Software Installation

> [!TIP]
> **Driver & Certificate are bundled in the release:** The `DFRDisplayKm` driver and its security certificate are already included in the release archive in the `Driver/` folder! You do **not** need to download them separately.

1. **Download & Extract Release:**
   * Download the latest **`TouchBar-Windows-v1.0.1-win64.zip`** from **[Releases](https://github.com/Gleb200/MacBook-TouchBar-Windows/releases)**.
   * Extract the ZIP archive into a permanent folder (e.g. `C:\Program Files\TouchBar` or anywhere on your SSD).

2. **Run Driver Installer (Automated 1-Click):**
   * Right-click **`Install_Driver.bat`** and select **Run as administrator**.
   * What this script does automatically:
     - Imports `DFRDisplayKm.cer` into Windows **Trusted Root Certification Authorities** and **Trusted Publishers** (eliminates Code 52 unsigned driver errors on 64-bit Windows).
     - Installs `DFRDisplayKm.inf` into the Windows DriverStore via PnPUtil.
     - Starts and sets `DFRDisplayKm` and `SensrSvc` services to automatic startup.
     - Refreshes the Apple T2 iBridge USB controller (`USB\VID_05AC&PID_8302`) to immediately switch to configuration 1 (Touch Bar Display mode).
   * Verify: You should see `SUCCESS! Apple Touch Bar Display is active and ready!` in green.

3. **Launch Touch Bar:**
   * Double-click **`Start_TouchBar.bat`** (or `TouchBar.exe`).
   * The Touch Bar will instantly light up at 60 FPS with media controls, clock, battery, dynamic app switcher dock, CPU temperature, and companion pet!

4. **Enable Autostart on Logon:**
   * Right-click **`Add_to_Startup.bat`** and select **Run as administrator**.
   * This registers the silent Windows Task Scheduler task `TouchBarAutoStart` with highest privileges, enabling autostart on user logon even when running on battery without any UAC prompts.

5. **Customize via Control Center:**
   * Launch **`TouchBar_Settings.bat`** (or click the tray icon $\rightarrow$ **⚙ Touch Bar Settings**) to customize widgets, change companion skins, and adjust brightness.

---

### Troubleshooting (MacBook Pro 2018 / T2)

* **Touch Bar stays black / nothing appears:**
  1. Open **Device Manager** (`devmgmt.msc`).
  2. Look under **Universal Serial Bus devices** or **System devices** for **Apple Touch Bar Display** (or `iBridge Display Device`).
  3. **Yellow exclamation mark (Code 52):** Windows Test Mode is not enabled or the certificate was not imported. Make sure you ran `bcdedit -set TESTSIGNING ON`, rebooted, and ran `Install_Driver.bat` as Administrator.
  4. **Device not listed at all:** Right-click **`Restart_TouchBar.bat`** $\rightarrow$ **Run as administrator**. This triggers `Fix_TouchBar.ps1` to pulse the Apple T2 iBridge USB composition device and re-bind the display interface.
  5. Check `crash.txt` in the program folder for detailed diagnostic logs.

* **Settings window takes long to open:**
  - Resolved: Settings window now queries system state asynchronously and loads in milliseconds. Use `TouchBar_Settings.bat` or double-click the system tray icon.

* **CPU Temperature shows as blank or dashes:**
  - Resolved: Temperature now updates immediately upon startup. For optimal accuracy and zero CPU overhead on T2 Macs, keep **[WMF-Control-T2-Edition](https://github.com/Gleb200/WMF-Control-T2-Edition)** running in the background.

---

## Uninstallation

To cleanly and completely remove Touch Bar, its autostart entry, and the kernel driver from Windows:
1. Open the application folder.
2. Right-click **`Uninstall_TouchBar.bat`** and select **Run as administrator**.
   * Automatically stops running processes, deletes the Task Scheduler autostart task, uninstalls the `DFRDisplayKm` driver from Windows Driver Store, removes installed certificates, and resets the Apple T2 iBridge controller back to default hardware function keys.
3. Delete the application folder.
4. *(Optional)* To disable Windows Test Mode, run `bcdedit -set TESTSIGNING OFF` in an Administrator Command Prompt and reboot.

---

## Building from Source

### Prerequisites
* Windows 10/11 x64
* [Visual Studio 2019 / 2022](https://visualstudio.microsoft.com/) or [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet/3.1)
* Workloads: `.NET desktop development`

### Build Command
```powershell
# Clone repository
git clone https://github.com/Gleb200/MacBook-TouchBar-Windows.git
cd MacBook-TouchBar-Windows

# Build entire solution in Release configuration
dotnet build -c Release DFRContentHost.sln
```

Build outputs will be generated in:
* `DFRContentHost\bin\Release\netcoreapp3.0\`
* `TouchBarSettings\bin\Release\netcoreapp3.1\`
* `TouchBar\bin\Release\netcoreapp3.1\`

---

## Project Structure

```
├── Avalonia.DfrFrameBuffer/  # Low-level DFRDisplayKm driver bridge & touch digitizer
├── DFRContentHost/           # Main Avalonia UI host, Control Strip, Dock, Telemetry
│   ├── Assets/               # UI graphics, icons, and fonts
│   ├── Interop/              # CoreAudio, CpuTemperatureMonitor, NativeMethods
│   ├── Models/               # AppConfig, ConfigManager
│   ├── ViewModels/           # MainViewModel, Telemetry, Companion, SMTC
│   └── pets/                 # Sprite sheets for 10 companion characters
├── Driver/                   # DFRDisplayKm kernel driver & test certificate
├── TouchBarSettings/         # Fluent Dark Windows 11 WPF settings application
├── TouchBar/                 # Elevated silent application launcher
├── Install_Driver.bat        # 1-click driver & certificate installer (Admin)
├── Add_to_Startup.bat        # Configures Task Scheduler autostart on logon
├── Remove_from_Startup.bat   # Unregisters Task Scheduler autostart
├── Start_TouchBar.bat        # Launches the Touch Bar host
├── Restart_TouchBar.bat      # Restarts services and Touch Bar host
├── TouchBar_Settings.bat     # Launches the TouchBarSettings GUI
├── DFRContentHost.sln        # Visual Studio solution file
└── LICENSE.txt               # MIT License
```

---

## Companion App & Ecosystem

> [!TIP]
> **Recommended Companion:** For complete hardware control of your MacBook Pro (2016–2020) running Windows in Boot Camp, use this project paired with **[WMF-Control-T2-Edition](https://github.com/Gleb200/WMF-Control-T2-Edition)**.
>
> While **Touch Bar for Windows** gives you tactile controls, media playback, and app switching, **WMF Control T2 Edition** manages dual fan curves, monitors Apple T2 temperature sensors, and streams real-time CPU thermal data directly to the Touch Bar widget via high-speed Shared Memory (`Local\WMF_CpuTemp`) with zero CPU overhead.

---

## Acknowledgments & Credits

* **Bingxing Wang ([imbushuo](https://github.com/imbushuo))** & **[Naozumi520](https://github.com/Naozumi520/DFRDisplayKm)** for the pioneering [DFRDisplayKm](https://github.com/Naozumi520/DFRDisplayKm) kernel driver and original DFRContentHost concept.
* **[Osaul4ik](https://github.com/Osaul4ik/apple-t2-touchid-windows)** for the Apple T2 Touch ID driver for Windows.
* **Avalonia UI** for the cross-platform XAML framework.
* **LibreHardwareMonitor** contributors for hardware sensor abstractions.
* **Gleb200** for the MacBook Pro Windows Touch Bar enhancements, Core Audio dynamic device switching, adaptive dock touch scrolling, multi-source thermal engine, companion pets, and settings center.

---

## License

Distributed under the [MIT License](LICENSE.txt).
