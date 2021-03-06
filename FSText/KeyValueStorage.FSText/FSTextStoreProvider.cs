﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeyValueStorage.Exceptions;
using KeyValueStorage.Interfaces;
using KeyValueStorage.Interfaces.Utility;
using KeyValueStorage.RetryStrategies;
using KeyValueStorage.Utility;

namespace KeyValueStorage.FSText
{
    public class FSTextStoreProvider : IStoreProvider
    {
        static FSTextStoreProvider()
        {
            KeyCharSubstitutorDefault = new CharSubstitutor();
            KeyCharSubstitutorDefault.SubstitutionTable.Add(':', '`');
        }

        private DirectoryInfo DI { get; set; }
        const string suffix = ".json";
        const string dataSuffix = ".data";
        const string casPrefix = "-CAS-";
        const string lockPrefix = "-L-";
        public static CharSubstitutor KeyCharSubstitutorDefault = new CharSubstitutor();

        public CharSubstitutor KeyCharSubstitutor {get;protected set;}
        public KVSExpiredKeyCleaner KeyCleaner { get; protected set; } 

		public FSTextStoreProvider()
			:this(AppDomain.CurrentDomain.RelativeSearchPath + @"\KVS\")
		{

		}


        public FSTextStoreProvider(string path, CharSubstitutor keyCharsubstitutor = null)
        {
            DI = new DirectoryInfo(path);

            if (!DI.Exists)
                DI.Create();

            KeyCharSubstitutor = keyCharsubstitutor ?? KeyCharSubstitutorDefault;
            KeyCleaner = new KVSExpiredKeyCleaner(this, lockPrefix + "KC", new TimeSpan(1, 0, 0, 0));
        }

        FileInfo _GetFileInfo(string key, bool isData = false)
        {
            return new FileInfo(Path.Combine(DI.FullName, !isData? key + suffix : key + dataSuffix));
        }


        #region IStoreProvider
        public void Initialize()
        {

        }

        public string Get(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(key);

            if (!fi.Exists)
                return null;

            using (var sr = fi.OpenText())
            {
                return sr.ReadToEnd();
            }
        }

        public void Set(string key, string value)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(key);

            if(fi.Exists)
                fi.Delete();

            using (var fs = fi.CreateText())
            {
                fs.Write(value);
            }
        }

        public void Remove(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(key);

            if (fi.Exists)
                fi.Delete();
        }

        public string Get(string key, out ulong cas)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(casPrefix + key, true);

            if (fi.Exists)
            {
                using (var sr = fi.OpenRead())
                {
                    byte[] bytes = new byte[sr.Length];
                    sr.Read(bytes, 0, (int)sr.Length);

                    if (bytes.Length > 0)
                    {
                        try
                        {
                            cas = BitConverter.ToUInt64(bytes, 0);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Data for key " + casPrefix + key + " is not CAS data", ex);
                        }
                    }
                    else
                        cas = 0;
                }
            }
            else
                cas = 0;

            return this.Get(key);
        }

        public void Set(string key, string value, ulong cas)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(casPrefix + key, true);
            var fiLock = _GetFileInfo(lockPrefix + key, true);

            if (!fi.Exists)
                fi.Create().Dispose();

            using(var swLock = fiLock.Create())
            {
                ulong readCasVal = 0;

                //get current cas
                byte[] bytes = null;
                using (var srCAS = fi.OpenRead())
                {
                    bytes = new byte[srCAS.Length];
                    srCAS.Read(bytes, 0, (int)srCAS.Length);
                }
                using (var swCAS = fi.OpenWrite())
                {

                    swCAS.Seek(0, SeekOrigin.Begin);

                    if (bytes.Count() > 0)
                        readCasVal = BitConverter.ToUInt64(bytes, 0);

                    if (readCasVal != cas)
                        throw new CASException("CAS expired");

                    //do our actual set operation
                    Set(key, value);

                    var bytesToWrite = BitConverter.GetBytes(cas + 1);
                    swCAS.Write(bytesToWrite, 0, bytesToWrite.Count());
                    swCAS.Flush();
                }
            }
            fiLock.Delete();
        }

        public void Set(string key, string value, DateTime expires)
        {
            Set(key, value);
            _SetKeyExpiry(key, expires);
        }

        public void Set(string key, string value, TimeSpan expiresIn)
        {
            var expires = DateTime.UtcNow + expiresIn;

            Set(key, value);
            _SetKeyExpiry(key, expires);
        }

        public void Set(string key, string value, ulong cas, DateTime expires)
        {
            Set(key, value, cas);
            _SetKeyExpiry(key, expires);
        }

        public void Set(string key, string value, ulong cas, TimeSpan expiresIn)
        {
            var expires = DateTime.UtcNow + expiresIn;

            Set(key, value, cas);
            _SetKeyExpiry(key, expires);
        }


        public bool Exists(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            if (_GetFileInfo(key).Exists)
                return true;
            return false;
        }

        public DateTime? ExpiresOn(string key)
        {
            if (KeyCleaner != null)
                return KeyCleaner.GetKeyExpiry(key);
            return null;
        }

        public IEnumerable<string> GetStartingWith(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            foreach (var file in DI.GetFiles(key + "*"))
            {
                var keyItem = file.Name.Replace(suffix, "");
                yield return Get(keyItem);
            }
        }

        public IEnumerable<string> GetContaining(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            foreach (var file in DI.GetFiles("*" + key + "*"))
            {
                var keyItem = file.Name.Replace(suffix, "");
                yield return Get(keyItem);
            }
        }

        public IEnumerable<string> GetAllKeys()
        {
            foreach (var file in DI.GetFiles())
            {
                var keyItem = file.Name.Replace(suffix, "");
                yield return keyItem;
            }
        }

        public IEnumerable<string> GetKeysStartingWith(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            foreach (var file in DI.GetFiles(key + "*"))
            {
                var keyItem = file.Name.Replace(suffix, "");
                keyItem = KeyCharSubstitutor.ConvertBack(keyItem);
                yield return keyItem;
            }
        }

        public IEnumerable<string> GetKeysContaining(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            foreach (var file in DI.GetFiles("*" + key + "*"))
            {
                var keyItem = file.Name.Replace(suffix, "");
                keyItem = KeyCharSubstitutor.ConvertBack(keyItem);
                yield return keyItem;
            }
        }

        public int CountStartingWith(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            return DI.GetFiles(key + "*").Count();
        }

        public int CountContaining(string key)
        {
            key = KeyCharSubstitutor.Convert(key);

            return DI.GetFiles("*" + key + "*").Count();
        }

        public int CountAll()
        {
            return DI.GetFiles().Count();
        }

        public ulong GetNextSequenceValue(string key, int increment)
        {
            key = KeyCharSubstitutor.Convert(key);
            return IStoreProviderInternalHelpers.GetNextSequenceValueViaCAS(this, key, increment);
        }

        public void Append(string key, string value)
        {
            key = KeyCharSubstitutor.Convert(key);

            var fi = _GetFileInfo(key);

            if (!fi.Exists)
                fi.Create().Dispose();
            
            using (var fs = fi.OpenWrite())
            {
                fs.Seek(fs.Length,SeekOrigin.Begin);
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                fs.Write(bytes, 0, bytes.Length);
            }
        }

        public IRetryStrategy GetDefaultRetryStrategy()
        {
            return new FSTextRetryStrategy(5, 500);
        }

		public IKeyLock GetKeyLock(string key, DateTime expires, IRetryStrategy retryStrategy = null, string workerId = null)
		{
			return new KVSLockWithCAS(key, expires, this, retryStrategy ?? new SimpleLockRetryStrategy(5,500), workerId);
		}

	    #endregion

        private void _SetKeyExpiry(string key, DateTime expires)
        {
            if (KeyCleaner == null)
                throw new InvalidOperationException("Expiry date cannot be set if no key cleaner is present");

            KeyCleaner.SetKeyExpiry(key, expires);
        }

        public void Dispose()
        {

        }
    }
}
