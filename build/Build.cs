using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.GitHub;
using Octokit;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.GitHub.GitHubTasks;
using static Nuke.GitHub.ChangeLogExtensions;
using static Nuke.Common.ChangeLog.ChangelogTasks;


class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    public AbsolutePath OutputDirectory = RootDirectory / "output";
    public AbsolutePath PublishDirectory => OutputDirectory / "artifacts" / "publish";
    public AbsolutePath ZipFiles => OutputDirectory / "artifacts" / "zip";

    [Parameter("authtoken")] [Secret] readonly string GitHubAuthenticationToken;
    
    [GitRepository] readonly GitRepository Repository;
    
    [GitVersion] readonly GitVersion GitVersion;

    [Solution] public Solution Solution { get; set; }

    [Parameter("publish-framework")] public string PublishFramework { get; set; }

    [Parameter("publish-runtime")] public string PublishRuntime { get; set; }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
            OutputDirectory.GlobDirectories("/**/bin/*", "/**/obj/*").DeleteDirectories();
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution));
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Publish => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetPublish(x => x
                .SetProject(Solution)
                .SetFramework(PublishFramework)
                .SetRuntime(PublishRuntime)
                .SetOutput(PublishDirectory)
            );

            PublishDirectory.ZipTo(ZipFiles / "zipped.zip");
        });

    Target PublishGitHubRelease => _ => _
        .DependsOn(Publish)
        .OnlyWhenStatic(() => GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
        .Requires(() => GitHubAuthenticationToken)
        .Executes(async () =>
        {
            var releaseTag = $"v{GitVersion.MajorMinorPatch}";

            var repositoryInfo = GetGitHubRepositoryInfo(Repository);

            Log.Debug("Commit for the release: {GitVersionSha}", GitVersion.Sha);
            
            await PublishRelease(x => x
                .SetArtifactPaths(new string[]{ PublishDirectory })
                .SetCommitSha(GitVersion.Sha)
                .SetRepositoryName(repositoryInfo.repositoryName)
                .SetRepositoryOwner(repositoryInfo.gitHubOwner)
                .SetTag(releaseTag)
                .SetToken(GitHubAuthenticationToken)
            );
        });
}
