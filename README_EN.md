# WinVClip

<div align="center">

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**A lightweight and powerful Windows clipboard manager**

[Features](#-features) • [Installation](#-installation) • [Usage Guide](#-usage-guide) • [Architecture](#-architecture)

[中文文档](README.md)

</div>

---

## 📖 Introduction

WinVClip is a Windows clipboard manager developed with AI assistance, designed to help users efficiently manage clipboard history. It automatically captures and stores clipboard content, supports multiple data types, and provides convenient search, grouping, and management features.


<img width="627" height="612" alt="1" src="https://github.com/user-attachments/assets/12638bde-6ccf-46a4-a3c7-568557854fdc" />
<img width="600" height="500" alt="5e" src="https://github.com/user-attachments/assets/da24871d-a4de-4e3f-a4b2-a76b7d7bf00f" />


<img width="750" height="620" alt="1e" src="https://github.com/user-attachments/assets/b7bbe2e2-4c15-4034-8f08-3fc83e406d2e" />
<img width="750" height="620" alt="2e" src="https://github.com/user-attachments/assets/192df508-e5ae-4717-86d4-e26f0f134946" />

---

## ✨ Features

### Core Features

| Feature | Description |
|---------|-------------|
| 🔍 **Multi-type Support** | Supports text, images, file lists, rich text (RTF/HTML), and more clipboard content types |
| 📋 **History Records** | Automatically saves clipboard history with unlimited storage, easy to review and reuse |
| ⌨️ **Hotkey Operation** | Global hotkey to quickly show main window, default `Ctrl+Shift+V`, customizable |
| 🎯 **Smart Deduplication** | Automatically detects and filters duplicate content to avoid history redundancy |
| 📁 **Group Management** | Organize clipboard items into groups, supports creating, editing, and deleting groups |
| 🔎 **Search Function** | Built-in search engine for quick retrieval of clipboard content |
| 🎨 **Theme Switching** | Supports light/dark themes, can automatically follow system settings |
| 🚀 **Auto Startup** | Supports auto-start on boot, ready to use without manual launch |

### Advanced Features

| Feature | Description |
|---------|-------------|
| 🧹 **Auto Cleanup** | Set automatic cleanup of expired history records, supports custom retention days and item limits |
| 🗑️ **Manual Cleanup** | Clear all history, clear ungrouped history, or clear records older than N days |
| 💾 **Data Backup** | Automatic database backup, supports custom backup frequency and retention count |
| 📍 **System Tray** | Minimizes to system tray, doesn't occupy taskbar space |
| 🔒 **Data Persistence** | Uses SQLite database for storage, data is safe and reliable |
| 🖥️ **Smart Paste** | Automatically detects terminal environment, intelligently chooses `Ctrl+V` or `Shift+Insert` |
| 📌 **Window Pin** | Supports window always-on-top for convenient comparison operations |
| 🏷️ **Type Filter** | Filter history records by content type (text/image/file/rich text/link) |
| 🔗 **Quick Actions** | Right-click menu supports quick search, open files, visit links, and more |
| 🔤 **Split Characters** | Split text into individual characters/words for selective insertion |
| 🔣 **Character Panel** | Built-in special character panel for quick access to symbols and characters |
| 🔠 **Font Size** | Adjustable interface font size (10-30), applies to main window and character picker instantly |
| ⚙️ **Quick Commands** | Custom text processing rule chains, supporting regex replacement, extraction, formatting, etc. |
| 🎹 **Global Hotkeys** | Custom global keyboard shortcuts to quickly access history records and perform actions |
| 🖱️ **Main UI Shortcuts** | Custom "modifier + mouse" combinations to perform quick actions on clipboard items |
| 🌐 **Multi-language** | Supports Chinese and English, language changes apply instantly without restart |
| ⚡ **Memory Optimization** | Background automatic memory cleanup, auto releases memory when minimized, lower resource usage |

---

## 🖥️ System Requirements

- **Operating System**: Windows 7 SP1 or higher
- **Runtime**: .NET Framework 4.8 or higher

---

## 📥 Installation

### Option 1: Direct Run

1. Go to the [Releases](https://github.com/adyhwang/WinVClip/releases) page to download the latest version
2. Extract the downloaded archive
3. Double-click to run `WinVClip.exe`

### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/adyhwang/WinVClip.git
cd WinVClip
```

Build with Visual Studio:

1. Open the `WinVClip.sln` solution
2. Select `Release` configuration
3. Click `Build` → `Build Solution`
4. Build output is located in `WinVClip\bin\Release\net48\` directory

---

## 📚 Usage Guide

### Hotkeys

| Hotkey | Function |
|--------|----------|
| `Ctrl+Shift+V` | Show/hide main window (default, customizable) |
| `Esc` | Hide main window |
| `Ctrl+Left Click` | Paste as plain text (in main UI, customizable) |
| `Shift+Left Click` | Paste as plain text (remove newlines, in main UI, customizable) |
| `Middle Click` | Paste as plain text (remove newlines, in main UI, customizable) |
| `Right Click` | Open context menu |

### Basic Operations

#### Copy & Capture
- Use `Ctrl+C` normally to copy content, WinVClip will automatically capture
- Supports capturing text, images, files, rich text, and more types

#### View & Paste
- Press hotkey or click tray icon to open main window
- Click a history item to paste to the current focus position
- **Middle-click** on text or rich text items to paste as plain text (newlines automatically removed)

#### Edit & Manage
- **Edit Content**: Select "Edit" from context menu
- **Delete Record**: Select "Delete" from context menu
- **Batch Operations**: Select "Multi-Select Mode" from context menu, supports batch delete and grouping

#### Search & Filter
- Enter keywords in the search box for quick search
- Click filter button to filter by type or group

#### Split Characters
- Right-click on a text or rich text item and select "Split Characters"
- The text will be split into individual characters (Chinese) or words (English)
- Click or drag to select characters you want to insert
- Selected characters will be concatenated in the text area
- Click "Insert" to paste the selected content

### Group Management

1. Click main window context menu → "Add to Group"
2. Select existing group or create new group
3. Quickly locate content through group filter
4. Edit or delete groups in Settings → Group Management

### Settings Options

#### General Settings
- **Hotkey**: Customize global hotkey (show/hide main window)
- **Theme**: Light/Dark/Follow System
- **Paste Method**: Auto select, Ctrl+V, Shift+Insert
- **Language**: Chinese/English, changes apply instantly
- **Font Size**: Adjustable from 10 to 30, applies instantly to main window and character picker
- **Auto Startup**: Auto launch program on system boot
- **Window Always On Top**: Main window stays on top of all windows

#### Global Hotkeys
- **Function**: Custom global keyboard shortcuts to quickly operate without opening the main window
- **Configuration**: Set shortcut combination (Ctrl/Shift/Alt/Win + key), specify which history item (1-N) to act on
- **Supported Actions**: Direct paste, paste as plain text, execute quick command, etc.

#### Main UI Shortcuts
- **Function**: Perform quick actions on clipboard items through "modifier + mouse" combinations when main UI is open
- **Default Mappings**: 
  - `Ctrl + Left Click`: Paste as plain text
  - `Shift + Left Click`: Paste without newlines
  - `Middle Click`: Paste without newlines
  - `Alt + Middle Click`: Open in browser
- **Customizable Actions**: Split characters, edit, delete, group, generate QR code, execute quick command, etc.

#### Capture Settings
- **Monitor Switch**: Enable/disable clipboard monitoring
- **Capture Types**: Select content types to capture

#### History Records
- **Deduplication**: Enable/disable duplicate content filtering
- **Auto Cleanup**: Set retention days and maximum item count
- **Manual Cleanup**: Clear all history, clear ungrouped history, or clear records older than N days

#### Storage & Backup
- **Database Location**: View or change database path
- **Backup Settings**: Set backup frequency and retention count

#### Search Engine
- Select default search engine (Bing, Baidu, Google, etc.)
- Supports adding custom search engines

#### Quick Commands
- **Function**: Custom text processing rule chains for batch processing clipboard text
- **Supported Operations**: String replacement, regex replacement, regex extraction, case conversion, whitespace removal, newline removal, line deduplication, etc.
- **Use Cases**: Extract phone numbers/emails/links, format text, clean up data, etc.
- **How to Trigger**: Via main UI context menu, main UI shortcuts, or global hotkeys

---

## 🏗️ Architecture

### Tech Stack

| Technology | Usage |
|------------|-------|
| **WPF** | User interface framework |
| **.NET Framework 4.8** | Runtime environment |
| **SQLite** | Data persistence storage |
| **Windows API** | System-level functionality |

### Core Dependencies

| Package | Version | Usage |
|---------|---------|-------|
| Microsoft.Data.Sqlite | 6.0.31 | SQLite database access |
| System.Text.Json | 6.0.9 | JSON serialization |
| System.Drawing.Common | 6.0.0 | Image processing |

### Project Structure

```
WinVClip/
├── Models/                      # Data model layer
│   ├── AppSettings.cs          # Application settings model
│   ├── CharGroupData.cs        # Character group data model
│   ├── ClipboardItem.cs        # Clipboard item model
│   ├── ClipboardType.cs        # Clipboard type enum
│   ├── GlobalHotkey.cs         # Global hotkey model
│   ├── Group.cs                # Group model
│   ├── LanguageModel.cs        # Language model
│   ├── QuickCommand.cs         # Quick command model
│   ├── QuickPasteShortcut.cs   # Main UI shortcut model
│   ├── RangeObservableCollection.cs  # Range observable collection
│   └── SearchEngine.cs         # Search engine model
│
├── Services/                    # Service layer
│   ├── BackupService.cs        # Data backup service
│   ├── CleanupService.cs       # Auto cleanup service
│   ├── ClipboardMonitor.cs     # Clipboard monitoring service
│   ├── DatabaseService.cs      # Database operation service
│   ├── FocusService.cs         # Window focus tracking service
│   ├── HotkeyService.cs        # Global hotkey service
│   ├── KeyboardService.cs      # Keyboard simulation service
│   ├── LocalizationService.cs  # Multi-language localization service
│   ├── SettingsService.cs      # Settings management service
│   ├── StartupTaskService.cs   # Auto startup service
│   ├── ThemeService.cs         # Theme management service
│   ├── TrayService.cs          # System tray service
│   └── WindowStateService.cs   # Window state management
│
├── Windows/                     # Window layer
│   ├── MainWindow.xaml         # Main window
│   ├── SettingsWindow.xaml     # Settings window
│   ├── EditItemWindow.xaml     # Edit window
│   ├── GroupManageWindow.xaml  # Group management window
│   └── CharPickerWindow.xaml   # Character picker window
│
├── Themes/                      # Theme resources
│   ├── LightTheme.xaml         # Light theme
│   ├── DarkTheme.xaml          # Dark theme
│   └── SharedStyles.xaml       # Shared styles
│
├── Resources/                   # Resource files
│   ├── Characters/             # Special character data
│   └── Languages/              # Multi-language resources
│
└── App.xaml.cs                  # Application entry point
```

### Architecture Design

```
┌─────────────────────────────────────────────────────────────┐
│                      User Interface Layer                   │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │MainWindow│  │Settings  │  │EditItem  │  │GroupMgmt │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        Service Layer                        │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Clipboard     │  │Database      │  │Settings      │      │
│  │Monitor       │  │Service       │  │Service       │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │Hotkey        │  │Theme         │  │Tray          │      │
│  │Service       │  │Service       │  │Service       │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        Data Layer                           │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              SQLite Database                          │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │  │
│  │  │ClipboardItem│  │   Groups    │  │  Settings   │   │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘   │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 Core Implementation

### Clipboard Monitoring

Uses timer polling mechanism to monitor clipboard changes, supports content signature deduplication:

```csharp
// Check clipboard status every 500ms
_timer = new Timer(CheckClipboard, null, 500, 500);
```

### Global Hotkey

Uses Windows API `RegisterHotKey` to register global hotkeys:

```csharp
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

### System Tray

Uses Windows API `Shell_NotifyIcon` to implement system tray functionality:

```csharp
[DllImport("shell32.dll")]
private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);
```

### Theme System

Implements following system theme by listening to registry changes:

```csharp
// Listen for registry key value changes
Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
```

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details

---

## 🙏 Acknowledgments

Thanks to the following AI tools for their help during development:

- **Trae**
- **Doubao** 
- **GLM**
- **Kimi**
- **DeepSeek**

---

<div align="center">

**If this project helps you, please give it a ⭐️ Star!**

Made with ❤️ by [adyhwang](https://github.com/adyhwang)

</div>
