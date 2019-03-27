using System;
using System.Collections.Generic;
using System.Text;

namespace Roco
{
    public class RedisOptions
    {
        public string ServerAddress { get; set; }
        public string Password { get; set; }
        public int Db { get; set; }
    }
}
