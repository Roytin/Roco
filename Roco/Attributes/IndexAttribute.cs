using System;
using System.Collections.Generic;
using System.Text;

namespace Roco.Attributes
{
    /// <summary>
    /// 标记索引属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IndexAttribute : Attribute
    {
        /// <summary>
        /// 是否唯一
        /// </summary>
        public bool IsUnique { get; set; }
    }

    /// <summary>
    /// 标记排序属性（也具有索引能力）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class SortableAttribute : Attribute
    {
    }
}
