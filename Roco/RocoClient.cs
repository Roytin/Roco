using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using AspectCore.Extensions.Reflection;
using CSRedis;
using Roco.EventArgs;

namespace Roco
{
    public class RocoClient
    {
        private static readonly ConcurrentDictionary<Type, RocoScheme> Schemes = new ConcurrentDictionary<Type, RocoScheme>();
        static RocoClient()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string name = assembly.GetName().Name;
                if (name != "TesModel")
                {
                    Console.WriteLine(name);
                    continue;
                }
                foreach (var type in assembly.DefinedTypes)
                {
                    if (!type.IsSubclassOf(typeof(RocoBase)))
                        continue;
                    var scheme = new RocoScheme(type);
                    Schemes[type] = scheme;
                }
            }
        }

        private readonly CSRedisClient _redis;

        public RocoClient(CSRedisClient redis)
        {
            _redis = redis;
        }

        /// <summary>
        /// 查询实例
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="id"></param>
        /// <param name="isTracking">是否实时追踪实例变化</param>
        /// <returns></returns>
        public T Query<T>(string id, bool isTracking = true)
            where T : RocoBase
        {
            var type = typeof(T);
            var ps = Schemes[type];
            var entity = ps.CreateInstance(id) as T;
            if (entity == null)
                return null;
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
                    property.SetValue(entity, value);
                    entity.PropertyCache[property.Name] = value;//cache
                }
            }
            //todo 考虑是否需要做结构校验？

            if(isTracking)
                this.Tracking(entity);
            return entity;
        }

        /// <summary>
        /// 插入操作
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        /// <param name="isTracking">是否实时追踪实例变化</param>
        /// <returns>插入成功True, 失败False表明Id或唯一索引有重复</returns>
        public bool Insert<T>(T entity, bool isTracking = true)
            where T : RocoBase
        {
            var type = entity.GetType();
            var ps = Schemes.GetOrAdd(type, t => new RocoScheme(t));
            string key = ps.GenerateKey(entity.Id);
            //检查key是否已存在
            if (_redis.Exists(key))
                return false;

            //基础数据表
            object[] baseProperties = new object[2* ps.Properties.Count];
            //索引表
            List<(string key, bool isUnique)> indexProperties = new List<(string key, bool isUnique)>();
            //排序表
            List<(string key, double score)> sortProperties = new List<(string key, double score)>();
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
                        indexProperties.Add((indexSetKey, true));
                    }
                    else
                    {
                        indexProperties.Add((indexSetKey, false));
                    }
                }

                if (property.IsSortable)
                {
                    string sortSetKey = property.GenerateSortableKey();
                    sortProperties.Add((sortSetKey,Convert.ToDouble(value)));
                }
                i++;
            }

            var pipe = _redis.StartPipe();
            foreach (var indexProperty in indexProperties)
            {
                if (indexProperty.isUnique)
                    pipe.Set(indexProperty.key, entity.Id);
                else
                    pipe.SAdd(indexProperty.key, entity.Id);
            }

            foreach (var sortProperty in sortProperties)
            {
                pipe.ZAdd(sortProperty.key, (sortProperty.score, entity.Id));
            }
            pipe.HMSet(key, baseProperties);
            var result = pipe.EndPipe();
            if(isTracking)
                this.Tracking(entity);

            for (i = 0; i < baseProperties.Length/2; i+=2)
            {
                var name = baseProperties[i * 2] as string;
                var value = baseProperties[i * 2 + 1];
                entity.PropertyCache[name] = value;
            }
            return true;
        }

        /// <summary>
        /// 追踪实例更新
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity">实例</param>
        public void Tracking<T>(T entity)
            where T : RocoBase
        {
            if (!entity.IsTracking)
            {
                entity.IsTracking = true;
                entity.PropertyChanged += Entity_PropertyChanged;
            }
        }

        /// <summary>
        /// 停止实例追踪
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="entity"></param>
        public void NoTracking<T>(T entity)
            where T : RocoBase
        {
            if (entity.IsTracking)
            {
                entity.IsTracking = false;
                entity.PropertyChanged -= Entity_PropertyChanged;
                //if (_changedProperties.TryRemove(entity, out var dict))
                //{
                //    dict.Clear();
                //}
            }
        }

        private void Entity_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            var entity = sender as RocoBase;
            if (entity == null)
                return;
            var argNew = e as PropertyValueChangedEventArgs;
            if (argNew == null)
                return;
            this.Update(entity, argNew.PropertyName, argNew.Before, argNew.After);
        }

        /// <summary>
        /// 删除实例
        /// </summary>
        public void Delete<T>(T entity)
            where T : RocoBase
        {
            var ps = Schemes[typeof(T)];
            string key = ps.GenerateKey(entity.Id);

            var pipe = _redis.StartPipe()
                .Del(key);
            //索引表
            foreach (var property in ps.Properties.Values)
            {
                var value = property.GetValue(entity);
                if (property.IsIndex)
                {
                    string indexSetKey = property.GenerateIndexKey(value);
                    if (property.IsUnique)
                    {
                        pipe.Del(indexSetKey);
                    }
                    else
                    {
                        pipe.SRem(indexSetKey, entity.Id);
                    }
                }

                if (property.IsSortable)
                {
                    string sorSetKey = property.GenerateSortableKey();
                    pipe.ZRem(sorSetKey, entity.Id);
                }
            }

            var result = pipe.EndPipe();
            this.NoTracking(entity);
        }

        public bool Update<T, TProperty>(T entity, Expression<Func<T, TProperty>> indexPropertyExpression)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(indexPropertyExpression);
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string key = property.GenerateIndexKey(entity.Id);
            var oldValue = entity.PropertyCache[propertyName];
            var newValue = property.GetValue(entity);
            if (this.Update(key, entity.Id, property, oldValue, newValue))
            {
                entity.PropertyCache[propertyName] = newValue;
                return true;
            }
            return true;
        }

        private bool Update(RocoBase entity, string propertyName, object oldValue, object newValue)
        {
            var ps = Schemes[entity.GetType()];
            string key = ps.GenerateKey(entity.Id);
            var property = ps.Properties[propertyName];
            if (this.Update(key, entity.Id, property, oldValue, newValue))
            {
                entity.PropertyCache[propertyName] = newValue;
                return true;
            }
            return false;
        }

        private bool Update(string key, string entityId, RocoProperty property, object oldValue, object newValue)
        {
            if (property.IsIndex)
            {
                string indexSetKeyOld = property.GenerateIndexKey(oldValue);
                string indexSetKeyNew = property.GenerateIndexKey(newValue);
                if (property.IsUnique)
                {
                    //检查唯一索引是否重复
                    if (_redis.Exists(indexSetKeyNew))
                        return false;
                    _redis.Del(indexSetKeyOld);
                    _redis.Set(indexSetKeyNew, entityId);
                }
                else
                {
                    _redis.SRem(indexSetKeyOld, entityId);
                    _redis.SAdd(indexSetKeyNew, entityId);
                }
            }

            if (property.IsSortable)
            {
                string sortSetKey = property.GenerateSortableKey();
                _redis.ZAdd(sortSetKey, ((double)newValue, entityId));
            }
            _redis.HSet(key, property.Name, newValue);
            return true;
        }

        /// <summary>
        /// 获得指定属性排序后的实例Id集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyExpression"></param>
        /// <param name="start">起始序号</param>
        /// <param name="stop">结束序号，支持负数倒序</param>
        /// <param name="isDescending">是否降序排列</param>
        public IEnumerable<string> RangeOfId<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression, int start, int stop, bool isDescending = false)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(propertyExpression);
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string sortSetKey = property.GenerateSortableKey();
            string[] ids;
            if(isDescending)
                ids = _redis.ZRevRange(sortSetKey, start, stop);
            else
                ids = _redis.ZRange(sortSetKey, start, stop);
            return ids;
        }

        /// <summary>
        /// 获得按指定属性排序后的实例集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="propertyExpression"></param>
        /// <param name="start">起始序号</param>
        /// <param name="stop">结束序号，支持负数倒序</param>
        /// <param name="isDescending">是否降序排列</param>
        /// <returns></returns>
        public IEnumerable<T> Range<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression, int start, int stop, bool isDescending = false)
            where T : RocoBase
        {
            foreach (var entityId in RangeOfId(propertyExpression, start, stop, isDescending))
            {
                yield return this.Query<T>(entityId);
            }
        }

        /// <summary>
        /// 获得指定属性排序后的实例Id集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        public IEnumerable<string> RangeByScoreOfId<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression, double min, double max, bool isDescending = false)
            where T : RocoBase
            where TProperty : struct
        {
            string propertyName = ExpressionUtils.GetPropertyName(propertyExpression);
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string sortSetKey = property.GenerateSortableKey();
            string[] ids;
            if (isDescending)
                ids = _redis.ZRevRangeByScore(sortSetKey, min, max);
            else
                ids = _redis.ZRangeByScore(sortSetKey, min, max);
            return ids;
        }

        /// <summary>
        /// 获得按指定属性排序后的实例集合
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <returns></returns>
        public IEnumerable<T> RangeByScore<T, TProperty>(Expression<Func<T, TProperty>> propertyExpression, double min, double max, bool isDescending = false)
            where T : RocoBase
            where TProperty : struct
        {
            foreach (var entityId in RangeByScoreOfId(propertyExpression, min, max, isDescending))
            {
                yield return this.Query<T>(entityId);
            }
        }

        /// <summary>
        /// 获得指定属性在所有实例中的排名
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="entity"></param>
        /// <param name="propertyExpression"></param>
        /// <param name="isDescending">是否降序</param>
        /// <returns></returns>
        public long? Rank<T, TProperty>(T entity, Expression<Func<T, TProperty>> propertyExpression, bool isDescending = false)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(propertyExpression);
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string sortSetKey = property.GenerateSortableKey();
            if (isDescending)
                return _redis.ZRevRank(sortSetKey, entity.Id);
            return _redis.ZRank(sortSetKey, entity.Id);
        }

        /// <summary>
        /// 获取两值之间的成员数量
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="entity"></param>
        /// <param name="propertyExpression"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public long Count<T, TProperty>(T entity, Expression<Func<T, double>> propertyExpression, double min, double max)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(propertyExpression);
            var ps = Schemes[typeof(T)];
            var property = ps.Properties[propertyName];
            string sortSetKey = property.GenerateSortableKey();
            return _redis.ZCount(sortSetKey, min, max);
        }
        
        ///// <summary>
        ///// 将更新缓存同步到Redis中
        ///// 只有成功才会清空缓存，失败情况下请修改唯一索引后重试
        ///// </summary>
        ///// <returns>成功true, 主键/唯一索引重复false</returns>
        //public bool FlushUpdateTrackingCache()
        //    where T : RocoBase
        //{
        //    var cache = entity.UpdateCache.Values.Where(x => x.After != x.Before).ToList();
        //    if (cache.Count == 0)
        //        return true;
        //    var ps = Schemes[typeof(T)];
        //    string key = ps.GenerateKey(entity.Id);

        //    //基础数据表
        //    object[] baseProperties = new object[2 * cache.Count];
        //    //索引表
        //    List<(string oldKey, string newKey, object obj, bool isUnique)> indexProperties = new List<(string oldKey, string newKey, object obj, bool isUnique)>();
        //    int i = 0;
        //    foreach (var propertyCache in cache)
        //    {
        //        var property = ps.Properties[propertyCache.PropertyName];
        //        var value = propertyCache.After;
        //        baseProperties[i * 2] = property.Name;
        //        baseProperties[i * 2 + 1] = value;

        //        if (property.IsIndex)
        //        {
        //            string indexSetKeyOld = property.GenerateIndexKey(propertyCache.Before);
        //            string indexSetKeyNew = property.GenerateIndexKey(propertyCache.After);
        //            if (property.IsUnique)
        //            {
        //                //检查唯一索引是否重复
        //                if (_redis.Exists(indexSetKeyNew))
        //                    return false;
        //                indexProperties.Add((indexSetKeyOld, indexSetKeyNew, entity.Id, true));
        //            }
        //            else
        //            {
        //                indexProperties.Add((indexSetKeyOld, indexSetKeyNew, entity.Id, false));
        //            }
        //        }
        //        i++;
        //    }

        //    var pipe = _redis.StartPipe()
        //        .HMSet(key, baseProperties);
        //    foreach (var indexProperty in indexProperties)
        //    {
        //        if (indexProperty.isUnique)
        //        {
        //            pipe.Del(indexProperty.oldKey);
        //            pipe.Set(indexProperty.newKey, indexProperty.obj);
        //        }
        //        else
        //        {
        //            pipe.SRem(indexProperty.oldKey, indexProperty.obj);
        //            pipe.SAdd(indexProperty.newKey, indexProperty.obj);
        //        }
        //    }
        //    pipe.EndPipe();
        //    entity.UpdateCache.Clear();
        //    return true;
        //}

        /// <summary>
        /// 通过索引查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="indexPropertyExpression">选取索引属性</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns>实例Id的集合</returns>
        public IEnumerable<string> IndexOfId<T, TProperty>(Expression<Func<T, TProperty>> indexPropertyExpression, TProperty indexValue)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(indexPropertyExpression);
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
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="indexPropertyExpression">选取索引属性</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns>实例集合</returns>
        public IEnumerable<T> Index<T, TProperty>(Expression<Func<T, TProperty>> indexPropertyExpression, TProperty indexValue)
            where T : RocoBase
        {
            foreach (string entityId in this.IndexOfId(indexPropertyExpression, indexValue))
            {
                yield return this.Query<T>(entityId);
            }
        }

        /// <summary>
        /// 通过索引查询第一个
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="indexPropertyExpression">选取索引属性</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns>实例</returns>
        public T FirstOrDefault<T, TProperty>(Expression<Func<T, TProperty>> indexPropertyExpression, TProperty indexValue)
            where T : RocoBase
        {
            var entityId = this.IndexOfId(indexPropertyExpression, indexValue).FirstOrDefault();
            if (entityId == null)
                return null;
            return this.Query<T>(entityId);
        }

        /// <summary>
        /// 通过唯一索引查询
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TProperty"></typeparam>
        /// <param name="indexPropertyExpression">选取唯一索引</param>
        /// <param name="indexValue">索引的值</param>
        /// <returns></returns>
        public T Unique<T, TProperty>(Expression<Func<T, TProperty>> indexPropertyExpression, object indexValue)
            where T : RocoBase
        {
            string propertyName = ExpressionUtils.GetPropertyName(indexPropertyExpression);
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
