using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using AspectCore.Extensions.Reflection;

namespace Roco
{
    class RocoScheme
    {
        public Type Type { get; set; }
        public Dictionary<string, RocoProperty> Properties { get; } = new Dictionary<string, RocoProperty>();

        private readonly ConstructorReflector _constructorReflector;
        public RocoScheme(Type type)
        {
            Type = type;

            foreach (var propertyInfo in type.GetProperties())
            {
                var rocoProperty = new RocoProperty(propertyInfo, this);
                Properties[propertyInfo.Name] = rocoProperty;
            }

            var constructorInfo = Type.GetTypeInfo().GetConstructor(new[] { typeof(string) });
            _constructorReflector = constructorInfo.GetReflector();
        }

        public object CreateInstance(string entityId)
        {
            return _constructorReflector.Invoke(entityId);
        }

        public string GenerateKey(string entityId)
        {
            return $"{Type.Name}:_T:{entityId}";
        }

        //public string GenerateKey<T>(T entity)
        //    where T :RocoBase
        //{
        //    string id = Properties["Id"].GetValue(entity) as string;
        //    return this.GenerateKey(id);
        //}
    }
}
