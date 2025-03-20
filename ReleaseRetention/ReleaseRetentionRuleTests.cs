using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using Moq;
using ReleaseRetention;

namespace ReleaseRetentionTests
{
    [TestClass]
    public class ReleaseRetentionServiceTests
    {
        private Mock<ILogger<ReleaseRetentionService>> _serviceLoggerMock;
        private Mock<ILogger<KeepMostRecentReleasesStrategy>> _strategyLoggerMock;
        private KeepMostRecentReleasesStrategy _strategy;
        private ReleaseRetentionService _service;

        // Constructor injection
        public ReleaseRetentionServiceTests()
        {
            _serviceLoggerMock = new Mock<ILogger<ReleaseRetentionService>>();
            _strategyLoggerMock = new Mock<ILogger<KeepMostRecentReleasesStrategy>>();

            _strategy = new KeepMostRecentReleasesStrategy(1, _strategyLoggerMock.Object);
            _service = new ReleaseRetentionService(_serviceLoggerMock.Object, _strategy);
        }

        [TestMethod]
        public void DetermineReleasesToKeep_ShouldKeepMostRecentRelease()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };
            var release2 = new Release { Id = "R2", Version = "1.0.1", ProjectId = "P1", Created = DateTime.Parse("2024-01-02T08:00:00") };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R2", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") }
            };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project },
                new List<DeploymentEnvironment> { environment },
                new List<Release> { release1, release2 },
                deployments
            );

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.Contains(release2));
        }

        [TestMethod]
        public void DetermineReleasesToKeep_NoDeployments_ShouldReturnEmpty()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.UtcNow };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project },
                new List<DeploymentEnvironment> { environment },
                new List<Release> { release1 },
                new List<Deployment>() // no deployments
            );

            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetermineReleasesToKeep_MultipleEnvironments_ShouldKeepMostRecentPerEnvironment()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var env1 = new DeploymentEnvironment { Id = "E1", Name = "Staging" };
            var env2 = new DeploymentEnvironment { Id = "E2", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };
            var release2 = new Release { Id = "R2", Version = "1.0.1", ProjectId = "P1", Created = DateTime.Parse("2024-01-02T08:00:00") };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R2", EnvironmentId = "E2", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") }
            };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project },
                new List<DeploymentEnvironment> { env1, env2 },
                new List<Release> { release1, release2 },
                deployments
            );

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(release1));
            Assert.IsTrue(result.Contains(release2));
        }

        [TestMethod]
        public void DetermineReleasesToKeep_ShouldRespectKeepLimit()
        {
            // Change strategy to keep last 2 releases
            _strategy = new KeepMostRecentReleasesStrategy(2, _strategyLoggerMock.Object);
            _service = new ReleaseRetentionService(_serviceLoggerMock.Object, _strategy);

            var project = new Project { Id = "P1", Name = "Project-1" };
            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var releases = new List<Release>
            {
                new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") },
                new Release { Id = "R2", Version = "1.0.1", ProjectId = "P1", Created = DateTime.Parse("2024-01-02T08:00:00") },
                new Release { Id = "R3", Version = "1.0.2", ProjectId = "P1", Created = DateTime.Parse("2024-01-03T08:00:00") }
            };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R2", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") },
                new Deployment { Id = "D3", ReleaseId = "R3", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-03T12:00:00") }
            };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project },
                new List<DeploymentEnvironment> { environment },
                releases,
                deployments
            );

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.Contains(releases[1])); // R2
            Assert.IsTrue(result.Contains(releases[2])); // R3
        }

        [TestMethod]
        public void DetermineReleasesToKeep_ReleaseWithoutDeployments_IsNotKept()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };

            var deployments = new List<Deployment>(); // No deployments for release1

            var result = _service.DetermineReleasesToKeep(new List<Project> { project }, new List<DeploymentEnvironment> { environment }, new List<Release> { release1 }, deployments);

            // Assert that no releases are kept
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetermineReleasesToKeep_StrategyWithZeroToKeep_ResultsInNothing()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };
            var release2 = new Release { Id = "R2", Version = "1.0.1", ProjectId = "P1", Created = DateTime.Parse("2024-01-02T08:00:00") };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R2", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") }
            };

            // Set the strategy to keep 0 releases
            var strategyWithZero = new KeepMostRecentReleasesStrategy(0, _strategyLoggerMock.Object);
            var serviceWithZero = new ReleaseRetentionService(_serviceLoggerMock.Object, strategyWithZero);

            var result = serviceWithZero.DetermineReleasesToKeep(new List<Project> { project }, new List<DeploymentEnvironment> { environment }, new List<Release> { release1, release2 }, deployments);

            // Assert that no releases are kept
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void DetermineReleasesToKeep_DeploymentsAcrossProjects_AreNotMixed()
        {
            var project1 = new Project { Id = "P1", Name = "Project-1" };
            var project2 = new Project { Id = "P2", Name = "Project-2" };

            var environment = new DeploymentEnvironment { Id = "E1", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };
            var release2 = new Release { Id = "R2", Version = "1.0.1", ProjectId = "P1", Created = DateTime.Parse("2024-01-02T08:00:00") };
            var release3 = new Release { Id = "R3", Version = "1.0.0", ProjectId = "P2", Created = DateTime.Parse("2024-01-01T08:00:00") };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R2", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") },
                new Deployment { Id = "D3", ReleaseId = "R3", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") }
            };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project1, project2 },
                new List<DeploymentEnvironment> { environment },
                new List<Release> { release1, release2, release3 },
                deployments
            );

            // Assert that the deployments and releases are not mixed across projects
            Assert.AreEqual(2, result.Count); // There should be 2 releases: one from each project
            Assert.IsTrue(result.Contains(release2)); // The latest release for P1
            Assert.IsTrue(result.Contains(release3)); // The latest release for P2
        }

        [TestMethod]
        public void DeploymentsWithSameReleaseInMultipleEnvironments()
        {
            var project = new Project { Id = "P1", Name = "Project-1" };
            var env1 = new DeploymentEnvironment { Id = "E1", Name = "Staging" };
            var env2 = new DeploymentEnvironment { Id = "E2", Name = "Production" };

            var release1 = new Release { Id = "R1", Version = "1.0.0", ProjectId = "P1", Created = DateTime.Parse("2024-01-01T08:00:00") };

            var deployments = new List<Deployment>
            {
                new Deployment { Id = "D1", ReleaseId = "R1", EnvironmentId = "E1", DeployedAt = DateTime.Parse("2024-01-01T10:00:00") },
                new Deployment { Id = "D2", ReleaseId = "R1", EnvironmentId = "E2", DeployedAt = DateTime.Parse("2024-01-02T12:00:00") }
            };

            var result = _service.DetermineReleasesToKeep(
                new List<Project> { project },
                new List<DeploymentEnvironment> { env1, env2 },
                new List<Release> { release1 },
                deployments
            );

            Assert.AreEqual(1, result.Count); // Only one release, R1
            Assert.IsTrue(result.Contains(release1));
        }
    }
}
