using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinVClip.Models;
using WinVClip.Services;

namespace WinVClip
{
    public partial class GroupManageWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private List<Group> _groups = new List<Group>();

        public List<Group> Groups => _groups;

        public GroupManageWindow(DatabaseService databaseService)
        {
            InitializeComponent();
            _databaseService = databaseService;
            LoadGroups();
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
            if (string.IsNullOrWhiteSpace(groupName))
            {
                MessageBox.Show("请输入分组名称", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _databaseService.CreateGroup(groupName);
                NewGroupNameTextBox.Clear();
                LoadGroups();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"添加分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Group selectedGroup)
            {
                var inputDialog = new InputDialogWindow
                {
                    Title = "编辑分组",
                    Prompt = "请输入新的分组名称:",
                    DefaultValue = selectedGroup.Name,
                    Owner = this
                };

                if (inputDialog.ShowDialog() == true)
                {
                    string newName = inputDialog.InputValue;
                    if (!string.IsNullOrWhiteSpace(newName) && newName != selectedGroup.Name)
                    {
                        try
                        {
                            _databaseService.UpdateGroup(selectedGroup.Id, newName);
                            LoadGroups();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"更新分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is Group selectedGroup)
            {
                var result = MessageBox.Show(
                    $"确定要删除分组 \"{selectedGroup.Name}\" 吗？",
                    "确认删除",
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
                        MessageBox.Show($"删除分组失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    public class InputDialogWindow : Window
    {
        public string InputValue { get; private set; } = string.Empty;

        public InputDialogWindow()
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Width = 450;
            Height = 200;
            WindowStyle = WindowStyle.SingleBorderWindow;
            
            Loaded += (s, args) =>
            {
                ApplyTheme();
            };
        }

        private void ApplyTheme()
        {
            if (Application.Current.TryFindResource("WindowBackground") is System.Windows.Media.Brush bgBrush)
            {
                Background = bgBrush;
            }
            if (Application.Current.TryFindResource("TextForeground") is System.Windows.Media.Brush fgBrush)
            {
                Foreground = fgBrush;
            }
        }

        public string Prompt { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var mainPanel = new System.Windows.Controls.StackPanel
            {
                Margin = new System.Windows.Thickness(20)
            };

            var promptTextBlock = new System.Windows.Controls.TextBlock
            {
                Text = Prompt,
                Margin = new System.Windows.Thickness(0, 0, 0, 15),
                TextWrapping = System.Windows.TextWrapping.Wrap,
                FontSize = 14
            };
            promptTextBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextForeground");

            var inputTextBox = new System.Windows.Controls.TextBox
            {
                Text = DefaultValue,
                Margin = new System.Windows.Thickness(0, 0, 0, 20),
                Padding = new System.Windows.Thickness(5, 5, 5, 5),
                FontSize = 14,
                Height = 30
            };
            inputTextBox.SetResourceReference(System.Windows.Controls.TextBox.BackgroundProperty, "InputBackground");
            inputTextBox.SetResourceReference(System.Windows.Controls.TextBox.ForegroundProperty, "TextForeground");
            inputTextBox.SetResourceReference(System.Windows.Controls.TextBox.BorderBrushProperty, "InputBorderBrush");

            var buttonPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };

            var okButton = new System.Windows.Controls.Button
            {
                Content = "确定",
                Width = 100,
                Height = 32,
                Margin = new System.Windows.Thickness(0, 0, 10, 0),
                Padding = new System.Windows.Thickness(10, 5, 10, 5),
                FontSize = 14
            };
            okButton.SetResourceReference(System.Windows.Controls.Button.StyleProperty, "PrimaryButtonStyle");

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "取消",
                Width = 100,
                Height = 32,
                Margin = new System.Windows.Thickness(0, 0, 0, 0),
                Padding = new System.Windows.Thickness(10, 5, 10, 5),
                FontSize = 14
            };
            cancelButton.SetResourceReference(System.Windows.Controls.Button.StyleProperty, "SecondaryButtonStyle");

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            mainPanel.Children.Add(promptTextBlock);
            mainPanel.Children.Add(inputTextBox);
            mainPanel.Children.Add(buttonPanel);

            Content = mainPanel;

            okButton.Click += (s, args) =>
            {
                InputValue = inputTextBox.Text.Trim();
                DialogResult = true;
                Close();
            };

            cancelButton.Click += (s, args) =>
            {
                DialogResult = false;
                Close();
            };

            inputTextBox.Focus();
            Loaded += (s, args) => inputTextBox.SelectAll();
        }
    }
}