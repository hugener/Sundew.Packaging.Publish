﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="StageBuildFacade.cs" company="Hukano">
// Copyright (c) Hukano. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Sundew.Packaging.Tool.Versioning
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using global::NuGet.Versioning;
    using Sundew.Base.Primitives.Time;
    using Sundew.Packaging.Source;
    using Sundew.Packaging.Staging;
    using Sundew.Packaging.Tool.Versioning.MsBuild;
    using Sundew.Packaging.Versioning;
    using Sundew.Packaging.Versioning.Commands;
    using Sundew.Packaging.Versioning.IO;

    /// <summary>
    /// Facade for getting the stage for publishing a package.
    /// </summary>
    public class StageBuildFacade
    {
        private const string LocalStageName = "none";
        private readonly ProjectPackageInfoProvider projectPackageInfoProvider;
        private readonly IPackageVersioner packageVersioner;
        private readonly INuGetSettingsInitializationCommand nuGetSettingsInitializationCommand;
        private readonly IDateTime dateTime;
        private readonly IFileSystem fileSystem;
        private readonly PackageVersionLogger packageVersionLogger;
        private readonly IStageBuildLogger exceptionReporter;
        private readonly LatestVersionSourcesCommand latestVersionSourcesCommand;

        /// <summary>
        /// Initializes a new instance of the <see cref="StageBuildFacade"/> class.
        /// </summary>
        /// <param name="projectPackageInfoProvider">The project package information provider.</param>
        /// <param name="packageVersioner">The package versioner.</param>
        /// <param name="nuGetSettingsInitializationCommand">The nu get settings initialization command.</param>
        /// <param name="dateTime">The date time.</param>
        /// <param name="fileSystem">The file system.</param>
        /// <param name="packagePublicationLogger">The package publication logger.</param>
        /// <param name="getVersionLogger">The get version logger.</param>
        public StageBuildFacade(
            ProjectPackageInfoProvider projectPackageInfoProvider,
            IPackageVersioner packageVersioner,
            INuGetSettingsInitializationCommand nuGetSettingsInitializationCommand,
            IDateTime dateTime,
            IFileSystem fileSystem,
            PackageVersionLogger packagePublicationLogger,
            IStageBuildLogger getVersionLogger)
        {
            this.projectPackageInfoProvider = projectPackageInfoProvider;
            this.packageVersioner = packageVersioner;
            this.nuGetSettingsInitializationCommand = nuGetSettingsInitializationCommand;
            this.dateTime = dateTime;
            this.fileSystem = fileSystem;
            this.packageVersionLogger = packagePublicationLogger;
            this.exceptionReporter = getVersionLogger;
            this.latestVersionSourcesCommand = new LatestVersionSourcesCommand(this.fileSystem);
        }

        /// <summary>
        /// Gets the version asynchronous.
        /// </summary>
        /// <param name="stageBuildVerb">The get version verb.</param>
        /// <returns>An async task.</returns>
        public Task GetVersionAsync(StageBuildVerb stageBuildVerb)
        {
            try
            {
                var packageInfo = this.projectPackageInfoProvider.GetPackageInfo(stageBuildVerb.ProjectFile, stageBuildVerb.Configuration);
                var workingDirectory = Path.GetFullPath(WorkingDirectorySelector.GetWorkingDirectory(stageBuildVerb.WorkingDirectory, this.fileSystem));
                var nuGetSettings = this.nuGetSettingsInitializationCommand.Initialize(workingDirectory, PackageSources.DefaultLocalSourceName, PackageSources.DefaultLocalSource);

                var selectedSource = StageSelector.Select(
                    stageBuildVerb.Stage,
                    stageBuildVerb.Production,
                    stageBuildVerb.Integration,
                    stageBuildVerb.Development,
                    nuGetSettings.LocalSourcePath,
                    stageBuildVerb.PrereleaseFormat,
                    null,
                    null,
                    LocalStageName,
                    stageBuildVerb.PrereleasePrefix,
                    stageBuildVerb.PrereleasePostfix,
                    nuGetSettings.DefaultSettings,
                    false,
                    true,
                    stageBuildVerb.NoStageProperties);

                var latestVersionSources = this.latestVersionSourcesCommand.GetLatestVersionSources(null, selectedSource, nuGetSettings, false, false);
                if (NuGetVersion.TryParse(packageInfo.PackageVersion, out var nuGetVersion))
                {
                    var packageVersion = this.packageVersioner.GetVersion(
                        packageInfo.PackageId,
                        nuGetVersion,
                        stageBuildVerb.VersionFormat,
                        stageBuildVerb.ForceVersion,
                        stageBuildVerb.VersioningMode,
                        selectedSource,
                        latestVersionSources,
                        this.dateTime.UtcNow,
                        stageBuildVerb.Metadata,
                        null,
                        null ?? string.Empty);

                    var publishInfo = new PublishInfo(
                        selectedSource.StageName,
                        selectedSource.VersionStageName,
                        selectedSource.FeedSource,
                        selectedSource.PushSource,
                        selectedSource.ApiKey,
                        selectedSource.SymbolsPushSource,
                        selectedSource.SymbolsApiKey,
                        selectedSource.IsEnabled,
                        packageVersion.ToNormalizedString(),
                        packageVersion.ToFullString(),
                        packageVersion.Metadata);

                    this.packageVersionLogger.Log(stageBuildVerb.OutputFormats, packageInfo.PackageId, publishInfo, workingDirectory, string.Empty, selectedSource.Properties);
                    return Task.CompletedTask;
                }

                this.exceptionReporter.ReportMessage($"Could not parse package version: {packageInfo.PackageVersion}");
            }
            catch (Exception e)
            {
                this.exceptionReporter.Exception(e);
            }

            return Task.CompletedTask;
        }
    }
}
