using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class GroupManageWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private List<Group> _groups = new List<Group>();

        public List<Group> Groups => _groups;

        public string EditButtonTooltip { get; private set; } = "";
        public string DeleteButtonTooltip { get; private set; } = "";

        public GroupManageWindow(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            ApplyLocalization();
            LoadGroups();
            DataContext = this;
        }

        private void ApplyLocalization()
        {
            Title = Loc.Get("GroupManage.Title", "管理分组");
            TitleText.Text = Loc.Get("GroupManage.Title", "管理分组");
            AddButton.Content = Loc.Get("GroupManage.AddGroup", "添加分组");
            EditButtonTooltip = Loc.Get("GroupManage.Edit", "编辑");
            DeleteButtonTooltip = Loc.Get("GroupManage.Delete", "删除");
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            // 确保窗口在主窗口位置弹出
            if (Owner != null)
            {
                Left = Owner.Left;
                Top = Owner.Top;
            }
        }

        private void LoadGroups()
        {
            _groups = _databaseService.GetAllGroups();
            GroupsListBox.ItemsSource = _groups;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            string groupName = NewGroupNameTextBox.Text.Trim();
            string groupIcon = NewGroupIconTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show(Loc.Get("Message.GroupNameEmpty", "请输入分组名称"), Loc.Get("Common.OK", "提示"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _databaseService.CreateGroup(groupName, string.IsNullOrEmpty(groupIcon) ? "🌟" : groupIcon);
                NewGroupNameTextBox.Clear();
                NewGroupIconTextBox.Text = "🌟";
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Loc.Get("Message.AddGroupFailed", "添加分组失败: {0}"), ex.Message), Loc.Get("Common.OK", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Group selectedGroup)
            {
                var editPanel = new StackPanel { Margin = new Thickness(20) };

                var nameLabel = new TextBlock
                {
                    Text = Loc.Get("GroupManage.EnterNewName", "请输入新的分组名称:"),
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 14
                };
                nameLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");

                var nameTextBox = new TextBox
                {
                    Text = selectedGroup.Name,
                    Margin = new Thickness(0, 0, 0, 16),
                    Padding = new Thickness(5),
                    FontSize = 14,
                    Height = 30
                };
                nameTextBox.SetResourceReference(TextBox.BackgroundProperty, "InputBackground");
                nameTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextForeground");
                nameTextBox.SetResourceReference(TextBox.BorderBrushProperty, "InputBorderBrush");

                var iconLabel = new TextBlock
                {
                    Text = Loc.Get("GroupManage.EnterIcon", "图标 (最多2个字符):"),
                    Margin = new Thickness(0, 0, 0, 8),
                    FontSize = 14
                };
                iconLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextForeground");

                var iconTextBox = new TextBox
                {
                    Text = selectedGroup.Icon ?? "⭐",
                    Margin = new Thickness(0, 0, 0, 16),
                    Padding = new Thickness(5),
                    FontSize = 14,
                    Height = 30,
                    MaxLength = 2,
                    TextAlignment = TextAlignment.Center,
                    Width = 60,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                iconTextBox.SetResourceReference(TextBox.BackgroundProperty, "InputBackground");
                iconTextBox.SetResourceReference(TextBox.ForegroundProperty, "TextForeground");
                iconTextBox.SetResourceReference(TextBox.BorderBrushProperty, "InputBorderBrush");

                editPanel.Children.Add(nameLabel);
                editPanel.Children.Add(nameTextBox);
                editPanel.Children.Add(iconLabel);
                editPanel.Children.Add(iconTextBox);

                var dialog = new Window
                {
                    Title = Loc.Get("GroupManage.EditGroup", "编辑分组"),
                    Content = editPanel,
                    Width = 450,
                    Height = 280,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    Owner = this,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.SingleBorderWindow
                };
                dialog.SetResourceReference(Window.BackgroundProperty, "WindowBackground");

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 8, 0, 0)
                };

                var okButton = new Button
                {
                    Content = Loc.Get("Common.OK", "确定"),
                    Width = 100,
                    Height = 32,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                okButton.SetResourceReference(Button.StyleProperty, "PrimaryButtonStyle");

                var cancelButton = new Button
                {
                    Content = Loc.Get("Common.Cancel", "取消"),
                    Width = 100,
                    Height = 32
                };
                cancelButton.SetResourceReference(Button.StyleProperty, "SecondaryButtonStyle");

                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);
                editPanel.Children.Add(buttonPanel);

                okButton.Click += (s, args) =>
                {
                    string newName = nameTextBox.Text.Trim();
                    string newIcon = iconTextBox.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        try
                        {
                            _databaseService.UpdateGroup(selectedGroup.Id, newName, string.IsNullOrEmpty(newIcon) ? "⭐" : newIcon);
                            LoadGroups();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(string.Format(Loc.Get("Message.UpdateGroupFailed", "更新分组失败: {0}"), ex.Message), Loc.Get("Common.OK", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    dialog.Close();
                };

                cancelButton.Click += (s, args) => dialog.Close();

                dialog.Loaded += (s, args) => nameTextBox.Focus();
                dialog.ShowDialog();
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Group selectedGroup)
            {
                var result = MessageBox.Show(
                    string.Format(Loc.Get("GroupManage.ConfirmDelete", "确定要删除分组 \"{0}\" 吗？"), selectedGroup.Name),
                    Loc.Get("Common.Delete", "确认删除"),
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        _databaseService.DeleteGroup(selectedGroup.Id);
                        LoadGroups();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(string.Format(Loc.Get("Message.DeleteGroupFailed", "删除分组失败: {0}"), ex.Message), Loc.Get("Common.OK", "错误"), MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void NewGroupNameTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                AddButton_Click(sender, e);
            }
        }

        private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }

    public class DefaultGroupVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string name && name == "Favorite")
                return Visibility.Collapsed;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}