using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace Roco.EventArgs
{
    public class PropertyValueChangedEventArgs : PropertyChangedEventArgs
    {
        public object Before { get; set; }
        public object After { get; set; }

        public PropertyValueChangedEventArgs(string propertyName, object before, object after) :
            base(propertyName)
        {
            Before = before;
            After = after;
        }
    }
}
