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

        protected void OnPropertyChanged(string propertyName, object before, object after)
        {
            PropertyChanged?.Invoke(this, new PropertyValueChangedEventArgs(propertyName, before, after));
        }

        public abstract string Id { get; }

        internal bool IsTracking;
        internal readonly Dictionary<string, object> PropertyCache = new Dictionary<string, object>();
    }
}
