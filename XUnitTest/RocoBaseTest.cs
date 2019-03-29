using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection.Metadata;
using System.Security.Cryptography.X509Certificates;
using Roco;
using Xunit;

namespace XUnitTest
{
    public class RocoBaseTest
    {
        private RocoClient _roco;

        public RocoBaseTest()
        {
            string connectionString = @"localhost:6379,defaultDatabase=2";
            var redis = new CSRedis.CSRedisClient(connectionString);
            redis.NodesServerManager.FlushDb();
            _roco = new RocoClient(redis);
            
            var person1 = new Person("001")
            {
                Name = "一号",
                Star = '5',
                AliveTime = TimeSpan.FromHours(8),
                CreateTime = DateTime.UtcNow,
                Gender = Gender.Woman,

                CharValue = 'x',
                DoubleValue = 0.1234,
                SingleValue = 0.4321f,

                Posts = new List<string>() { "aaa", "bbb", "ccc" },

                Address = new Address()
                {
                    City = "NingBo",
                    Country = "China",
                    Number = 10,
                    Town = "下应"
                }
            };
            _roco.Insert(person1);
            var person2 = new Person("002")
            {
                Name = "二号",
                Star = '6',
                AliveTime = TimeSpan.FromHours(8),
                CreateTime = DateTime.UtcNow,
                Gender = Gender.Woman,

                CharValue = 'x',
                DoubleValue = 0.1234,
                SingleValue = 0.4321f,

                Posts = new List<string>() { "aaa", "bbb", "ccc" },

                Address = new Address()
                {
                    City = "NingBo",
                    Country = "China",
                    Number = 10,
                    Town = "下应"
                }
            };
            _roco.Insert(person2);

            var person3 = new Person("003")
            {
                Name = "三号",
                Star = '8',
                AliveTime = TimeSpan.FromHours(8),
                CreateTime = DateTime.UtcNow,
                Gender = Gender.Woman,

                CharValue = 'x',
                DoubleValue = 0.1234,
                SingleValue = 0.4321f,

                Posts = new List<string>() { "aaa", "bbb", "ccc" },

                Address = new Address()
                {
                    City = "NingBo",
                    Country = "China",
                    Number = 10,
                    Town = "下应"
                }
            };
            _roco.Insert(person3);
        }

        [Fact]
        public void InsertTest()
        {
            var person = new Person(Guid.NewGuid().ToString("N"))
            {
                Name = DateTime.Now.Ticks.ToString(),
                Star = 'a',
                AliveTime = TimeSpan.FromHours(8),
                CreateTime = DateTime.UtcNow,
                Gender = Gender.Man,

                CharValue = 'x',
                DoubleValue = 0.1234,
                SingleValue = 0.4321f,

                Posts = new List<string>() { "aaa", "bbb", "ccc" },

                Address = new Address()
                {
                    City = "NingBo",
                    Country = "China",
                    Number = 10,
                    Town = "下应"
                }
            };
            bool ok = _roco.Insert(person);
            Assert.True(ok);
        }

        [Fact]
        public void QueryTest()
        {
            var person = _roco.Query<Person>("001");
            Assert.NotNull(person);
            Assert.Equal("一号", person.Name);
        }

        [Fact]
        public void UniqueTest()
        {
            var person = _roco.Unique<Person, string>(x=>x.Name, "一号");
            Assert.NotNull(person);
            Assert.Equal("一号", person.Name);
        }


        [Fact]
        public void IndexTest()
        {
            var persons = _roco.Index<Person, Gender>(x => x.Gender, Gender.Woman).ToList();
            Assert.NotNull(persons);
            Assert.True(persons.Any());
            Assert.True(persons.All(x=>x.Gender == Gender.Woman));
        }

        [Fact]
        public void DeleteTest()
        {
            var person2 = _roco.Query<Person>("002");
            Assert.True(person2 != null);
            _roco.Delete(person2);
            person2 = _roco.Query<Person>("002");
            Assert.Null(person2);
        }


        [Fact]
        public void UpdateTest()
        {
            var person3 = _roco.Query<Person>("003");
            Assert.True(person3 != null);
            person3.Name = "改名三号";
            person3.Gender = Gender.Unknown;
            person3.CharValue = '3';
            //bool ok = _roco.Update(person3);
            //Assert.True(ok);
            var ps = _roco.Index<Person, Gender>(x => x.Gender, Gender.Unknown).ToList();
            Assert.True(ps.Any());
            Assert.True(ps.FirstOrDefault(x=>x.Id == "003") != null);

            var p = _roco.Unique<Person, string>(x => x.Name, "改名三号");
            Assert.NotNull(p);
        }


        [Fact]
        public void SortTest()
        {
            var ps = _roco.Range<Person, double>(p => p.Star, 0, -1).ToList();

            Assert.True(ps.Count == 3);
            Assert.True(ps[0].Star <= ps[1].Star && ps[1].Star<= ps[2].Star);
        }
    }
}
