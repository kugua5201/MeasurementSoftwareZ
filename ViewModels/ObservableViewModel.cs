using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Runtime.CompilerServices;

namespace MeasurementSoftware.ViewModels
{
    /// <summary>
    /// 一个继承自 ObservableObject 的基类
    /// </summary>
    public abstract class ObservableViewModel : ObservableObject
    {
        /// <summary>
        /// 比较属性的当前值和新值。如果值已更改，则更新属性、调用回调，然后引发 PropertyChanged 事件。
        /// </summary>
        /// <typeparam name="T">属性的类型。</typeparam>
        /// <param name="field">存储属性值的字段。</param>
        /// <param name="newValue">属性的新值。</param>
        /// <param name="onChanged">如果属性值已更改，则要调用的回调操作。</param>
        /// <param name="propertyName">已更改的属性的名称。</param>
        /// <returns>如果属性值已更改，则为 true；否则为 false。</returns>
        protected bool SetProperty<T>(ref T field, T newValue, Action? onChanged = null, [CallerMemberName] string? propertyName = null)
        {
            if (base.SetProperty(ref field, newValue, propertyName))
            {
                onChanged?.Invoke();
                return true;
            }
            return false;
        }

       
    }
}
