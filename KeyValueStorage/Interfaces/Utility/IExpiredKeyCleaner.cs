﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace KeyValueStorage.Interfaces.Utility
{
    public interface IExpiredKeyCleaner
    {
        IStoreProvider Provider { get; }
        void CleanupKeys();
	    void SetKeyExpiry(string key, DateTime expires);
	    DateTime? GetKeyExpiry(string key);
    }
}
