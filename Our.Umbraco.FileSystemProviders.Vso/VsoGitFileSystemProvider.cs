using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Umbraco.Core.IO;

namespace Umbraco.Forms.Git
{
    public class VsoGitFileSystemProvider : IFileSystem
    {
        readonly IFileSystem innerFileSystem;
        private readonly GitHttpClientBase gitClient;
        private readonly string repositoryId;
        private readonly string repoRoot;

        public VsoGitFileSystemProvider(string virtualRoot, string gitUrl, string username, string password, string repositoryId, string repoRoot)
            : this(
                  new PhysicalFileSystem(virtualRoot), 
                  new GitHttpClient(new Uri(gitUrl), new VssCredentials(new VssBasicCredential(username, password))), repositoryId, repoRoot)
        {
        }

        public VsoGitFileSystemProvider(IFileSystem innerFileSystem, GitHttpClientBase gitClient, string repositoryId, string repoRoot)
        {
            this.innerFileSystem = innerFileSystem;
            this.gitClient = gitClient;
            this.repositoryId = repositoryId;
            this.repoRoot = repoRoot;
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            return innerFileSystem.GetDirectories(path);
        }

        public void DeleteDirectory(string path)
        {
            innerFileSystem.DeleteDirectory(path);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            innerFileSystem.DeleteDirectory(path, recursive);
        }

        public bool DirectoryExists(string path)
        {
            return innerFileSystem.DirectoryExists(path);
        }

        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            innerFileSystem.AddFile(path, stream, overrideIfExists);

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
                path = path.TrimStart('~');

                var lastCommitId = ExecuteSync(GetLastCommitId);
                var fileExists = ExecuteSync(() => GitFileExists(path));

                VersionControlChangeType changeType;
                string message;

                if (fileExists)
                {
                    changeType = VersionControlChangeType.Edit;
                    message = "Changed from backoffice";
                }
                else
                {
                    changeType = VersionControlChangeType.Add;
                    message = "Added from backoffice";
                }

                ExecuteSync(() => PushChange(path, stream, lastCommitId, message, changeType));
            }
            else
            {
                // TODO: Log can't seek?
            }
        }

        private async Task<bool> GitFileExists(string path)
        {
            try
            {
                await gitClient.GetItemAsync(repositoryId, repoRoot + path);
            }
            catch (VssServiceException)
            {
                return false;
            }
            return true;
        }

        private void ExecuteSync(Func<Task> asyncCall)
        {
            asyncCall().SyncResult();
        }

        private TResult ExecuteSync<TResult>(Func<Task<TResult>> asyncCall)
        {
            return asyncCall().SyncResult();
        }

        private async Task PushChange(string path, Stream stream, string lastCommitId, string message, VersionControlChangeType changeType)
        {
            var fileContents = new StreamReader(stream).ReadToEnd();
            var result = await gitClient.CreatePushAsync(new GitPush
            {
                RefUpdates = new[]
                {
                    new GitRefUpdate
                    {
                        Name = "refs/heads/master",
                        OldObjectId = lastCommitId
                    }
                },
                Commits = new[]
                {
                    new GitCommit
                    {
                        Comment = message,
                        Changes = new[]
                        {
                            new GitChange
                            {
                                ChangeType = changeType,
                                Item = new GitItem
                                {
                                    Path = repoRoot + path
                                },
                                NewContent = new ItemContent
                                {
                                    ContentType = ItemContentType.RawText,
                                    Content = fileContents
                                }
                            }
                        }
                    }
                }
            }, repositoryId);
        }

        private async Task<string> GetLastCommitId()
        {
            var result = await gitClient.GetCommitsAsync(repositoryId, new GitQueryCommitsCriteria
            {
                ItemVersion = new GitVersionDescriptor
                {
                    Version = "master",
                    VersionType = GitVersionType.Branch
                }
            }, 0, 1);
            return result[0].CommitId;
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return innerFileSystem.GetFiles(path);
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            return innerFileSystem.GetFiles(path, filter);
        }

        public Stream OpenFile(string path)
        {
            return innerFileSystem.OpenFile(path);
        }

        public void DeleteFile(string path)
        {
            innerFileSystem.DeleteFile(path);
        }

        public bool FileExists(string path)
        {
            return innerFileSystem.FileExists(path);
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            return innerFileSystem.GetRelativePath(fullPathOrUrl);
        }

        public string GetFullPath(string path)
        {
            return innerFileSystem.GetFullPath(path);
        }

        public string GetUrl(string path)
        {
            return innerFileSystem.GetUrl(path);
        }

        public DateTimeOffset GetLastModified(string path)
        {
            return innerFileSystem.GetLastModified(path);
        }

        public DateTimeOffset GetCreated(string path)
        {
            return innerFileSystem.GetCreated(path);
        }
    }
}
