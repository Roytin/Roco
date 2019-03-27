using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AspectCore.Extensions.Reflection;
using CSRedis;

namespace Roco
{
    public class RocoClient
    {
        private static readonly Dictionary<Type, RocoScheme> Schemes;

        static RocoClient()
        {
            try
            {
                var types = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(x => x.ExportedTypes)
                    .Where(type => type.IsSubclassOf(typeof(RocoBase)));
                Schemes = types
                    .Select(type=> new RocoScheme(type))
                    .ToDictionary(x => x.Type, x => x);
            }
            catch (Exception ex)
            {
                throw new Exception("类名/属性名重复", ex);
            }
        }

        private readonly CSRedisClient _redis;

        public RocoClient(CSRedisClient redis)
        {
            _redis = redis;
        }

        public T Query<T>(string id)
            where T : RocoBase
        {
            var type = typeof(T);
            var ps = Schemes[type];
            var instance = ps.CreateInstance(id);
            //赋值
            var data = _redis.HGetAll(ps.GenerateKey(id));
            if (data.Count == 0)
                return null;
            foreach (var property in ps.Properties.Values)
            {
                if (property.Name == "Id")
                    continue;
                if (data.TryGetValue(property.Name, out var value))
                {
                    property.SetValue(instance, value);
                }
            }
            return instance as T;
        }

        /// <summary>
        /// 插入操作
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns>插入成功True, 失败False表明Id或唯一索引有重复</returns>
        public bool Insert<T>(T entity)
            where T : RocoBase
        {
            var ps = Schemes[typeof(T)];
            string key = ps.GenerateKey(entity.Id);
            //检查key是否已存在
            if (_redis.Exists(key))
                return false;

            //基础数据表
            object[] baseProperties = new object[2* ps.Properties.Count];
            //索引表
            List<(string key, object obj, bool isUnique)> indexProperties = new List<(string key, object obj, bool isUnique)>();
            int i = 0;
            foreach (var property in ps.Properties.Values)
            {
                var value = property.GetValue(entity);
                baseProperties[i * 2] = property.Name;
                baseProperties[i * 2 + 1] = value;

                if (property.IsIndex)
                {
                    string indexSetKey = property.GenerateIndexKey(value);
                    if (property.IsUnique)
                    {
                        //检查唯一索引是否重复
                        if (_redis.Exists(indexSetKey))
                            return false;
                        indexProperties.Add((indexSetKey, entity.Id, true));
                    }
                    else
                    {
                        indexProperties.Add((indexSetKey, entity.Id, false));
                    }
                }
                i++;
            }

            var pipe = _redis.StartPipe()
                .HMSet(key, baseProperties);
            foreach (var indexProperty in indexProperties)
            {
                if (indexProperty.isUnique)
                    pipe.Set(indexProperty.key, indexProperty.obj);
                else
                    pipe.SAdd(indexProperty.key, indexProperty.obj);
            }

            var result = pipe.EndPipe();
            entity.UpdateCache.Clear();
            return true;
        }
        
        /// <summary>
        /// 删除实例
        /// </summary>
        public void Delete<T>(T entity)
            where T : RocoBase
        {
            var ps = Schemes[typeof(T)];
            string key = ps.GenerateKey(entity.Id);

            //索引表
            List<(string key, object obj, bool isUnique)> indexProperties = new List<(string key, object obj, bool isUnique)>();
            int i = 0;
            foreach (var property in ps.Properties.Values)
            {
                var value = property.GetValue(entity);
                if (property.IsIndex)
                {
                    string indexSetKey = property.GenerateIndexKey(value);
                    if (property.IsUnique)
                    {
                        //检查唯一索引是否重复
                        indexProperties.Add((indexSetKey, entity.Id, true));
                    }
                    else
                    {
                        indexProperties.Add((indexSetKey, entity.Id, false));
                    }
                }
                i++;
            }

            var pipe = _redis.StartPipe()
                .Del(key);
            foreach (var indexProperty in indexProperties)
            {
                if (indexProperty.isUnique)
                    pipe.Del(indexProperty.key);
                else
                    pipe.SRem(indexProperty.key, indexProperty.obj);
            }
            var result = pipe.EndPipe();
            entity.UpdateCache.Clear();
        }

        /// <summary>
        /// 将更新缓存同步到Redis中
        /// 只有成功才会清空缓存，失败情况下请修改唯一索引后重试
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <returns>成功true, 主键/唯一索引重复false</returns>
        public bool FlushUpdate<T>(T entity)
            where T : RocoBase
        {
            var ps = Schemes[typeof(T)];
            string key = ps.GenerateKey(entity.Id);

            //基础数据表
            object[] baseProperties = new object[2 * ps.Properties.Count];
            //索引表
            List<(string oldKey, string newKey, object obj, bool isUnique)> indexProperties = new List<(string oldKey, string newKey, object obj, bool isUnique)>();
            int i = 0;
            foreach (var propertyCache in entity.UpdateCache.Values)
            {
                if (propertyCache.After == propertyCache.Before)
                    continue;
                var property = ps.Properties[propertyCache.PropertyName];
                var value = propertyCache.After;
                baseProperties[i * 2] = property.Name;
                baseProperties[i * 2 + 1] = value;

                if (property.IsIndex)
                {
                    string indexSetKeyOld = property.GenerateIndexKey(propertyCache.Before);
                    string indexSetKeyNew = property.GenerateIndexKey(propertyCache.After);
                    if (property.IsUnique)
                    {
                        //检查唯一索引是否重复
                        if (_redis.Exists(indexSetKeyNew))
                            return false;
                        indexProperties.Add((indexSetKeyOld, indexSetKeyNew, entity.Id, true));
                    }
                    else
                    {
                        indexProperties.Add((indexSetKeyOld, indexSetKeyNew, entity.Id, false));
                    }
                }
                i++;
            }

            var pipe = _redis.StartPipe()
                .HMSet(key, baseProperties);
            foreach (var indexProperty in indexProperties)
            {
                if (indexProperty.isUnique)
                {
                    pipe.Del(indexProperty.oldKey);
                    pipe.Set(indexProperty.newKey, indexProperty.obj);
                }
                else
                {
                    pipe.SRem(indexProperty.oldKey, indexProperty.obj);
                    pipe.SAdd(indexProperty.newKey, indexProperty.obj);
                }
            }
            entity.UpdateCache.Clear();
            return true;
        }

        /// <summary>
        /// 通过索引查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="indexPropertyExpression">选取索引属性</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns>实例Id的集合</returns>
        public IEnumerable<string> IndexOfId<T>(Expression<Func<T, string>> indexPropertyExpression, object indexValue)
            where T : RocoBase
        {
            var expression = (MemberExpression)indexPropertyExpression.Body;
            string propertyName = expression.Member.Name;
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string key = property.GenerateIndexKey(indexValue);
            if (property.IsUnique)
            {
                string entityId = _redis.Get(key);
                if (entityId != null)
                    yield return entityId;
            }
            else
            {
                var entityIds = _redis.SMembers(key);
                foreach (string entityId in entityIds)
                {
                    yield return entityId;
                }
            }
        }

        /// <summary>
        /// 通过索引查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="indexPropertyExpression">选取索引属性</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns>实例集合</returns>
        public IEnumerable<T> Index<T>(Expression<Func<T, string>> indexPropertyExpression, object indexValue)
            where T : RocoBase
        {
            foreach (string entityId in this.IndexOfId(indexPropertyExpression, indexValue))
            {
                yield return this.Query<T>(entityId);
            }
        }

        /// <summary>
        /// 通过唯一索引查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="indexPropertyExpression">选取唯一索引</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns></returns>
        public T Unique<T>(Expression<Func<T, string>> indexPropertyExpression, object indexValue)
            where T : RocoBase
        {
            var expression = (MemberExpression)indexPropertyExpression.Body;
            string propertyName = expression.Member.Name;
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string key = property.GenerateIndexKey(indexValue);
            if (!property.IsUnique)
            {
                throw new Exception($"{typeof(T).Name}的属性{propertyName}不是唯一索引");
            }
            string entityId = _redis.Get(key);
            if (entityId != null)
                return this.Query<T>(entityId);
            return null;
        }

        ///// <summary>
        ///// 根据属性值查看当前排名
        ///// </summary>
        //public int Rank<T>(T entity, Expression<Func<T, string>> propertyExpression)
        //{
        //    var expression = (MemberExpression)propertyExpression.Body;
        //    string propertyName = expression.Member.Name;
        //    return default(T);
        //}

        //public T Range<T>()
        //{
        //    return default(T);
        //}

    }
}
