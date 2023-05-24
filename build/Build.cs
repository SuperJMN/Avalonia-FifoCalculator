using System.Linq;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.GitHub;
using Serilog;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.GitHub.GitHubTasks;

[AzurePipelines(AzurePipelinesImage.WindowsLatest, ImportSecrets = new[]{ nameof(GitHubAuthenticationToken)})]
class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.Publish);

    public AbsolutePath OutputDirectory = RootDirectory / "output";
    public AbsolutePath PublishDirectory => OutputDirectory / "publish";
    public AbsolutePath PackagesDirectory => OutputDirectory / "packages";

    [Parameter("authtoken")] [Secret] readonly string GitHubAuthenticationToken;
    
    [GitRepository] readonly GitRepository Repository;
    
    [GitVersion] readonly GitVersion GitVersion;

    [Parameter] public string Project { get; set; }

    [Parameter("publish-framework")] public string PublishFramework { get; set; }

    [Parameter("publish-runtime")] public string PublishRuntime { get; set; }

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    Target Clean => _ => _
        .Executes(() =>
        {
            OutputDirectory.CreateOrCleanDirectory();
            OutputDirectory.GlobDirectories("/**/bin/*", "/**/obj/*").DeleteDirectories();
        });

    Target Publish => _ => _
        .DependsOn(Clean)
        .Requires(() => Project)
        .Executes(() =>
        {
            var publishCombinations =
                from project in new[] { Project, }
                from framework in new[]{ "net7.0" }
                from runtime in new[] { "win-x64", "linux-x64" }
                select new { project, framework, runtime };

            DotNetPublish(_ => _
                .SetConfiguration(Configuration)
                .CombineWith(publishCombinations, (_, v) => _
                    .SetProject(v.project)
                    .SetFramework(v.framework)
                    .SetRuntime(v.runtime)
                    .EnableSelfContained()
                    .SetOutput(PublishDirectory / v.runtime )));
        });

    Target Zip => _ => _
        .DependsOn(Publish)
        .Executes(() =>
        {
            PublishDirectory.GetDirectories()
                .ForEach(path =>
                {
                    var zipFileName = path.Name + ".zip";
                    Log.Information("Compressing {Path} to {File}", path, zipFileName );
                    path.ZipTo(PackagesDirectory / zipFileName);
                });
        });

    Target PublishGitHubRelease => _ => _
        .DependsOn(Zip)
        .OnlyWhenStatic(() => GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
        .Requires(() => GitHubAuthenticationToken)
        .Executes(async () =>
        {
            var releaseTag = $"v{GitVersion.MajorMinorPatch}";
    
            var repositoryInfo = GetGitHubRepositoryInfo(Repository);
    
            Log.Information("Commit for the release: {GitVersionSha}", GitVersion.Sha);
    
            var paths = PackagesDirectory.GlobFiles("*.zip").Select(path => (string)path).ToArray();
            Assert.NotEmpty(paths, "Found no packages to upload to the release");
            
            await PublishRelease(x => x
                .SetArtifactPaths(paths)
                .SetCommitSha(GitVersion.Sha)
                .SetRepositoryName(repositoryInfo.repositoryName)
                .SetRepositoryOwner(repositoryInfo.gitHubOwner)
                .SetTag(releaseTag)
                .SetToken(GitHubAuthenticationToken)
            );
        });
}
