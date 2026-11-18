using System;
using System.IO;
using NUnit.Framework;

namespace Rebellion.Tests.Editor.Build.CodeGen
{
    [TestFixture]
    public sealed class SimulationBuildIdentityInputsTests
    {
        private string _projectDirectory;

        /// <summary>
        /// Creates the isolated project inputs used by each digest comparison.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _projectDirectory = Path.Combine(
                Path.GetTempPath(),
                nameof(SimulationBuildIdentityInputsTests) + Guid.NewGuid().ToString("N")
            );
            WriteInitialInputs(_projectDirectory);
        }

        /// <summary>
        /// Removes the temporary project created by this fixture.
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_projectDirectory))
                Directory.Delete(_projectDirectory, true);
        }

        [Test]
        public void ComputeIdentity_UtilityAssemblyDefinitionChanges_ChangesDigest()
        {
            string before = SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory);
            WriteInput(
                _projectDirectory,
                "Assets/Scripts/Util/Rebellion.Util.asmdef",
                "{\"name\":\"Rebellion.Util\",\"noEngineReferences\":true}"
            );

            Assert.AreNotEqual(
                before,
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory)
            );
        }

        [Test]
        public void ComputeIdentity_NestedModeAssemblyAdded_ChangesDigest()
        {
            string before = SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory);
            WriteInput(
                _projectDirectory,
                "Assets/Scripts/Tactical/Core/Rebellion.Tactical.Core.asmdef",
                "{\"name\":\"Rebellion.Tactical.Core\"}"
            );

            Assert.AreNotEqual(
                before,
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory)
            );
        }

        [Test]
        public void ComputeIdentity_InputAssemblyDefinitionChanges_ChangesDigest()
        {
            string before = SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory);
            WriteInput(
                _projectDirectory,
                "Assets/Input/InputActions.asmdef",
                "{\"name\":\"Rebellion.InputActions\",\"allowUnsafeCode\":true}"
            );

            Assert.AreNotEqual(
                before,
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory)
            );
        }

        [Test]
        public void ComputeIdentity_NonCompilationFileAdded_PreservesDigest()
        {
            string before = SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory);
            WriteInput(_projectDirectory, "Assets/Scripts/Notes.txt", "Developer notes");

            Assert.AreEqual(
                before,
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory)
            );
        }

        [Test]
        public void ComputeIdentity_DifferentProjectRoots_ProducesSameDigest()
        {
            string otherProject = Path.Combine(_projectDirectory, "OtherProject");
            WriteInitialInputs(otherProject);

            Assert.AreEqual(
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory),
                SimulationBuildIdentityInputs.ComputeIdentity(otherProject)
            );
        }

        [Test]
        public void ComputeIdentity_LineEndingChanges_PreservesDigest()
        {
            string before = SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory);
            WriteInput(_projectDirectory, "Assets/Scripts/Example.cs", "class Example {}\r\n");

            Assert.AreEqual(
                before,
                SimulationBuildIdentityInputs.ComputeIdentity(_projectDirectory)
            );
        }

        /// <summary>
        /// Writes the minimum source and dependency inputs for an isolated project.
        /// </summary>
        /// <param name="projectDirectory">The isolated project's directory.</param>
        private static void WriteInitialInputs(string projectDirectory)
        {
            WriteInput(projectDirectory, "Assets/Scripts/Example.cs", "class Example {}\n");
            WriteInput(
                projectDirectory,
                "Assets/Scripts/GameAssembly.asmdef",
                "{\"name\":\"GameAssembly\"}"
            );
            WriteInput(
                projectDirectory,
                "Assets/Scripts/Util/Rebellion.Util.asmdef",
                "{\"name\":\"Rebellion.Util\",\"noEngineReferences\":false}"
            );
            WriteInput(projectDirectory, "Packages/manifest.json", "{}");
            WriteInput(
                projectDirectory,
                "Assets/Input/InputActions.asmdef",
                "{\"name\":\"Rebellion.InputActions\"}"
            );
            WriteInput(
                projectDirectory,
                "Assets/Input/PlayerInputActions.cs",
                "class PlayerInputActions {}\n"
            );
            WriteInput(projectDirectory, "Packages/packages-lock.json", "{}");
            WriteInput(projectDirectory, "ProjectSettings/ProjectVersion.txt", "Unity version");
        }

        /// <summary>
        /// Writes one project input without depending on the process working directory.
        /// </summary>
        /// <param name="projectDirectory">The isolated project's directory.</param>
        /// <param name="relativePath">The input path relative to the project.</param>
        /// <param name="contents">The input's contents.</param>
        private static void WriteInput(
            string projectDirectory,
            string relativePath,
            string contents
        )
        {
            string path = Path.Combine(projectDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, contents);
        }
    }
}
