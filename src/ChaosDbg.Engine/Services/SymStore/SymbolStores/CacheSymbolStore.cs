using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ChaosDbg.SymStore
{
    public sealed class CacheSymbolStore : SymbolStore
    {
        public string CacheDirectory { get; }

        public CacheSymbolStore(ISymStoreLogger logger, SymbolStore backingStore, string cacheDirectory)
            : base(logger, backingStore)
        {
            if (cacheDirectory == null)
                throw new ArgumentNullException(nameof(cacheDirectory));

            CacheDirectory = cacheDirectory;
        }

        protected override Task<SymbolStoreFile> GetFileInner(SymbolStoreKey key, CancellationToken token)
        {
            SymbolStoreFile result = null;
            string cacheFile = GetCacheFilePath(key);
            if (File.Exists(cacheFile))
            {
                Stream fileStream = File.OpenRead(cacheFile);

                //If the length is 0, assume that we previously attempted to download the file and that it got corrupt.
                //Try and download the file again
                if (fileStream.Length == 0)
                    return Task.FromResult<SymbolStoreFile>(null);

                result = new SymbolStoreFile(fileStream, cacheFile);
            }
            return Task.FromResult(result);
        }

        protected override async Task WriteFileInner(SymbolStoreKey key, SymbolStoreFile file)
        {
            string cacheFile = GetCacheFilePath(key);

            if (cacheFile != null)
            {
                if (File.Exists(cacheFile))
                {
                    //If the file already exists, if the length is not the same, we need to rewrite the file

                    using (var fs = File.OpenRead(cacheFile))
                    {
                        if (fs.Length == file.Stream.Length)
                            return;
                    }
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                    using (Stream destinationStream = File.OpenWrite(cacheFile))
                    {
                        await file.Stream.CopyToAsync(destinationStream).ConfigureAwait(false);
                        Logger.Verbose("Cached: {0}", cacheFile);
                    }
                }
                catch (Exception ex) when (ex is ArgumentException || ex is UnauthorizedAccessException || ex is IOException)
                {
                }
            }
        }

        private string GetCacheFilePath(SymbolStoreKey key)
        {
            if (SymbolStoreKey.IsKeyValid(key.Index))
            {
                return Path.Combine(CacheDirectory, key.Index);
            }
            Logger.Error("CacheSymbolStore: invalid key index {0}", key.Index);
            return null;
        }

        public override bool Equals(object obj)
        {
            if (obj is CacheSymbolStore)
            {
                return IsPathEqual(CacheDirectory, ((CacheSymbolStore) obj).CacheDirectory);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashPath(CacheDirectory);
        }

        public override string ToString()
        {
            return $"Cache: {CacheDirectory}";
        }
    }
}
