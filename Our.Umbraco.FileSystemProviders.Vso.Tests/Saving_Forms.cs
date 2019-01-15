using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Hosting;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using NUnit.Framework;
using Umbraco.Core.IO;

namespace Our.Umbraco.FileSystemProviders.Vso.Tests
{
    [TestFixture]
    public class Saving_Files
    {
        private FakeGitHttpClientBase gitClient;
        private VsoGitFileSystemProvider provider;
        private string path;
        private MemoryStream memoryStream;
        const string expectedOldObjectId = "8AE7FC51-2175-4423-8C09-1CF454367353";
        const string repositoryId = "C7FD20A1-9821-40F1-9F0B-10BB1360F43A";
        const string repoRoot = "/My.WebSite";
        private string entityId;
        private string expectedContents;
        private string expectedPath;
        private string localPath;
        private string physicalRoot;

        [SetUp]
        public void Setup()
        {
            entityId = "7b9487c3-7a66-4187-a049-e0213389e0a3";
            expectedContents = "{\"id\":\"7b9487c3-7a66-4187-a049-e0213389e0a3\", \"name\":\"Fancy form\"}";
            expectedPath = "/App_Data/UmbracoForms/Data/Forms/b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            localPath = @"C:\Fancy\Root\Some.Web\App_Data\UmbracoForms\Data\Forms\b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            physicalRoot = @"C:\Fancy\Root\Some.Web\App_Data\UmbracoForms\Data";

            Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity("User X"), new string[0]);
            VsoGitFileSystemProvider.ApplicationPath = () => @"C:\Fancy\Root\Some.Web";
            
            var wrapped = Mock.Of<IFileSystem>();
            gitClient = new FakeGitHttpClientBase(
                new List<GitCommitRef>
                {
                    new GitCommitRef
                    {
                        CommitId = expectedOldObjectId
                    }
                }
            );
            provider = new VsoGitFileSystemProvider(wrapped, gitClient, repositoryId, repoRoot);
            path = "~" + expectedPath;

            ResetStream();

            Mock.Get(wrapped).Setup(w => w.GetFullPath(It.IsAny<string>()))
                .Returns(new Func<string, string>(s => Path.Combine(physicalRoot, s.Replace("/", @"\"))));
        }

        private void ResetStream()
        {
            memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            writer.Write(expectedContents);
            writer.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);
        }

        [Test]
        public void Commits_Changed_Form_To_Git()
        {
            provider.AddFile(localPath, memoryStream);

            VerifyCommit(VersionControlChangeType.Edit, "User X modified form \"Fancy form\"");
        }

        [Test]
        public void Commits_Changed_Workflow_To_Git()
        {
            localPath = @"C:\Fancy\Root\Some.Web\App_Data\UmbracoForms\Data\Workflows\b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            expectedPath = "/App_Data/UmbracoForms/Data/Workflows/b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";
            expectedContents = "{\"id\":\"7b9487c3-7a66-4187-a049-e0213389e0a3\"}";
            ResetStream();

            provider.AddFile(localPath, memoryStream);

            VerifyCommit(VersionControlChangeType.Edit, $"User X modified workflow \"{entityId}\"");
        }

        [Test]
        public void Commits_New_File_To_Git()
        {
            gitClient.ThrowOnNextGetItem();

            provider.AddFile(localPath, memoryStream);

            VerifyCommit(VersionControlChangeType.Add, "User X added form \"Fancy form\"");
        }

        private void VerifyCommit(VersionControlChangeType changeType, string message)
        {
            var push = gitClient.Pushes.Last();
            var commit = push.Commits.Single();
            var change = commit.Changes.Single();
            var refUpdate = push.RefUpdates.Single();
            Assert.That(refUpdate.OldObjectId, Is.EqualTo(expectedOldObjectId));
            Assert.That(commit.Comment, Is.EqualTo(message));
            Assert.That(change.ChangeType, Is.EqualTo(changeType));
            Assert.That(change.Item.Path, Is.EqualTo(repoRoot + expectedPath));
            Assert.That(change.NewContent.ContentType, Is.EqualTo(ItemContentType.RawText));
            Assert.That(change.NewContent.Content, Is.EqualTo(expectedContents));
        }
    }

    public class FakeGitHttpClientBase : GitHttpClientBase
    {
        private readonly List<GitCommitRef> commits;
        private bool throwOnNextGet = false;

        public List<GitPush> Pushes = new List<GitPush>();

        public FakeGitHttpClientBase(List<GitCommitRef> commits)
            : base(new Uri("http://localhost"), new VssCredentials())
        {
            this.commits = commits;
        }

        public override async Task<GitPush> CreatePushAsync(GitPush push, string repositoryId, object userState = null, CancellationToken cancellationToken = new CancellationToken())
        {
            Pushes.Add(push);
            return await Task.FromResult(push);
        }

        public override async Task<List<GitCommitRef>> GetCommitsAsync(string repositoryId, GitQueryCommitsCriteria searchCriteria, int? skip = null, int? top = null, object userState = null, CancellationToken cancellationToken = new CancellationToken())
        {
            return await Task.FromResult(commits);
        }

        public override async Task<GitItem> GetItemAsync(string repositoryId, string path, string scopePath = null, VersionControlRecursionType? recursionLevel = null, bool? includeContentMetadata = null, bool? latestProcessedChange = null, bool? download = null, GitVersionDescriptor versionDescriptor = null, bool? includeContent = null, object userState = null, CancellationToken cancellationToken = new CancellationToken())
        {
            if (throwOnNextGet)
            {
                throwOnNextGet = false;
                throw new VssServiceException();
            }

            return await Task.FromResult(new GitItem());
        }

        public void ThrowOnNextGetItem()
        {
            throwOnNextGet = true;
        }
    }
}
