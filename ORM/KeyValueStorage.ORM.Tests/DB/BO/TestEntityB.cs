﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeyValueStorage.ORM.Tests.DB.BO
{
    public class TestEntityB
    {
        public int Id { get; set; }
        public string Description { get; set; }
        public virtual TestEntityC EntityC { get; set; }
    }
}
