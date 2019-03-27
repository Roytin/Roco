using System;
using System.Collections.Generic;
using System.Linq;
using Roco;

namespace RocoTest
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            string connectionString = @"localhost:6379,defaultDatabase=1";
         
            var csredis = new CSRedis.CSRedisClient(connectionString);
            
            RocoClient client = new RocoClient(csredis);
            string blogId = "aaa";
            Blog blog = new Blog(blogId)
            {
                Name = "第一号",
                Url = "hhh",
                Star = 99,
                AliveTime = TimeSpan.FromHours(8),
                CreateTime = DateTime.UtcNow,
                IsPrivate = true,

                CharValue = 'x',
                DoubleValue = 0.1234,
                SingleValue = 0.4321f,

                Posts = new List<string>() { "aaa", "bbb", "ccc" },

                Address = new Address()
                {
                    City = "Ningbo",
                    Country = "China",
                    Number = 10,
                    Town = "YinZhou"
                }
            };
            
            bool ok = client.Insert(blog);
            if (!ok)
            {
                blog = client.Query<Blog>(blogId);
                client.Delete(blog);
            }
            Blog blog2 = client.Query<Blog>(blogId);
            var blogs3 = client.Index<Blog>(x => x.Name, blog.Name).ToList();

            Console.WriteLine("over");
            Console.ReadLine();
        }
    }
}
