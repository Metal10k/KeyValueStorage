﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeyValueStorage.Utility
{
    public static class Helpers
    {
        public static IEnumerable<string> SeparateJsonArray(string json)
        {
            if (json == null)
                return Enumerable.Empty<string>();

            int depth = 0;
            bool inQuot = false;
            List<string> outputStringEnumerable = new List<string>();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < json.Length; i++)
            {
                if (json[i] == '{')
                    depth++;
                else if (json[i] == '}')
                    depth--;
                else if (json[i] == '"')
                    inQuot = !inQuot;
                else if (depth < 0)
                    throw new Exception("Json is invalid");

                sb.Append(json[i]);

                if ((depth == 0 && !inQuot) && i > 0)
                {
                    outputStringEnumerable.Add(sb.ToString());
                    sb = new StringBuilder();
                }
            }

            return outputStringEnumerable;
        }
    }
}
