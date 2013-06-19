﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.Text;

namespace KeyValueStorage.ORM.Utility
{
    public static class TextExtensions
    {
        public static Dictionary<string, string> ToStringDictionaryExcludingRefs(this object obj)
        {
            var dict = obj.ToStringDictionary();

            foreach (var prop in obj.GetType().GetProperties())
            {
                //complex reduce logic goes here
                if (prop.PropertyType == typeof(int))
                    dict.Remove(prop.Name);
            }

            return dict;
        }
    }
}