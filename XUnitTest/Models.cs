using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using Roco;
using Roco.Attributes;

namespace XUnitTest
{
    public class Person : RocoBase
    {
        public Person(string id)
        {
            Id = id;
        }

        public override string Id { get; }
        [Index(IsUnique = true)]
        public string Name { get; set; }


        [Index]
        public Gender Gender { get; set; }


        public int Star { get; set; }
        public DateTime CreateTime { get; set; }
        public TimeSpan AliveTime { get; set; }
        
        public float SingleValue { get; set; }
        public double DoubleValue { get; set; }
        public char CharValue { get; set; }

        public Address Address { get; set; }

        public List<string> Posts { get; set; }
    }

    public enum Gender
    {
        Unknown,
        Man,
        Woman
    }
    
    public class Address
    {
        public string City { get; set; }
        public string Country { get; set; }
        public string Town { get; set; }
        public int Number { get; set; }
    }
}
