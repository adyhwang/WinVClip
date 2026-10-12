using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WinVClip.Controls
{
    public partial class NumericUpDown : UserControl
    {
        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValuePropertyChanged, CoerceValue));

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0, OnRangePropertyChanged));

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(100, OnRangePropertyChanged));

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step), typeof(int), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(1));

        public event RoutedPropertyChangedEventHandler<int> ValueChanged;

        public int Value
        {
            get => (int)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Minimum
        {
            get => (int)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public int Maximum
        {
            get => (int)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public int Step
        {
            get => (int)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public NumericUpDown()
        {
            InitializeComponent();
            UpdateText();
        }

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var c = (NumericUpDown)d;
            var v = (int)baseValue;
            if (v < c.Minimum) v = c.Minimum;
            if (v > c.Maximum) v = c.Maximum;
            return v;
        }

        private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (NumericUpDown)d;
            c.UpdateText();
            c.ValueChanged?.Invoke(c, new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue));
        }

        private static void OnRangePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var c = (NumericUpDown)d;
            c.CoerceValue(ValueProperty);
        }

        private void UpdateText()
        {
            // 输入过程中不打断用户编辑
            if (ValueTextBox.IsFocused && !string.IsNullOrWhiteSpace(ValueTextBox.Text))
                return;
            ValueTextBox.Text = Value.ToString();
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            var step = Step > 0 ? Step : 1;
            Value = Math.Min(Maximum, Value + step);
        }

        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            var step = Step > 0 ? Step : 1;
            Value = Math.Max(Minimum, Value - step);
        }

        private void ValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitText();
        }

        private void ValueTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitText();
                e.Handled = true;
            }
        }

        private void CommitText()
        {
            if (int.TryParse(ValueTextBox.Text.Trim(), out var v))
            {
                Value = v;
            }
            ValueTextBox.Text = Value.ToString();
        }
    }
}
