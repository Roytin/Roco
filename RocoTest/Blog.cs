using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Roco;
using Roco.Attributes;

namespace RocoTest
{
    public class Blog : RocoBase
    {
        public Blog(string id)
        {
            Id = id;
        }

        public override string Id { get; }
        [Index(IsUnique = true)]
        public string Name { get; set; }
        public string Url { get; set; }


        public int Star { get; set; }
        public DateTime CreateTime { get; set; }
        public TimeSpan AliveTime { get; set; }

        [Index]
        public bool IsPrivate { get; set; }

        public float SingleValue { get; set; }
        public double DoubleValue { get; set; }
        public char CharValue { get; set; }
        //public List<Post> Posts { get; set; }

        public Address Address { get; set; }

        public List<string> Posts { get; set; }
    }

    //public class Post
    //{
    //    public int PostId { get; set; }
    //    public string Title { get; set; }
    //    public string Content { get; set; }
    //    public int BlogId { get; set; }
    //    public Blog Blog { get; set; }
    //}


    public class Address
    {
        public string City { get; set; }
        public string Country { get; set; }
        public string Town { get; set; }
        public int Number { get; set; }
    }
}
