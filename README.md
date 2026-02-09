# WinVClip

一款用AI制作的功能强大、轻量级的 Windows 剪贴板管理工具，帮助你高效管理剪贴板历史记录。
<img width="350" height="600" alt="1" src="https://github.com/user-attachments/assets/2765372b-064d-4c72-8169-239d3b979ae3" />
<img width="197" height="164" alt="2" src="https://github.com/user-attachments/assets/ef7bd558-4887-4497-a8f3-503d843ea7db" />
<img width="750" height="620" alt="3" src="https://github.com/user-attachments/assets/ccbc0794-56ea-4308-ba49-69b3a396300b" />


## 功能特性

- **多类型支持** - 支持文本、图片、文件列表和超链接的剪贴板内容捕获
- **历史记录** - 自动保存剪贴板历史，方便随时回溯和复用
- **快捷键操作** - 自定义全局快捷键，快速唤出主界面
- **智能去重** - 自动过滤重复内容，避免历史记录冗余
- **分组管理** - 将剪贴板项分组整理，提高查找效率
- **搜索功能** - 内置搜索引擎，快速搜索剪贴板内容
- **主题切换** - 支持亮色/暗色主题，跟随系统自动切换
- **自动清理** - 可设置自动清理过期历史记录
- **系统托盘** - 最小化到系统托盘，不占用任务栏空间
- **数据持久化** - 使用 SQLite 数据库存储，数据安全可靠

## 系统要求

- Windows 7 或更高版本
- .NET Framework 4.8 或更高版本

## 安装使用

### 从源码编译

1. 克隆仓库：
```bash
git clone https://github.com/adyhwang/WinVClip/
cd WinVClip
```

2. 使用 Visual Studio 打开 `WinVClip.sln` 解决方案

3. 选择 Release 配置，点击生成解决方案

4. 在 `WinVClip\bin\Release\net48\` 目录下找到编译好的 `WinVClip.exe`

### 直接运行

下载最新的 Release 版本，解压后直接运行 `WinVClip.exe` 即可。

## 使用说明

### 快捷键

- 默认快捷键：`Ctrl + Shift + V`
- 可在设置中自定义快捷键

### 基本操作

- **复制内容** - 正常使用 Ctrl+C 复制内容，WinVClip 会自动捕获
- **查看历史** - 按下快捷键或点击托盘图标打开主界面
- **粘贴内容** - 单击历史记录项即可粘贴到当前焦点位置
- **删除记录** - 右键记录后选择菜单删除
- **批量操作** - 历史记录右键“多选模式”，进入批量操作，单击记录选择记录，右键菜单选择批量分组和批量删除
- **搜索内容** - 在搜索框输入关键词快速查找

### 分组管理

1. 点击"分组管理"按钮
2. 创建新分组或编辑现有分组
3. 将剪贴板项拖拽到对应分组

### 设置选项

- **监控开关** - 启用/禁用剪贴板监控
- **捕获设置** - 选择要捕获的内容类型（文本/链接/图片/文件）
- **去重设置** - 开启/关闭重复内容过滤
- **清理设置** - 设置自动清理天数和历史记录上限
- **主题设置** - 选择亮色/暗色/自动主题
- **搜索引擎** - 选择默认搜索引擎或添加自定义搜索引擎

## 技术栈

- **开发框架** - WPF (.NET Framework 4.8)
- **数据库** - SQLite (Microsoft.Data.Sqlite)
- **UI 主题** - 自定义 XAML 主题系统
- **快捷键** - Windows API (RegisterHotKey)
- **系统托盘** - Windows API (Shell_NotifyIcon)

## 项目结构

```
WinVClip/
├── Models/              # 数据模型
│   ├── AppSettings.cs   # 应用设置
│   ├── ClipboardItem.cs # 剪贴板项
│   ├── ClipboardType.cs # 剪贴板类型
│   ├── Group.cs         # 分组
│   └── SearchEngine.cs  # 搜索引擎
├── Services/            # 服务层
│   ├── BackupService.cs # 备份服务
│   ├── CleanupService.cs # 清理服务
│   ├── ClipboardMonitor.cs # 剪贴板监控
│   ├── DatabaseService.cs # 数据库服务
│   ├── HotkeyService.cs # 快捷键服务
│   ├── SettingsService.cs # 设置服务
│   ├── ThemeService.cs  # 主题服务
│   └── TrayService.cs   # 托盘服务
├── Themes/              # 主题资源
│   ├── DarkTheme.xaml   # 暗色主题
│   ├── LightTheme.xaml  # 亮色主题
│   └── SharedStyles.xaml # 共享样式
└── Windows/             # 窗口
    ├── MainWindow.xaml # 主窗口
    ├── SettingsWindow.xaml # 设置窗口
    ├── EditItemWindow.xaml # 编辑窗口
    └── GroupManageWindow.xaml # 分组管理窗口
```


## 致谢

感谢Trae/GLM/Kimi/DeepSeek等等
