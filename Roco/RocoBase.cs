using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Roco.EventArgs;

namespace Roco
{
    /// <summary>
    /// 非线程安全，不可并行操作
    /// </summary>
    public abstract class RocoBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName, object before, object after)
        {

            if (UpdateCache.TryGetValue(propertyName, out var args))
            {
                args.After = after;
            }
            else
            {
                args = new PropertyValueChangedEventArgs(propertyName, before, after);
                UpdateCache.Add(args.PropertyName, args);
            }

            PropertyChanged?.Invoke(this, args);
        }

        public abstract string Id { get; }

        internal readonly Dictionary<string, PropertyValueChangedEventArgs> UpdateCache = new Dictionary<string, PropertyValueChangedEventArgs>();
    }
}
