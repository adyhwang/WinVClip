# WinVClip

<div align="center">

![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)
![License](https://img.shields.io/badge/license-MIT-green.svg)

**一款轻量级、功能强大的 Windows 剪贴板管理工具**

[功能特性](#功能特性) • [安装使用](#安装使用) • [使用指南](#使用指南) • [技术架构](#技术架构)

[English](README_EN.md)

</div>

---

## 📖 简介

WinVClip 是一款使用 AI 辅助开发的 Windows 剪贴板管理工具，旨在帮助用户高效管理剪贴板历史记录。它能够自动捕获和存储剪贴板内容，支持多种数据类型，并提供便捷的搜索、分组和管理功能。

<img width="350" height="600" alt="主界面" src="https://github.com/user-attachments/assets/2765372b-064d-4c72-8169-239d3b979ae3" />
<img width="197" height="164" alt="托盘菜单" src="https://github.com/user-attachments/assets/ef7bd558-4887-4497-a8f3-503d843ea7db" />
<img width="750" height="620" alt="设置窗口" src="https://github.com/user-attachments/assets/ccbc0794-56ea-4308-ba49-69b3a396300b" />
<img width="600" height="500" alt="5" src="https://github.com/user-attachments/assets/562efe07-c768-463d-9085-95fa47f96ecb" />

---

## ✨ 功能特性

### 核心功能

| 功能 | 描述 |
|------|------|
| 🔍 **多类型支持** | 支持文本、图片、文件列表、富文本（RTF/HTML）等多种剪贴板内容类型 |
| 📋 **历史记录** | 自动保存剪贴板历史，支持无限条目存储，随时回溯和复用 |
| ⌨️ **快捷键操作** | 全局快捷键快速唤出主界面，默认 `Ctrl+Shift+V`，支持自定义 |
| 🎯 **智能去重** | 自动检测并过滤重复内容，避免历史记录冗余 |
| 📁 **分组管理** | 将剪贴板项分组整理，支持创建、编辑、删除分组 |
| 🔎 **搜索功能** | 内置搜索引擎，快速检索剪贴板内容 |
| 🎨 **主题切换** | 支持亮色/暗色主题，可跟随系统自动切换 |
| 🚀 **开机自启** | 支持开机自动启动，开机即用无需手动打开 |

### 高级功能

| 功能 | 描述 |
|------|------|
| 🧹 **自动删除** | 可设置自动删除过期历史记录，支持自定义保留天数和条目上限 |
| 🗑️ **手动删除** | 删除所有历史、删除未分组历史、删除指定天数前的历史记录 |
| 💾 **数据备份** | 自动备份数据库，支持自定义备份频率和保留数量 |
| 📍 **系统托盘** | 最小化到系统托盘，不占用任务栏空间 |
| 🔒 **数据持久化** | 使用 SQLite 数据库存储，数据安全可靠 |
| 🖥️ **智能粘贴** | 自动识别终端环境，智能选择 `Ctrl+V` 或 `Shift+Insert` |
| 📌 **窗口置顶** | 支持窗口置顶功能，方便对照操作 |
| 🏷️ **类型筛选** | 按内容类型（文本/图片/文件/富文本/链接）筛选历史记录 |
| 🔗 **快捷操作** | 右键菜单支持快速搜索、打开文件、访问链接等操作 |
| 🔤 **拆分选字** | 将文本拆分为单字/单词，选择性插入所需内容 |
| 😀 **表情面板** | 内置表情面板，支持分类浏览和快速插入 |
| 🔣 **字符面板** | 内置特殊字符面板，快速访问符号和字符 |
| 🔠 **字体大小** | 可调整界面字体大小（10-30），主界面和拆分选字界面即时生效 |
| 🌐 **多语言** | 支持中英文切换，语言修改即时生效无需重启 |
| ⚡ **内存优化** | 后台自动内存回收，最小化后自动释放内存，资源占用更低 |

---

## 🖥️ 系统要求

- **操作系统**: Windows 7 SP1 或更高版本
- **运行时**: .NET Framework 4.8 或更高版本

---

## 📥 安装使用

### 方式一：直接运行

1. 前往 [Releases](https://github.com/adyhwang/WinVClip/releases) 页面下载最新版本
2. 解压下载的压缩包
3. 双击运行 `WinVClip.exe`

### 方式二：从源码编译

```bash
# 克隆仓库
git clone https://github.com/adyhwang/WinVClip.git
cd WinVClip
```

使用 Visual Studio 编译：

1. 打开 `WinVClip.sln` 解决方案
2. 选择 `Release` 配置
3. 点击 `生成` → `生成解决方案`
4. 编译输出位于 `WinVClip\bin\Release\net48\` 目录

---

## 📚 使用指南

### 快捷键

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Shift+V` | 显示/隐藏主界面（默认，可自定义） |
| `Esc` | 隐藏主界面 |
| `Right Click` | 打开右键菜单 |

### 基本操作

#### 复制与捕获
- 正常使用 `Ctrl+C` 复制内容，WinVClip 会自动捕获
- 支持捕获文本、图片、文件、富文本等多种类型

#### 查看与粘贴
- 按下快捷键或点击托盘图标打开主界面
- 单击历史记录项即可粘贴到当前焦点位置

#### 编辑与管理
- **编辑内容**: 右键菜单选择"编辑"
- **删除记录**: 右键菜单选择"删除"
- **批量操作**: 右键菜单选择"多选模式"，支持批量删除和分组

#### 搜索与筛选
- 在搜索框输入关键词快速查找
- 点击筛选按钮按类型或分组过滤

#### 拆分选字
- 右键点击文本或富文本条目，选择"拆分选字"
- 文本会被拆分为单个汉字或英文单词
- 点击或拖动选择需要插入的字符
- 选中的字符会按顺序拼接在文本区
- 点击"插入"按钮粘贴选中的内容

### 分组管理

1. 点击主界面右键菜单 → "添加到分组"
2. 选择已有分组或创建新分组
3. 通过分组筛选快速定位内容
4. 在设置 → 分组管理中编辑或删除分组

### 设置选项

#### 常规设置
- **快捷键**: 自定义全局快捷键
- **主题**: 亮色/暗色/跟随系统
- **粘贴方式**: 自动选择、Ctrl+V、Shift+Insert
- **语言**: 中英文切换，修改即时生效
- **字体大小**: 可调整范围 10-30，主界面和拆分选字界面即时生效
- **开机自启**: 开机自动启动程序
- **窗口置顶**: 主窗口始终显示在最前方

#### 捕获设置
- **监控开关**: 启用/禁用剪贴板监控
- **捕获类型**: 选择要捕获的内容类型

#### 历史记录
- **去重设置**: 开启/关闭重复内容过滤
- **自动删除**: 设置保留天数和最大条目数
- **手动删除**: 删除所有历史、删除未分组历史、删除指定天数前的历史记录

#### 存储与备份
- **数据库位置**: 查看或更改数据库路径
- **备份设置**: 设置备份频率和保留数量

#### 搜索引擎
- 选择默认搜索引擎（Bing、百度、Google 等）
- 支持添加自定义搜索引擎

---

## 🏗️ 技术架构

### 技术栈

| 技术 | 用途 |
|------|------|
| **WPF** | 用户界面框架 |
| **.NET Framework 4.8** | 运行时环境 |
| **SQLite** | 数据持久化存储 |
| **Windows API** | 系统级功能实现 |

### 核心依赖

| 包 | 版本 | 用途 |
|-----|------|------|
| Microsoft.Data.Sqlite | 6.0.31 | SQLite 数据库访问 |
| System.Text.Json | 6.0.9 | JSON 序列化 |
| System.Drawing.Common | 6.0.0 | 图像处理 |

### 项目结构

```
WinVClip/
├── Models/                      # 数据模型层
│   ├── AppSettings.cs          # 应用程序设置模型
│   ├── CharGroupData.cs        # 字符分组数据模型
│   ├── ClipboardItem.cs        # 剪贴板条目模型
│   ├── ClipboardType.cs        # 剪贴板类型枚举
│   ├── Group.cs                # 分组模型
│   ├── LanguageModel.cs        # 语言模型
│   ├── RangeObservableCollection.cs  # 范围可观察集合
│   └── SearchEngine.cs         # 搜索引擎模型
│
├── Services/                    # 服务层
│   ├── BackupService.cs        # 数据备份服务
│   ├── CleanupService.cs       # 自动清理服务
│   ├── ClipboardMonitor.cs     # 剪贴板监控服务
│   ├── DatabaseService.cs      # 数据库操作服务
│   ├── FocusService.cs         # 窗口焦点追踪服务
│   ├── HotkeyService.cs        # 全局快捷键服务
│   ├── KeyboardService.cs      # 键盘模拟服务
│   ├── LocalizationService.cs  # 多语言本地化服务
│   ├── SettingsService.cs      # 设置管理服务
│   ├── StartupTaskService.cs   # 开机自启服务
│   ├── ThemeService.cs         # 主题管理服务
│   ├── TrayService.cs          # 系统托盘服务
│   └── WindowStateService.cs   # 窗口状态管理
│
├── Windows/                     # 窗口层
│   ├── MainWindow.xaml         # 主窗口
│   ├── SettingsWindow.xaml     # 设置窗口
│   ├── EditItemWindow.xaml     # 编辑窗口
│   ├── GroupManageWindow.xaml  # 分组管理窗口
│   └── CharPickerWindow.xaml   # 拆分选字窗口
│
├── Themes/                      # 主题资源
│   ├── LightTheme.xaml         # 亮色主题
│   ├── DarkTheme.xaml          # 暗色主题
│   └── SharedStyles.xaml       # 共享样式
│
├── Resources/                   # 资源文件
│   ├── Characters/             # 特殊字符数据
│   ├── Emoji/                  # 表情数据
│   └── Languages/              # 多语言资源
│
└── App.xaml.cs                  # 应用程序入口
```

### 架构设计

```
┌─────────────────────────────────────────────────────────────┐
│                        用户界面层                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐    │
│  │MainWindow│  │Settings  │  │EditItem  │  │GroupMgmt │    │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                        服务层                                │
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
│                        数据层                                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │              SQLite Database                          │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐   │  │
│  │  │ClipboardItem│  │   Groups    │  │  Settings   │   │  │
│  │  └─────────────┘  └─────────────┘  └─────────────┘   │  │
│  └──────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 🔧 核心实现

### 剪贴板监控

使用定时器轮询机制监控剪贴板变化，支持内容签名去重：

```csharp
// 每 500ms 检查剪贴板状态
_timer = new Timer(CheckClipboard, null, 500, 500);
```

### 全局快捷键

使用 Windows API `RegisterHotKey` 注册全局快捷键：

```csharp
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

### 系统托盘

使用 Windows API `Shell_NotifyIcon` 实现系统托盘功能：

```csharp
[DllImport("shell32.dll")]
private static extern bool Shell_NotifyIcon(uint dwMessage, ref NotifyIconData data);
```

### 主题系统

通过监听注册表变化实现跟随系统主题：

```csharp
// 监听注册表键值变化
Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
```

---


## 📄 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

---

## 🙏 致谢

感谢以下 AI 工具在开发过程中提供的帮助：

- **Trae** 
- **Doubao** 
- **GLM** 
- **Kimi** 
- **DeepSeek** 

---

<div align="center">

**如果这个项目对你有帮助，请给一个 ⭐️ Star！**

Made with ❤️ by [adyhwang](https://github.com/adyhwang)

</div>
