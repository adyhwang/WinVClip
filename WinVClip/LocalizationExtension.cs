using System;
using System.Windows.Markup;
using WinVClip.Services;

namespace WinVClip
{
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }
        public string DefaultValue { get; set; } = "";

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return DefaultValue;

            return LocalizationService.Instance.GetString(Key, DefaultValue);
        }
    }
}
