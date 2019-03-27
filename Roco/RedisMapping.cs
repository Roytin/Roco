using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AspectCore.Extensions.Reflection;
using Newtonsoft.Json;
using Roco.Attributes;

namespace Roco
{
    enum RocoTypes
    {
        Empty,
        Object,
        Boolean,
        Char,
        SByte,
        Byte,
        Int16,
        UInt16,
        Int32,
        UInt32,
        Int64,
        UInt64,
        Single,
        Double,
        Decimal,
        TimeSpan,
        DateTime,
        String
    }

    class RocoScheme
    {
        public Type Type { get; set; }
        public Dictionary<string, RocoProperty> Properties { get; set; }

        private readonly ConstructorReflector _constructorReflector;
        public RocoScheme(Type type)
        {
            Type = type;
            Properties = type
                .GetProperties()
                .Select(x=> new RocoProperty(x, this))
                .ToDictionary(x=>x.Name, x=> x);

            var constructorInfo = Type.GetTypeInfo().GetConstructor(new[] { typeof(string) });
            _constructorReflector = constructorInfo.GetReflector();
        }

        public object CreateInstance(string entityId)
        {
            return _constructorReflector.Invoke(entityId);
        }

        public string GenerateKey(string entityId)
        {
            return $"{Type.Name}:{entityId}";
        }

        //public string GenerateKey<T>(T entity)
        //    where T :RocoBase
        //{
        //    string id = Properties["Id"].GetValue(entity) as string;
        //    return this.GenerateKey(id);
        //}
    }


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
        
        public void SetValue(object instance, object value)
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

        public object GetValue(object instance)
        {
            return Property.GetValue(instance);
        }


        public string GenerateIndexKey(object indexValue)
        {
            if (!this.IsIndex)
                throw new Exception("不可使用非索引特性属性来进行索引查询");
            return $"{this.Scheme.Type.Name}:Index:{this.Name}:{indexValue}";
        }
    }
}
