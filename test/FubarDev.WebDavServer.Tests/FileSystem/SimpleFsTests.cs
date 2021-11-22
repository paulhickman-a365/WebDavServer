﻿// <copyright file="SimpleFsTests.cs" company="Fubar Development Junker">
// Copyright (c) Fubar Development Junker. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;

using FubarDev.WebDavServer.FileSystem;
using FubarDev.WebDavServer.Tests.Support.ServiceBuilders;

using Microsoft.Extensions.DependencyInjection;

using Xunit;

namespace FubarDev.WebDavServer.Tests.FileSystem
{
    public abstract class SimpleFsTests<T> : IClassFixture<T>
        where T : class, IFileSystemServices
    {
        protected SimpleFsTests(T fsServices)
        {
            var fsFactory = fsServices.ServiceProvider.GetRequiredService<IFileSystemFactory>();
            var principal = new GenericPrincipal(
                new GenericIdentity(Guid.NewGuid().ToString()),
                Array.Empty<string>());
            FileSystem = fsFactory.CreateFileSystem(
                null,
                principal);
        }

        public IFileSystem FileSystem { get; }

        [Fact]
        public async Task Empty()
        {
            var ct = CancellationToken.None;
            var root = await FileSystem.Root.ConfigureAwait(false);
            var rootChildren = await root.GetChildrenAsync(ct).ConfigureAwait(false);
            Assert.Equal(0, rootChildren.Count);
        }

        [Fact]
        public async Task SingleEmptyDirectory()
        {
            var ct = CancellationToken.None;
            var root = await FileSystem.Root.ConfigureAwait(false);
            var test1 = await root.CreateCollectionAsync("test1", ct).ConfigureAwait(false);
            var rootChildren = await root.GetChildrenAsync(ct).ConfigureAwait(false);
            Assert.Collection(
                rootChildren,
                child =>
                {
                    Assert.NotNull(child);
                    var coll = Assert.IsAssignableFrom<ICollection>(child);
                    Assert.Equal(test1.Path, coll.Path);
                    Assert.Equal("test1", coll.Name);
                    Assert.NotNull(coll.Parent);
                    Assert.Equal(root.Path, coll.Parent!.Path);
                });
        }

        [Fact]
        public async Task TwoEmptyDirectories()
        {
            var ct = CancellationToken.None;
            var root = await FileSystem.Root.ConfigureAwait(false);
            var test1 = await root.CreateCollectionAsync("test1", ct).ConfigureAwait(false);
            var test2 = await root.CreateCollectionAsync("test2", ct).ConfigureAwait(false);
            var rootChildren = await root.GetChildrenAsync(ct).ConfigureAwait(false);
            Assert.Collection(
                rootChildren.OrderBy(n => n.Name),
                child =>
                {
                    Assert.NotNull(child);
                    var coll = Assert.IsAssignableFrom<ICollection>(child);
                    Assert.Equal(test1.Path, coll.Path);
                    Assert.Equal("test1", coll.Name);
                    Assert.NotNull(coll.Parent);
                    Assert.Equal(root.Path, coll.Parent!.Path);
                },
                child =>
                {
                    Assert.NotNull(child);
                    var coll = Assert.IsAssignableFrom<ICollection>(child);
                    Assert.Equal(test2.Path, coll.Path);
                    Assert.Equal("test2", coll.Name);
                    Assert.NotNull(coll.Parent);
                    Assert.Equal(root.Path, coll.Parent!.Path);
                });
        }

        [Fact]
        public async Task CannotAddTwoDirectoriesWithSameName()
        {
            var ct = CancellationToken.None;
            var root = await FileSystem.Root.ConfigureAwait(false);
            await root.CreateCollectionAsync("test1", ct).ConfigureAwait(false);
            await Assert.ThrowsAnyAsync<IOException>(async () => await root.CreateCollectionAsync("test1", ct).ConfigureAwait(false)).ConfigureAwait(false);
        }

        [Fact]
        public async Task SupportPartialChange()
        {
            var ct = CancellationToken.None;
            var root = await FileSystem.Root.ConfigureAwait(false);
            var doc = await root.CreateDocumentAsync("test1.txt", ct);
            await using (var stream = await doc.CreateAsync(ct).ConfigureAwait(false))
            {
                await stream.WriteAsync(new byte[] { 0, 1 }, ct).ConfigureAwait(false);
            }

            await using (var stream = await doc.OpenWriteAsync(1, ct).ConfigureAwait(false))
            {
                await stream.WriteAsync(new byte[] { 2 }, ct).ConfigureAwait(false);
            }

            await using var temp = new MemoryStream();
            await using (var stream = await doc.OpenReadAsync(ct).ConfigureAwait(false))
            {
                await stream.CopyToAsync(temp, ct).ConfigureAwait(false);
            }

            var data = temp.ToArray();
            Assert.Collection(
                data,
                v => Assert.Equal(0, v),
                v => Assert.Equal(2, v));
        }
    }
}
