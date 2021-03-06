﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyValueStorage.Interfaces;
using Couchbase;
using Couchbase.Management;
using KeyValueStorage.Exceptions;
using System.Reflection;
using System.IO;
using KeyValueStorage.Interfaces.Utility;
using KeyValueStorage.RetryStrategies;
using KeyValueStorage.Utility;

namespace KeyValueStorage.Couchbase
{

    public class CouchbaseStoreProvider : IStoreProvider
    {
        CouchbaseClient Client;
        CouchbaseCluster Cluster;
        string Bucket;
        public const string KVSDocNm = "KVSViews";

        public CouchbaseStoreProvider(CouchbaseClient client)
        {
            Client = client;
        }


        /// <summary>
        /// Initializes a new instance of the <see cref="CouchbaseStoreProvider"/> class.
        /// It is necessary to provider a CouchbaseCluster object if you wish the factory to be able to correctly check and initialize the required views on startup.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="cluster">The cluster.</param>
        public CouchbaseStoreProvider(CouchbaseClient client, CouchbaseCluster cluster, string bucket)
        {
            Client = client;
            Cluster = cluster;
            Bucket = bucket;
        }

        #region IStoreProvider
        public void Initialize()
        {
            if (Cluster != null)
            {
                var json = Encoding.ASCII.GetString(Properties.Resources.KVSViews);
                Cluster.CreateDesignDocument(Bucket, KVSDocNm, json);
            }
        }

        public string Get(string key)
        {
            return (string)Client.Get(key);
        }

        public void Set(string key, string value)
        {
            Client.Store(Enyim.Caching.Memcached.StoreMode.Set, key, value);
        }

        public void Remove(string key)
        {
            Client.Remove(key);
        }

        public string Get(string key, out ulong cas)
        {
            var casRes = Client.GetWithCas(key);
            cas = casRes.Cas;
            return (string)casRes.Result;
        }

        public void Set(string key, string value, ulong cas)
        {
            var casRes = Client.Cas(Enyim.Caching.Memcached.StoreMode.Replace, key, value, cas);

            if (casRes.Result == false)
                throw new CASException("CAS expired");
        }

        public void Set(string key, string value, DateTime expires)
        {
            Client.Store(Enyim.Caching.Memcached.StoreMode.Set, key, value, expires);
        }

        public void Set(string key, string value, TimeSpan expiresIn)
        {
            Client.Store(Enyim.Caching.Memcached.StoreMode.Set, key, value,DateTime.UtcNow + expiresIn);
        }

        public void Set(string key, string value, ulong cas, DateTime expires)
        {
            Client.Cas(Enyim.Caching.Memcached.StoreMode.Set, key, value, expires, cas);
        }

        public void Set(string key, string value, ulong cas, TimeSpan expiresIn)
        {
            Client.Cas(Enyim.Caching.Memcached.StoreMode.Set, key, value, DateTime.UtcNow + expiresIn, cas);
        }

        public bool Exists(string key)
        {
            return Client.KeyExists(key);
        }

        public DateTime? ExpiresOn(string key)
        {
            //Couchbase does not seem to have an easy api to support this
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetStartingWith(string key)
        {
            var keyMax = Encoding.UTF8.GetString(ArrayHelpers.IncrementByteArrByOne(Encoding.UTF8.GetBytes(key)));

            var values = Client.GetView(KVSDocNm, "GetAll").StartKey(key).EndKey(keyMax).Select(s => (string)s.GetItem()).ToList();
            return values;
        }


        public IEnumerable<string> GetAllKeys()
        {
            return Client.GetView(KVSDocNm, "GetAll").Select(s => s.ItemId);
        }

        public IEnumerable<string> GetKeysStartingWith(string key)
        {
            var keyMax = Encoding.UTF8.GetString(ArrayHelpers.IncrementByteArrByOne(Encoding.UTF8.GetBytes(key)));

            var keys = Client.GetView(KVSDocNm, "GetAll").StartKey(key).EndKey(keyMax).Select(s => s.ItemId);

            return keys;
        }

        public int CountStartingWith(string key)
        {
            var keyMax = Encoding.UTF8.GetString(ArrayHelpers.IncrementByteArrByOne(Encoding.UTF8.GetBytes(key)));

            return Client.GetView(KVSDocNm, "GetAll").StartKey(key).EndKey(keyMax).Count();
        }

        public int CountAll()
        {
            return Client.GetView(KVSDocNm, "GetAll").Count();
        }

        public ulong GetNextSequenceValue(string key, int increment)
        {
            return Client.Increment(key, 1, (ulong)increment);
        }

        public void Append(string key, string value)
        {
            Client.Append(key, new ArraySegment<byte>(Encoding.UTF8.GetBytes(value)));
        }

        public IRetryStrategy GetDefaultRetryStrategy()
        {
            return new SimpleRetryStrategy(5, 1000);
        }

		public IKeyLock GetKeyLock(string key, DateTime expires, IRetryStrategy retryStrategy = null, string workerId = null)
		{
			return new KVSLockWithCAS(key, expires, this, retryStrategy ?? new SimpleLockRetryStrategy(5, 500), workerId);
		}

	    #endregion

        public void Dispose()
        {
            //we do not dispose of the client as its lifetime is intended to extend beyond the lifetime of the provider and context.
        }

        string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }
    }
}
