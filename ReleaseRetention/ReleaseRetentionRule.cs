using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Microsoft.Extensions.Logging;

namespace ReleaseRetention
{
    // Model classes representing Project, DeploymentEnvironment, Release, and Deployment
    public class Project
    {
        public required string Id { get; set; }
        public required string Name { get; set; }

        public override string ToString() => $"{GetType().Name}(Id='{Id}', Name='{Name}')";
    }

    public class DeploymentEnvironment
    {
        public required string Id { get; set; }
        public required string Name { get; set; }

        public override string ToString() => $"{GetType().Name}(Id='{Id}', Name='{Name}')";
    }

    public class Release
    {
        public required string Id { get; set; }
        public required string Version { get; set; }
        public required string ProjectId { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime Created { get; set; }

        public override string ToString() => $"{GetType().Name}(Id='{Id}', Version='{Version}', ProjectId='{ProjectId}', Created='{Created}')";
    }

    public class Deployment
    {
        public required string Id { get; set; }
        public required string ReleaseId { get; set; }
        public required string EnvironmentId { get; set; }

        [JsonConverter(typeof(IsoDateTimeConverter))]
        public DateTime DeployedAt { get; set; }

        public override string ToString() => $"{GetType().Name}(Id='{Id}', ReleaseId='{ReleaseId}', EnvironmentId='{EnvironmentId}', DeployedAt='{DeployedAt}')";
    }

    // Interface for retention strategy
    public interface IReleaseRetentionStrategy
    {
        List<Release> DetermineReleasesToKeep(
            Project project,
            DeploymentEnvironment environment,
            List<Release> releases,
            List<Deployment> deployments);
    }

    // Strategy implementation to keep the most recent releases
    public class KeepMostRecentReleasesStrategy : IReleaseRetentionStrategy
    {
        private readonly int _numToKeep;
        private readonly ILogger _logger; // Single logger for simplicity

        public KeepMostRecentReleasesStrategy(int numToKeep, ILogger logger)
        {
            _numToKeep = numToKeep;
            _logger = logger;
        }

        public List<Release> DetermineReleasesToKeep(
            Project project,
            DeploymentEnvironment environment,
            List<Release> releases,
            List<Deployment> deployments)
        {
            var recentReleaseIds = deployments
                .Where(d => d.EnvironmentId == environment.Id && releases.Any(r => r.Id == d.ReleaseId))
                .OrderByDescending(d => d.DeployedAt)
                .Take(_numToKeep)
                .Select(d => d.ReleaseId)
                .ToHashSet();

            var keptReleases = releases
                .Where(r => recentReleaseIds.Contains(r.Id))
                .ToList();

            foreach (var release in keptReleases)
            {
                _logger.LogInformation($"Keeping release {release.Version} for project {project.Name} in environment {environment.Name}.");
            }

            return keptReleases;
        }
    }

    // Service to manage release retention logic
    public class ReleaseRetentionService
    {
        private readonly ILogger _logger;
        private readonly IReleaseRetentionStrategy _strategy;

        public ReleaseRetentionService(ILogger logger, IReleaseRetentionStrategy strategy)
        {
            _logger = logger;
            _strategy = strategy;
        }

        public HashSet<Release> DetermineReleasesToKeep(
            List<Project> projects,
            List<DeploymentEnvironment> environments,
            List<Release> releases,
            List<Deployment> deployments)
        {
            var releasesToKeep = new HashSet<Release>();

            foreach (var project in projects)
            {
                foreach (var environment in environments)
                {
                    var projectReleases = releases.Where(r => r.ProjectId == project.Id).ToList();
                    var environmentDeployments = deployments
                        .Where(d => d.EnvironmentId == environment.Id && projectReleases.Any(r => r.Id == d.ReleaseId))
                        .ToList();

                    var releasesForEnv = _strategy.DetermineReleasesToKeep(project, environment, projectReleases, environmentDeployments);
                    releasesToKeep.UnionWith(releasesForEnv);
                }
            }

            return releasesToKeep;
        }
    }

    // Class to read data from a file
    public class DataReader
    {
        public static List<T> LoadData<T>(string filePath)
        {
            try
            {
                string jsonData = System.IO.File.ReadAllText(filePath);
                List<T>? result = JsonConvert.DeserializeObject<List<T>>(jsonData);
                return result ?? new List<T>();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error reading or deserializing file {filePath}: {ex.Message}");
                return new List<T>();
            }
        }
    }
}