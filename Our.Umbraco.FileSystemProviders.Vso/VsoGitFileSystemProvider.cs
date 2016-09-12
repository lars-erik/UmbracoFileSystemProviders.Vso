using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Web.Hosting;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Forms.Data.Storage;

namespace Our.Umbraco.FileSystemProviders.Vso
{
    public class VsoGitFileSystemProvider : IFileSystem
    {
        public static Func<string> ApplicationPath = () => HostingEnvironment.ApplicationPhysicalPath;

        readonly IFileSystem innerFileSystem;
        private readonly GitHttpClientBase gitClient;
        private readonly string repositoryId;
        private readonly string repoRoot;

        public VsoGitFileSystemProvider(string virtualRoot, string gitUrl, string username, string password, string repositoryId, string repoRoot)
            : this(
                  new FormsFileSystem(new PhysicalFileSystem(virtualRoot)), 
                  new GitHttpClient(new Uri(gitUrl), new VssCredentials(new VssBasicCredential(username, password))),
                  repositoryId,
                  repoRoot
            )
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

            path = innerFileSystem.GetFullPath(path);
            path = path.Replace(ApplicationPath(), "").Replace(@"\", "/").EnsureStartsWith("/");

            if (stream.CanSeek)
            {
                stream.Seek(0, SeekOrigin.Begin);
                path = path.TrimStart('~');

                var lastCommitId = GetLastCommitId();
                var fileExists = GitFileExists(path);

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

                PushChange(path, stream, lastCommitId, message, changeType);
            }
            else
            {
                // TODO: Log can't seek?
            }
        }

        private bool GitFileExists(string path)
        {
            try
            {
                gitClient.GetItemAsync(repositoryId, repoRoot + path).SyncResult();
            }
            catch (VssServiceException)
            {
                return false;
            }
            return true;
        }

        private void PushChange(string path, Stream stream, string lastCommitId, string message, VersionControlChangeType changeType)
        {
            var fileContents = new StreamReader(stream).ReadToEnd();
            var result = gitClient.CreatePushAsync(new GitPush
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
            }, repositoryId).SyncResult();
        }

        private string GetLastCommitId()
        {
            var result = gitClient.GetCommitsAsync(repositoryId, new GitQueryCommitsCriteria
            {
                ItemVersion = new GitVersionDescriptor
                {
                    Version = "master",
                    VersionType = GitVersionType.Branch
                }
            }, 0, 1).SyncResult();
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
