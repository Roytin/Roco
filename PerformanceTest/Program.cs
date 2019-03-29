using System;
using System.Collections.Generic;
using System.Diagnostics;
using Roco;
using TestModel;

namespace PerformanceTest
{
    class Program
    {
        static RocoClient roco;
        static void Main(string[] args)
        {
            Person p = new Person("1");
            string connectionString = @"localhost:6379,defaultDatabase=2";
            var redis = new CSRedis.CSRedisClient(connectionString);
            redis.NodesServerManager.FlushDb();
            roco = new RocoClient(redis);

            TestInsert();

            Console.ReadLine();
        }

        static void TestInsert()
        {
            double N = 10000;
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < N; i++)
            {
                var person = new Person(Guid.NewGuid().ToString("N"))
                {
                    Name = $"{i}号",
                    Star = i % 8,
                    AliveTime = TimeSpan.FromHours(8),
                    CreateTime = DateTime.UtcNow,
                    Gender = Gender.Woman,

                    CharValue = 'x',
                    DoubleValue = 0.1234,
                    SingleValue = 0.4321f,

                    Posts = new List<string>() {"aaa", "bbb", "ccc"},
                    Address = new Address()
                    {
                        City = "NingBo",
                        Country = "China",
                        Number = 10,
                        Town = "下应"
                    }
                };
                roco.Insert(person);
            }
            sw.Stop();
            Console.WriteLine($"插入{N}条，耗时{sw.ElapsedMilliseconds},平均{sw.ElapsedMilliseconds/N}");
        }
    }
}
