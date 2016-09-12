using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Moq;
using NUnit.Framework;
using Umbraco.Core.IO;

namespace Umbraco.Forms.Git.Tests
{
    [TestFixture]
    public class Saving_Files
    {
        private Mock<GitHttpClientBase> gitClientMock;
        private VsoGitFileSystemProvider provider;
        private string path;
        private MemoryStream memoryStream;
        const string expectedOldObjectId = "8AE7FC51-2175-4423-8C09-1CF454367353";
        const string repositoryId = "C7FD20A1-9821-40F1-9F0B-10BB1360F43A";
        const string repoRoot = "/My.WebSite";
        private const string expectedContents = "{\"id\":\"7b9487c3-7a66-4187-a049-e0213389e0a3\"}";
        private const string expectedPath = "/App_Plugins/UmbracoForms/Data/Forms/b79a3cc8-533c-41a9-bcd2-2e9210c7c010.json";

        [SetUp]
        public void Setup()
        {
            var wrapped = Mock.Of<IFileSystem>();
            gitClientMock = new Mock<GitHttpClientBase>(new Uri("http://localhost"), new VssCredentials());
            var gitClient = gitClientMock.Object;
            provider = new VsoGitFileSystemProvider(wrapped, gitClient, repositoryId, repoRoot);
            path = "~" + expectedPath;

            memoryStream = new MemoryStream();
            var writer = new StreamWriter(memoryStream);
            writer.Write(expectedContents);
            writer.Flush();
            memoryStream.Seek(0, SeekOrigin.Begin);

            gitClientMock.Setup(c => c.GetCommitsAsync(repositoryId, It.IsAny<GitQueryCommitsCriteria>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<GitCommitRef>
                {
                    new GitCommitRef
                    {
                        CommitId = expectedOldObjectId
                    }
                });
        }

        [Test]
        public void Commits_Changed_File_To_Git()
        {
            provider.AddFile(path, memoryStream);

            VerifyCommit(VersionControlChangeType.Edit, "Changed from backoffice");
        }

        [Test]
        public void Commits_New_File_To_Git()
        {
            gitClientMock
                .Setup(c => c.GetItemAsync(repositoryId, repoRoot + expectedPath, It.IsAny<string>(), It.IsAny<VersionControlRecursionType?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<GitVersionDescriptor>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new VssServiceException());

            provider.AddFile(path, memoryStream);

            gitClientMock
                .Verify(c => c.GetItemAsync(repositoryId, repoRoot + expectedPath, It.IsAny<string>(), It.IsAny<VersionControlRecursionType?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<bool?>(), It.IsAny<GitVersionDescriptor>(), It.IsAny<object>(), It.IsAny<CancellationToken>()));

            VerifyCommit(VersionControlChangeType.Add, "Added from backoffice");
        }

        private void VerifyCommit(VersionControlChangeType changeType, string message)
        {
            gitClientMock.Verify(c => c.CreatePushAsync(
                Match.Create<GitPush>(gp =>
                    VerifyCommit(gp, changeType, message)
                    ),
                repositoryId,
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()
                ));
        }

        private bool VerifyCommit(GitPush gp, VersionControlChangeType changeType, string message)
        {
            //gp.RefUpdates.Single().RepositoryId == new Guid(repositoryId) &&
            var result = gp.RefUpdates.Single().OldObjectId == expectedOldObjectId &&
                               gp.Commits.Single().Comment == message &&
                               gp.Commits.Single().Changes.Single().ChangeType == changeType &&
                               gp.Commits.Single().Changes.Single().Item.Path == repoRoot + expectedPath &&
                               gp.Commits.Single().Changes.Single().NewContent.ContentType == ItemContentType.RawText &&
                               gp.Commits.Single().Changes.Single().NewContent.Content == expectedContents;
            return result;
        }
    }
}
