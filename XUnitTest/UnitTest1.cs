using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Roco;
using Xunit;

namespace XUnitTest
{
    public class UnitTest1
    {
        private RocoClient _roco;
        
        public UnitTest1()
        {
            string connectionString = @"localhost:6379,defaultDatabase=1";
            var redis = new CSRedis.CSRedisClient(connectionString);
            _roco = new RocoClient(redis);
        }

        [Fact]
        public void InsertTest()
        {
            string id = Guid.Empty.ToString();
            var person = new Person(id)
            {
                Name = "ÄÐÒ»ºÅ",
                Star = 99,
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
                    Town = "ÏÂÓ¦"
                }
            };
            bool ok = _roco.Insert(person);
            Assert.True(ok);
            Assert.Equal(person.Id, id);
        }

        [Fact]
        public void QueryTest()
        {
            string id = Guid.Empty.ToString();
            var person = _roco.Query<Person>(id);


        }

    }
}
