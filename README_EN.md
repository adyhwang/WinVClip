# WinVClip

<div align="center">

![.NET 10](https://img.shields.io/badge/.NET-10%20LTS-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**A lightweight and powerful Windows clipboard manager**

[Features](#-features) • [Installation](#-installation) • [Usage Guide](#-usage-guide) • [Architecture](#-architecture)

[中文文档](README.md)

</div>

---

## 📖 Introduction

WinVClip is a Windows clipboard manager developed with AI assistance, designed to help users efficiently manage clipboard history. It automatically captures and stores clipboard content, supports multiple data types, and provides convenient search, grouping, and management features.


<img width="388" height="600" alt="1e" src="https://github.com/user-attachments/assets/f13229c4-9188-4be4-ac8d-1ed52efebed8" />

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
| 🎯 **Smart Deduplication** | Optional duplicate content filtering: automatically removes the old record and pins the existing one when duplicates are detected (off by default) |
| 📁 **Group Management** | Organize clipboard items into groups, supports creating, editing, and deleting groups |
| 🔎 **Search Function** | Built-in search engine with keyword highlighting, match-all-keywords, pinyin search, regex/wildcard matching, and search history |
| 🎨 **Theme Switching** | Supports light/dark themes, can automatically follow system settings |
| 🚀 **Auto Startup** | Supports auto-start on boot, ready to use without manual launch |

### Advanced Features

| Feature | Description |
|---------|-------------|
| 🧹 **Auto Cleanup** | Set automatic cleanup of expired history records, supports custom retention days and item limits |
| 🗑️ **Manual Cleanup** | Clear all history, clear ungrouped history, delete duplicate records (keeps newest), delete overlong texts, or clear records older than N days |
| 💾 **Data Backup** | One-click backup/restore of the database (packed as ZIP), plus JSON export/import and plain-text import |
| 📍 **System Tray** | Minimizes to system tray, doesn't occupy taskbar space |
| 🔒 **Data Persistence** | Uses SQLite database for storage, data is safe and reliable |
| 🖥️ **Smart Paste** | Automatically detects terminal environment, intelligently chooses `Ctrl+V` or `Shift+Insert` |
| 📌 **Window Pin** | Supports window always-on-top for convenient comparison operations |
| 🏷️ **Type Filter** | Filter history records by content type (text/image/file/rich text/link); links are precisely recognized via a common domain-suffix whitelist, so ordinary text is never misjudged as a link |
| 🔗 **Quick Actions** | Right-click menu supports quick search, open files, visit links, and more |
| 🚫 **Ignore Windows** | Configure window title/process-name keywords; matching windows are not recorded |
| 🖱️ **Drag & Drop Copy** | Drag items with the left mouse button straight into other apps (text/image/file supported) |
| 📐 **Window Size Memory** | Remembers the main window size and restores it on the next launch |
| 🔤 **Split Characters** | Split text into individual characters/words for selective insertion |
| 🔣 **Character Panel** | Built-in special character panel for quick access to symbols and characters |
| 🔠 **Font Size** | Adjustable interface font size (10-30) |
| ⚙️ **Quick Commands** | Custom text processing rule chains, supporting regex replacement, extraction, formatting, etc. |
| 🎹 **Global Hotkeys** | Custom global keyboard shortcuts to quickly access history records and perform actions |
| 🎹 **Sequential Paste Hotkeys** | Paste clipboard history one by one with repeated key presses, without opening the main UI; supports LIFO/FIFO modes with customizable hotkeys and batch interval |
| 🖱️ **Main UI Shortcuts** | Custom "modifier + mouse" combinations to perform quick actions on clipboard items |
| 🌐 **Multi-language** | Built-in 9 languages: Simplified Chinese, Traditional Chinese, English, Japanese, Korean, Spanish, Russian, French, German; language changes apply instantly without restart |
| ⚡ **Memory Optimization** | Background automatic memory cleanup, auto releases memory when minimized, lower resource usage |

---

## 🖥️ System Requirements

- **Operating System**: Windows 10 1809 or higher
- **Runtime**: Framework-dependent build requires the [.NET 10 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/10.0); 

---

## 📥 Installation

1. Go to the [Releases](https://github.com/adyhwang/WinVClip/releases) page to download the latest version
2. Extract the downloaded archive
3. Double-click to run `WinVClip.exe`

## 📚 Usage Guide

### Hotkeys

| Hotkey | Function |
|--------|----------|
| `Ctrl+Shift+V` | Show/hide main window (default, customizable) |
| `Esc` | Hide main window |
| `Ctrl+Shift+Z` | Sequential Paste - LIFO (default, customizable): press repeatedly to paste records from newest to oldest |
| `Ctrl+Shift+X` | Sequential Paste - FIFO (default, customizable): press repeatedly to paste the latest batch of records in their original copy order |
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

#### Drag & Drop Copy
- Drag any history item with the left mouse button straight into other apps (editors, chat windows, File Explorer, etc.)
- Supports text, rich text, image, and file types; multi-select mode allows batch dragging

#### Edit & Manage
- **Edit Content**: Select "Edit" from context menu; the edit window includes a "Custom Command" rule panel — click "＋ Add Rule" to append text-processing rules, click the lightbulb to import from your quick commands and built-in templates (deduplicated), then click "Execute" to apply the whole rule chain to the text; "Reset" restores the content as it was when the window opened, and cancelling discards unsaved edits
- **Delete Record**: Select "Delete" from context menu; the bottom toolbar supports batch deletion by all/ungrouped/invalid records, and the "Duplicates" button cleans up older duplicate records in one click
- **Batch Operations**: Select "Multi-Select Mode" from context menu, supports batch delete and grouping

#### Search & Filter
- Enter keywords in the search box for quick search; matches are highlighted automatically
- Click "Search Options" to expand: match all keywords, pinyin search, regex, wildcard, keyword highlight, search history
- Search history keeps the latest 10 entries; click one to reuse it instantly
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
4. Edit or delete groups via the management entry in the group tab bar at the top of the main window

### Settings Options

#### General Settings
- **Hotkey**: Customize global hotkey (show/hide main window)
- **Theme**: Light/Dark/Follow System
- **Paste Method**: Auto select, Ctrl+V, Shift+Insert (Auto: uses Shift+Insert in terminals or while holding Ctrl/Alt)
- **Language**: Supports Simplified Chinese, Traditional Chinese, English, Japanese, Korean, Spanish, Russian, French, German; changes apply instantly
- **Font Size**: Adjustable from 10 to 30
- **Administrator Privileges**: Automatically request admin rights on startup (normal privileges cannot auto-paste into elevated windows)
- **Character Panel Button**: Optionally open the system character panel (Win+., requires Windows 10 1709 or later)
- **System Options**: Auto startup, show hotkey tip on startup

#### Global Hotkeys
- **Function**: Custom global keyboard shortcuts to quickly operate without opening the main window
- **Configuration**: Set shortcut combination (Ctrl/Shift/Alt/Win + key), specify which history item (1-N) to act on
- **Supported Actions**: Direct paste, paste as plain text, execute quick command, etc.

#### Sequential Paste
- **Function**: Paste clipboard history records one by one with repeated key presses, without opening the main UI; data is always based on the full history (database)
- **LIFO (Last In, First Out)** (default `Ctrl+Shift+Z`): The first press pastes the newest record; subsequent presses paste progressively older records
- **FIFO (First In, First Out)** (default `Ctrl+Shift+X`): Automatically groups the latest batch of records (adjacent records copied within the configured seconds are treated as one batch) and pastes them in their original copy order — ideal for refilling forms in the original order
- **Interval Seconds** (default 8, minimum 1): If the gap between two presses exceeds the configured seconds, the sequence restarts from the beginning; also used as the time threshold for FIFO batch detection
- **Others**: Wraps around to the beginning after reaching the end; hotkey conflicts are validated instantly, and a tray notification pops up if registration fails

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
- **Capture Switches**: Text, images, and file paths are controlled by separate switches — enable only what you want captured
- **Auto Delete Duplicates**: Automatically remove the old record and pin the newest one when duplicates are detected (off by default)
- **Move to Top After Paste**: After pasting by clicking an item, move that ungrouped record to the top of the list
- **Ignore Windows**: When the foreground window title or process name contains any configured keyword, its clipboard content is not recorded

#### History Records
- **Auto Cleanup**: When enabled, set retention days and maximum record count (0 means unlimited for both); records beyond the limit are removed oldest-first among ungrouped records — grouped records are never auto-deleted
- **Manual Cleanup**: Clear all history, clear ungrouped history, delete duplicate records (keeps the newest of each group, grouped records preserved), delete text/rich text records exceeding a specified character count, or clear records older than N days; the actual number of deleted records is reported, and associated image files are cleaned up with VACUUM run to shrink the database

#### Storage & Backup
- **Data Storage Location**: View or change database path
- **Data Management**:
  - Backup Data: Pack the database and images into a ZIP file
  - Restore Backup: Restore from a ZIP file and automatically restart the app
  - Export JSON: Export all groups and clipboard records to a JSON file
  - Import JSON: Import groups and records from a JSON file, handling duplicates automatically
  - Import Plain Text: Import text records from a TXT file (one record per line)
- **Auto Maintenance**: On startup, automatically truncates the WAL journal file and cleans up orphan image files no longer referenced by the database, preventing continuous disk usage growth

#### Search Options
- **Search Options Card**: Keyword highlight, match all keywords (on by default), search history (on by default), pinyin search (on by default), use regex, use wildcard (regex and wildcard are mutually exclusive); syncs with the main-window search popup
- **Default Search Engine**: The engine used when opening text in the browser (Bing, Baidu, Google, etc.), with support for a custom search URL

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
| **.NET 10 (LTS)** | Runtime environment |
| **SQLite** | Data persistence storage |
| **Windows API** | System-level functionality |

### Core Dependencies

| Package | Version | Usage |
|---------|---------|-------|
| Microsoft.Data.Sqlite | 10.0.0 | SQLite database access |
| QRCoder | 1.8.0 | QR code generation (PngByteQRCode, no GDI+ dependency) |
| SQLitePCLRaw.bundle_e_sqlite3 | 2.1.13 | SQLite native library |

> JSON serialization uses the built-in System.Text.Json Source Generator (compile-time code generation). Only Chinese and English are embedded in the executable; other languages ship as external files that you can add, edit, or remove freely.

### Project Structure

```
WinVClip/
├── Controls/                    # Shared controls
│   ├── HighlightTextBlock.cs   # TextBlock with keyword highlighting
│   ├── NumericUpDown.xaml      # Numeric input control (with step buttons)
│   └── NumericUpDown.xaml.cs
│
├── Models/                      # Data model layer
│   ├── AppSettings.cs          # Application settings model
│   ├── CharGroupData.cs        # Character group data model
│   ├── ClipboardItem.cs        # Clipboard item model
│   ├── ClipboardType.cs        # Clipboard type enum
│   ├── GlobalHotkey.cs         # Global hotkey model
│   ├── Group.cs                # Group model
│   ├── QuickCommand.cs         # Quick command model
│   ├── QuickPasteShortcut.cs   # Main UI shortcut model
│   ├── RangeObservableCollection.cs  # Range observable collection
│   ├── SearchEngine.cs         # Search engine model
│   └── SearchOptions.cs        # Search behavior options
│
├── Services/                    # Service layer
│   ├── AppJsonContext.cs       # JSON serialization Source Generator context
│   ├── ClipboardMonitor.cs     # Clipboard monitoring service
│   ├── ClipboardWin32Helper.cs # Clipboard read/write and loading tip
│   ├── CleanupService.cs       # Auto cleanup service
│   ├── DatabaseService.cs      # Database operation service
│   ├── FileValidationService.cs# File validity validation
│   ├── FocusService.cs         # Window focus tracking service
│   ├── HotkeyService.cs        # Global hotkey service
│   ├── KeyboardService.cs      # Keyboard simulation service
│   ├── LocExtensions.cs        # Table-driven localization extensions
│   ├── LocalizationService.cs  # Multi-language localization service
│   ├── PinyinService.cs        # Pinyin index generation
│   ├── SettingsService.cs      # Settings management service
│   ├── StartupTaskService.cs   # Auto startup service
│   ├── ThemeService.cs         # Theme management service
│   ├── TrayService.cs          # System tray service
│   └── WindowStateService.cs   # Window state management
│
├── Themes/                      # Theme resources
│   ├── LightTheme.xaml         # Light theme
│   ├── DarkTheme.xaml          # Dark theme
│   └── SharedStyles.xaml       # Shared styles
│
├── Resources/                   # Resource files
│   ├── Characters/             # Special character data
│   └── Languages/              # Multi-language resources (9 languages)
│
├── MainWindow.xaml(.cs)         # Main window
├── SettingsWindow.xaml(.cs)     # Settings window
├── EditItemWindow.xaml(.cs)     # Edit window
├── GroupManageWindow.xaml(.cs)  # Group management window
├── CharPickerWindow.xaml(.cs)   # Character picker window
├── QRCodeWindow.xaml(.cs)       # QR code window
├── LoadingTipWindow.xaml(.cs)   # Clipboard loading tip window
├── AppMessageBox.xaml(.cs)      # Unified message dialog
├── App.xaml(.cs)                # Application entry point
└── WinVClip.csproj              # Project file
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

Uses the Windows API `AddClipboardFormatListener` to subscribe to the `WM_CLIPBOARDUPDATE` message, with debouncing and content-signature deduplication:

```csharp
[DllImport("user32.dll", SetLastError = true)]
private static extern bool AddClipboardFormatListener(IntPtr hwnd);
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


<div align="center">

**If this project helps you, please give it a ⭐️ Star!**

Made with ❤️ by [adyhwang](https://github.com/adyhwang)

</div>
