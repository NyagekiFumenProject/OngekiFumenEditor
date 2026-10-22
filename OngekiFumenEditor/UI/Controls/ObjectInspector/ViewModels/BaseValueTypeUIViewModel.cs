using OngekiFumenEditor.UI.Controls.ObjectInspector.UIGenerator;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows;

namespace OngekiFumenEditor.UI.Controls.ObjectInspector.ViewModels
{
    public class BaseValueTypeUIViewModel : CommonUIViewModelBase
    {
        public BaseValueTypeUIViewModel(IObjectPropertyAccessProxy wrapper) : base(wrapper)
        {
        }

        /// <summary>
        /// 数值属性按固定小数位显示，避免 float/double 的二进制误差
        /// （例如 1.2000000476837158）直接出现在属性编辑器里。
        /// </summary>
        public string ProxyValueText
        {
            get => FormatProxyValue();
            set => ApplyProxyValueText(value);
        }

        private Type ValueType
            => Nullable.GetUnderlyingType(PropertyInfo.PropertyInfo?.PropertyType ?? typeof(object))
               ?? PropertyInfo.PropertyInfo?.PropertyType
               ?? typeof(object);

        private bool IsNullableValue
            => Nullable.GetUnderlyingType(PropertyInfo.PropertyInfo?.PropertyType ?? typeof(object)) is not null
               || !ValueType.IsValueType;

        private string FormatProxyValue()
        {
            var value = PropertyInfo.ProxyValue;
            if (value is null || ReferenceEquals(value, DependencyProperty.UnsetValue))
                return string.Empty;

            try
            {
                if (ValueType == typeof(double))
                    return Convert.ToDouble(value, CultureInfo.CurrentCulture).ToString("0.######", CultureInfo.CurrentCulture);
                if (ValueType == typeof(float))
                    return Convert.ToSingle(value, CultureInfo.CurrentCulture).ToString("0.######", CultureInfo.CurrentCulture);
                if (ValueType == typeof(decimal))
                    return Convert.ToDecimal(value, CultureInfo.CurrentCulture).ToString("0.######", CultureInfo.CurrentCulture);
            }
            catch
            {
                // 代理值与属性类型不一致时保留原始文本，避免属性面板崩溃。
            }

            return Convert.ToString(value, CultureInfo.CurrentCulture) ?? string.Empty;
        }

        private void ApplyProxyValueText(string text)
        {
            text = text?.Trim() ?? string.Empty;

            if (ValueType == typeof(string))
            {
                PropertyInfo.ProxyValue = text;
                return;
            }

            if (text.Length == 0)
            {
                if (IsNullableValue)
                    PropertyInfo.ProxyValue = null;
                else
                    NotifyOfPropertyChange(nameof(ProxyValueText));
                return;
            }

            object parsed;
            try
            {
                if (ValueType == typeof(double))
                {
                    if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
                        { NotifyOfPropertyChange(nameof(ProxyValueText)); return; }
                    parsed = value;
                }
                else if (ValueType == typeof(float))
                {
                    if (!float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
                        { NotifyOfPropertyChange(nameof(ProxyValueText)); return; }
                    parsed = value;
                }
                else if (ValueType == typeof(decimal))
                {
                    if (!decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
                        { NotifyOfPropertyChange(nameof(ProxyValueText)); return; }
                    parsed = value;
                }
                else if (ValueType == typeof(int))
                {
                    if (!int.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
                        { NotifyOfPropertyChange(nameof(ProxyValueText)); return; }
                    parsed = value;
                }
                else if (ValueType == typeof(long))
                {
                    if (!long.TryParse(text, NumberStyles.Integer | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var value))
                        { NotifyOfPropertyChange(nameof(ProxyValueText)); return; }
                    parsed = value;
                }
                else
                {
                    var converter = TypeDescriptor.GetConverter(ValueType);
                    if (!converter.CanConvertFrom(typeof(string)))
                        return;
                    parsed = converter.ConvertFromString(null, CultureInfo.CurrentCulture, text);
                }
            }
            catch
            {
                NotifyOfPropertyChange(nameof(ProxyValueText));
                return;
            }

            PropertyInfo.ProxyValue = parsed;
        }

        protected override void PropertyInfo_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.PropertyInfo_PropertyChanged(sender, e);

            if (e.PropertyName == nameof(IObjectPropertyAccessProxy.ProxyValue)
                || e.PropertyName == PropertyInfo.PropertyInfo?.Name)
                NotifyOfPropertyChange(nameof(ProxyValueText));
        }
    }
}
