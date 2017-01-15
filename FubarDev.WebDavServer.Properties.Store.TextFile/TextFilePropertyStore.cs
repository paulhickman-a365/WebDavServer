﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using FubarDev.WebDavServer.FileSystem;
using FubarDev.WebDavServer.Properties.Events;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Newtonsoft.Json;

namespace FubarDev.WebDavServer.Properties.Store.TextFile
{
    public class TextFilePropertyStore : IFileSystemPropertyStore
    {
        private readonly IMemoryCache _cache;

        private readonly TextFilePropertyStoreOptions _options;

        private readonly string _storeEntryName = ".properties";

        public TextFilePropertyStore(IOptions<TextFilePropertyStoreOptions> options, IMemoryCache cache)
            : this(options.Value, cache)
        {
        }

        public TextFilePropertyStore(TextFilePropertyStoreOptions options, IMemoryCache cache)
        {
            _cache = cache;
            _options = options;
            RootPath = options.RootFolder;
        }

        public int Cost => _options.EstimatedCost;

        public string RootPath { get; set; }

        public bool IgnoreEntry(IEntry entry)
        {
            return entry is IDocument && entry.Name == _storeEntryName;
        }

        public Task<IReadOnlyCollection<IUntypedReadableProperty>> LoadAndCreateAsync(IEntry entry, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<IUntypedReadableProperty> result;
            var storeData = Load(entry, false);
            EntryInfo info;
            if (storeData.Entries.TryGetValue(GetEntryKey(entry), out info))
            {
                result = info.Attributes.Select(x => CreateDeadProperty(entry, x.Value)).ToList();
            }
            else
            {
                result = new IUntypedReadableProperty[0];
            }

            return Task.FromResult(result);
        }

        public Task<XElement> LoadRawAsync(IEntry entry, XName name, CancellationToken cancellationToken)
        {
            var info = GetInfo(entry);
            if (info == null)
                return Task.FromResult<XElement>(null);

            XElement result;
            if (!info.Attributes.TryGetValue(name, out result))
                return Task.FromResult<XElement>(null);

            return Task.FromResult(result);
        }

        public Task SaveRawAsync(IEntry entry, XElement element, CancellationToken cancellationToken)
        {
            var info = GetInfo(entry) ?? new EntryInfo();
            info.Attributes[element.Name] = element;
            UpdateInfo(entry, info);

            return Task.FromResult(0);
        }

        public Task RemoveRawAsync(IEntry entry, XName name, CancellationToken cancellationToken)
        {
            var info = GetInfo(entry) ?? new EntryInfo();
            info.Attributes.Remove(name);
            UpdateInfo(entry, info);

            return Task.FromResult(0);
        }

        public async Task<EntityTag> GetETagAsync(IDocument document, CancellationToken cancellationToken)
        {
            var etag = await LoadRawAsync(document, EntityTag.PropertyName, cancellationToken).ConfigureAwait(false);
            if (etag == null)
            {
                etag = new EntityTag().ToXml();
                await SaveRawAsync(document, etag, cancellationToken).ConfigureAwait(false);
            }

            return EntityTag.FromXml(etag);
        }

        public async Task<EntityTag> UpdateETagAsync(IDocument document, CancellationToken cancellationToken)
        {
            var etagElement = await LoadRawAsync(document, EntityTag.PropertyName, cancellationToken).ConfigureAwait(false);
            EntityTag etag;
            if (etagElement == null)
            {
                etag = EntityTag.FromXml(null);
            }
            else
            {
                etag = EntityTag.FromXml(etagElement).Update();
            }

            etagElement = etag.ToXml();
            await SaveRawAsync(document, etagElement, cancellationToken).ConfigureAwait(false);

            return etag;
        }

        public Task HandleMovedEntryAsync(EntryMoved info, CancellationToken ct)
        {
            var isCollection = info.NewEntry is ICollection;
            var oldFileName = GetFileNameFor(info.FromPath, isCollection);
            var oldEntryKey = GetEntryKey(info.FromPath, isCollection);
            var oldStoreData = Load(oldFileName, false);
            EntryInfo oldInfo;
            if (!oldStoreData.Entries.TryGetValue(oldEntryKey, out oldInfo))
                return Task.FromResult(0);

            oldStoreData.Entries.Remove(oldEntryKey);
            Save(oldFileName, oldStoreData);

            var newFileName = GetFileNameFor(info.NewEntry);
            var newEntryKey = GetEntryKey(info.NewEntry);
            var newStoreData = Load(newFileName, false);
            newStoreData.Entries[newEntryKey] = oldInfo;
            Save(newFileName, newStoreData);

            return Task.FromResult(0);
        }

        public Task HandleModifiedEntryAsync(IEntry entry, CancellationToken ct)
        {
            return UpdateETagAsync((IDocument)entry, ct);
        }

        private static string GetEntryKey(IEntry entry)
        {
            if (entry is ICollection)
                return ".";
            return entry.Name.ToLower();
        }

        private static string GetEntryKey(string path, bool isCollection)
        {
            if (isCollection)
                return ".";
            return Path.GetFileName(path);
        }

        private void UpdateInfo(IEntry entry, EntryInfo info)
        {
            var storeData = Load(entry, true);
            var entryKey = GetEntryKey(entry);
            storeData.Entries[entryKey] = info;
            Save(entry, storeData);
        }

        private EntryInfo GetInfo(IEntry entry)
        {
            var storeData = Load(entry, false);

            EntryInfo info;
            if (!storeData.Entries.TryGetValue(GetEntryKey(entry), out info))
                info = new EntryInfo();

            return info;
        }

        private void Save(IEntry entry, StoreData data)
        {
            Save(GetFileNameFor(entry), data);
        }

        private StoreData Load(IEntry entry, bool useCache)
        {
            return Load(GetFileNameFor(entry), useCache);
        }

        private void Save(string fileName, StoreData data)
        {
            try
            {
                var key = fileName.ToLower();
                File.WriteAllText(fileName, JsonConvert.SerializeObject(data));
                _cache.Set(key, data);
            }
            catch (Exception)
            {
                // Ignore all exceptions for directories that cannot be modified
            }
        }

        private StoreData Load(string fileName, bool useCache)
        {
            if (!File.Exists(fileName))
                return new StoreData();

            var key = fileName.ToLower();
            if (!useCache)
            {
                var result = JsonConvert.DeserializeObject<StoreData>(File.ReadAllText(fileName));
                _cache.Set(key, result);
                return result;
            }

            return _cache.GetOrCreate(key, ce => JsonConvert.DeserializeObject<StoreData>(File.ReadAllText(fileName)));
        }

        private string GetFileNameFor(IEntry entry)
        {
            var isCollection = entry is ICollection;
            return GetFileNameFor(entry.Path, isCollection);
        }

        private string GetFileNameFor(string path, bool isCollection)
        {
            if (isCollection)
                return Path.Combine(RootPath, path, _storeEntryName);

            return Path.Combine(RootPath, Path.GetDirectoryName(path), _storeEntryName);
        }

        private IUntypedWriteableProperty CreateDeadProperty(IEntry entry, XElement element)
        {
            return new DeadProperty(this, entry, element);
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        private class StoreData
        {
            public IDictionary<string, EntryInfo> Entries { get; } = new Dictionary<string, EntryInfo>();
        }

        private class EntryInfo
        {
            public IDictionary<XName, XElement> Attributes { get; } = new Dictionary<XName, XElement>();
        }
    }
}