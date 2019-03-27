using System;
using System.Collections.Generic;
using System.Text;

namespace Roco.Attributes
{
    public class IndexAttribute : Attribute
    {
        public bool IsUnique { get; set; }
    }
}
