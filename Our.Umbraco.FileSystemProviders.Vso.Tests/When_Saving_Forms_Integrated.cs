using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;
using Newtonsoft.Json;
using NUnit.Framework;
using Umbraco.Core.IO;

namespace Our.Umbraco.FileSystemProviders.Vso.Tests
{
    [TestFixture]
    [Explicit("Integrated calls. Settings in environment variables.")]
    public class When_Saving_Forms_Integrated
    {
        [Test]
        public void Creates_New_File()
        {
            fileSystemProvider.AddFile(expectedPath, memoryStream);

            var commits = gitClient.GetCommitsAsync(
                repositoryGuid,
                new GitQueryCommitsCriteria
                {
                    ItemVersion = new GitVersionDescriptor
                    {
                        VersionType = GitVersionType.Branch,
                        Version = "forms/test"
                    }
                }
            ).SyncResult();

            Console.WriteLine(JsonConvert.SerializeObject(commits.First()));

            Assert.That(commits.First(), Has.Property("Comment").EqualTo("User X added form \"Fancy form\""));
        }

        [Test]
        public void Updates_Existing_File()
        {
            fileSystemProvider.AddFile(expectedPath, memoryStream);
            
            ResetStream();

            fileSystemProvider.AddFile(expectedPath, memoryStream);

            var commits = gitClient.GetCommitsAsync(
                repositoryGuid,
                new GitQueryCommitsCriteria
                {
                    ItemVersion = new GitVersionDescriptor
                    {
                        VersionType = GitVersionType.Branch,
                        Version = "forms/test"
                    }
                }
            ).SyncResult();

            Console.WriteLine(JsonConvert.SerializeObject(commits.Take(2)));

            Assert.That(commits.First(), Has.Property("Comment").EqualTo("User X modified form \"Fancy form\""));
            Assert.That(commits.Skip(1).First(), Has.Property("Comment").EqualTo("User X added form \"Fancy form\""));
        }

        private string entityId;
        private string expectedContents;
        private string expectedPath;
        private string localPath;
        private string physicalRoot;
        private MemoryStream memoryStream;
        protected internal GitHttpClient gitClient;
        protected internal VsoGitFileSystemProvider fileSystemProvider;
        protected internal string repositoryId;
        private Guid repositoryGuid;

        [SetUp]
        public void Setup()
        {
            entityId = "7b9487c3-7a66-4187-a049-e0213389e0a3";
            expectedContents = "{\"id\":\"7b9487c3-7a66-4187-a049-e0213389e0a3\", \"name\":\"Fancy form\"}";
            expectedPath = "/App_Data/UmbracoForms/Data/Forms/b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            localPath = @"C:\Fancy\Root\Some.Web\App_Data\UmbracoForms\Data\Forms\b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            physicalRoot = @"C:\Fancy\Root\Some.Web\App_Data\UmbracoForms\Data";
            repositoryId = Environment.GetEnvironmentVariable("vsogit_repositoryId");
            repositoryGuid = new Guid(repositoryId);

            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("User X"), new string[0]);
            VsoGitFileSystemProvider.ApplicationPath = () => @"C:\Fancy\Root\Some.Web";
            VsoGitFileSystemProvider.LogException = (e) => throw e;

            var wrapped = Mock.Of<IFileSystem>();
            Mock.Get(wrapped).Setup(w => w.GetFullPath(It.IsAny<string>()))
                .Returns(new Func<string, string>(s => Path.Combine(physicalRoot, s.Replace("/", @"\"))));

            gitClient = new GitHttpClient(
                new Uri(Environment.GetEnvironmentVariable("vsogit_giturl")),
                new VssCredentials(new VssBasicCredential(
                    Environment.GetEnvironmentVariable("vsogit_username"),
                    Environment.GetEnvironmentVariable("vsogit_password")
                ))
            );
            fileSystemProvider = new VsoGitFileSystemProvider(
                wrapped,
                gitClient,
                repositoryId,
                Environment.GetEnvironmentVariable("vsogit_repositoryRoot"),
                "test"
            );

            gitClient.GetBranchRefsAsync(repositoryGuid).SyncResult()
                .Where(x => x.Name == "refs/heads/forms/test")
                .ToList()
                .ForEach(x =>
                {
                    gitClient.UpdateRefsAsync(
                        new[]{
                            new GitRefUpdate
                            {
                                NewObjectId = new String('0', 40),
                                OldObjectId = x.ObjectId,
                                Name = x.Name
                            }
                        },
                        repositoryGuid
                    ).SyncResult();
                });


            ResetStream();
        }

        private void ResetStream()
        {
            memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            writer.Write(expectedContents);
            writer.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
        }

    }
}
