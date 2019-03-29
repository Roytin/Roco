using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AspectCore.Extensions.Reflection;
using Newtonsoft.Json;
using Roco.Attributes;

namespace Roco
{
    //interface IRocoProperty
    //{
    //    Type Type { get; }
    //    string Name { get; }
    //    void SetValue(object instance, object value);
    //    object GetValue(object instance);
    //}

    class RocoProperty //:IRocoProperty
    {
        public RocoScheme Scheme { get; private set; }
        public string Name { get; private set; }
        public Type Type { get; private set; }
        public bool IsIndex { get; private set; }
        public bool IsUnique { get; private set; }
        public bool IsSortable { get; }
        
        private PropertyReflector Property { get; set; }

        public RocoProperty(PropertyInfo propertyInfo, RocoScheme scheme)
        {
            Scheme = scheme;
            this.Property = propertyInfo.GetReflector();
            this.Type = propertyInfo.PropertyType;
            this.Name = propertyInfo.Name;

            var indexAttribute = this.Property.GetCustomAttribute<IndexAttribute>();
            if (indexAttribute != null)
            {
                IsIndex = true;
                if (indexAttribute.IsUnique)
                    IsUnique = true;
            }

            var sortAttribute = this.Property.GetCustomAttribute<SortableAttribute>();
            if (sortAttribute != null)
            {
                if(!Type.IsValueType)
                    throw new Exception("排序属性必须是值类型");
                int size = Marshal.SizeOf(Type);
                //检查属性类型是否可以排序
                var maxSize = sizeof(double);
                if (size > maxSize)
                {
                    throw new Exception("不能排序比double更大size的类型");
                }
                IsSortable = true;
            }
        }

        private static readonly Dictionary<Type, Action<PropertyReflector, object, string>> ActionSetValueByType = 
            new Dictionary<Type, Action<PropertyReflector, object, string>>
        {
            { typeof(bool), (p, instance, o) => p.SetValue(instance,     o =="1" || o =="true" || o =="True" || o =="TRUE") },
            { typeof(char), (p, instance, o) => p.SetValue(instance,     Convert.ToChar(o)) },
            { typeof(sbyte), (p, instance, o) => p.SetValue(instance,    Convert.ToSByte(o)) },
            { typeof(byte), (p, instance, o) => p.SetValue(instance,     Convert.ToByte(o)) },
            { typeof(Int16), (p, instance, o) => p.SetValue(instance,    Convert.ToInt16(o)) },
            { typeof(UInt16), (p, instance, o) => p.SetValue(instance,   Convert.ToUInt16(o)) },
            { typeof(Int32), (p, instance, o) => p.SetValue(instance,    Convert.ToInt32(o)) },
            { typeof(UInt32), (p, instance, o) => p.SetValue(instance,   Convert.ToUInt32(o)) },
            { typeof(Int64), (p, instance, o) => p.SetValue(instance,    Convert.ToInt64(o)) },
            { typeof(UInt64), (p, instance, o) => p.SetValue(instance,   Convert.ToUInt64(o)) },
            { typeof(Single), (p, instance, o) => p.SetValue(instance,   Convert.ToSingle(o)) },
            { typeof(Double), (p, instance, o) => p.SetValue(instance,   Convert.ToDouble(o)) },
            { typeof(decimal), (p, instance, o) => p.SetValue(instance,  Convert.ToDecimal(o)) },
            { typeof(TimeSpan), (p, instance, o) => p.SetValue(instance, TimeSpan.FromTicks(Convert.ToInt64(o))) },
            { typeof(DateTime), (p, instance, o) => p.SetValue(instance, Convert.ToDateTime(o)) },
            { typeof(string), (p, instance, o) => p.SetValue(instance,   o) },
        };
        
        public void SetValue(RocoBase instance, object value)
        {
            if (ActionSetValueByType.TryGetValue(Type, out var action))
            {
                action.Invoke(Property, instance, value as string);
            }
            else
            {
                //todo 在未来加入递归面向对象二维展开
                Property.SetValue(instance, JsonConvert.DeserializeObject(value as string, Type));
            }
        }

        public object GetValue(RocoBase instance)
        {
            return Property.GetValue(instance);
        }


        public string GenerateIndexKey(object indexValue)
        {
            if (!this.IsIndex)
                throw new Exception("不可使用非索引特性属性来进行索引查询");
            return $"{this.Scheme.Type.Name}:_X:{this.Name}:{indexValue}";
        }

        public string GenerateSortableKey()
        {
            return $"{this.Scheme.Type.Name}:_S:{this.Name}";
        }
    }
}
