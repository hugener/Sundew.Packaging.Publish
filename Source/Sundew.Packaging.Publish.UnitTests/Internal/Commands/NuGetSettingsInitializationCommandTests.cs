﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="NuGetSettingsInitializationCommandTests.cs" company="Hukano">
// Copyright (c) Hukano. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

namespace Sundew.Packaging.Publish.UnitTests.Internal.Commands
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using FluentAssertions;
    using Moq;
    using NuGet.Configuration;
    using Sundew.Packaging.Versioning.Commands;
    using Sundew.Packaging.Versioning.IO;
    using Sundew.Packaging.Versioning.NuGet.Configuration;
    using Xunit;

    public class NuGetSettingsInitializationCommandTests
    {
        private const string ASolutionDirText = "ASolutionDirText";
        private const string ALocalSourceNameText = "ALocalSourceName";
        private const string ADefaultLocalSourceText = "ADefaultLocalSourceText";
        private const string ExpectedLocalSourceText = "ExpectedLocalSourceText";
        private readonly ISettings settings;
        private readonly ISettingsFactory settingsFactory;
        private readonly NuGetSettingsInitializationCommand testee;
        private readonly ISettings defaultSettings;

        public NuGetSettingsInitializationCommandTests()
        {
            this.settings = New.Mock<ISettings>();
            this.defaultSettings = New.Mock<ISettings>();
            this.settingsFactory = New.Mock<ISettingsFactory>();
            this.testee = new NuGetSettingsInitializationCommand(this.settingsFactory);
            this.settingsFactory.Setup(x => x.Create(It.IsAny<string>(), It.IsAny<string>(), false)).Returns(this.settings);
            this.settingsFactory.Setup(x => x.LoadSpecificSettings(It.IsAny<string>(), NuGetSettingsInitializationCommand.NuGetConfigFileName)).Returns(this.settings);
            this.settingsFactory.Setup(x => x.LoadDefaultSettings(ASolutionDirText)).Returns(this.defaultSettings);
        }

        [Fact]
        public void Add_When_LocalSourceNameDoesNotExist_Then_AddOrUpdateAndSaveToDiskShouldBeCalled()
        {
            this.defaultSettings.Setup(x => x.GetConfigFilePaths()).Returns(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"NuGet\NuGet.Config") });

            var result = this.testee.Initialize(ASolutionDirText, ALocalSourceNameText, ADefaultLocalSourceText);

            this.settings.Verify(x => x.AddOrUpdate(NuGetSettingsInitializationCommand.PackageSourcesText, It.Is<AddItem>(x => x.Key == ALocalSourceNameText && x.Value == ADefaultLocalSourceText)), Times.Once);
            this.settings.Verify(x => x.SaveToDisk(), Times.Once);
            result.LocalSourcePath.Should().Be(ADefaultLocalSourceText);
        }

        [Fact]
        public void Add_When_LocalSourceNameDoesExist_Then_AddOrUpdateAndSaveToDiskShouldNotBeCalled()
        {
            this.ArrangeLocalSourceSetting();

            var result = this.testee.Initialize(ASolutionDirText, ALocalSourceNameText, ADefaultLocalSourceText);

            this.settings.Verify(x => x.AddOrUpdate(It.IsAny<string>(), It.IsAny<AddItem>()), Times.Never);
            this.settings.Verify(x => x.SaveToDisk(), Times.Never);
            result.LocalSourcePath.Should().Be(ExpectedLocalSourceText);
        }

        private void ArrangeLocalSourceSetting()
        {
            this.defaultSettings.Setup(x => x.GetSection(NuGetSettingsInitializationCommand.PackageSourcesText)).Returns(
                (SettingSection)typeof(VirtualSettingSection).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
                    .OrderByDescending(x => x.GetParameters().Length).First().Invoke(
                        new object[]
                        {
                            NuGetSettingsInitializationCommand.PackageSourcesText,
                            new Dictionary<string, string>(),
                            new List<SettingItem> { new AddItem(ALocalSourceNameText, ExpectedLocalSourceText) },
                        }));
        }
    }
}