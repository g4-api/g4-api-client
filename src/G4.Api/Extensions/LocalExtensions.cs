using G4.Converters;
using G4.Models;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;

namespace G4.Extensions
{
    internal static class LocalExtensions
    {
        // Provides JSON serialization options for custom serialization/deserialization handling.
        private static readonly JsonSerializerOptions s_jsonOptions = G4Utilities.NewJsonSettings(
            new ExceptionConverter(),
            new TypeConverter(),
            new MethodBaseConverter(),
            new DateTimeIso8601Converter());

        /// <summary>
        /// Adds or updates a <see cref="G4QueueModel"/> in the concurrent queue based on its group and identifier.
        /// </summary>
        /// <param name="queue">
        /// A concurrent dictionary where the key is the group identifier and the value is another concurrent dictionary
        /// mapping automation identifiers to <see cref="G4QueueModel"/> instances.
        /// </param>
        /// <param name="queueModel">The queue model to add or update.</param>
        public static void Add(this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue, G4QueueModel queueModel)
        {
            // Extract the progress status identifier from the queue model.
            var statusId = queueModel.ProgressStatus.Id;

            // Extract the automation instance from the queue model.
            var automation = queueModel.Automation;

            // Extract the progress status group identifier from the queue model.
            var statusGroupId = queueModel.ProgressStatus.GroupId;

            // Determine the effective group identifier: if the progress status group is not provided,
            // fall back to the automation's group identifier.
            var groupId = string.IsNullOrEmpty(statusGroupId) ? automation.GroupId : statusGroupId;

            // Determine the effective automation identifier: if the progress status ID is not provided,
            // fall back to the automation reference's ID.
            var id = string.IsNullOrEmpty(statusId) ? automation.Reference.Id : statusId;

            // Retrieve the active group from the queue. If it doesn't exist, create a new concurrent dictionary for the group.
            if (!queue.TryGetValue(groupId, out ConcurrentDictionary<string, G4QueueModel> group))
            {
                group = [];
                queue[groupId] = group;
            }

            // Set the status of the queue model to "Processing".
            queueModel.ProgressStatus.Status = G4QueueModel.QueueStatusCodes.Processing;

            // Add or update the queue model in the group using the determined automation identifier.
            group[id] = queueModel;
        }

        /// <summary>
        /// Marks a specific job within the automation queue as complete and updates the corresponding stage and queue statuses.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all stages and jobs.</param>
        /// <param name="args">The event arguments containing information about the job that has been completed.</param>
        /// <returns>An instance of <see cref="JobEventArgs"/> containing the updated job status, queue status, job reference, and stage status after marking the job as complete.</returns>
        public static JobEventArgs Complete(this AutomationQueueModel queueModel, JobEventArgs args)
        {
            // Extract the identifiers for the stage and job from the event arguments
            var stageId = args.Stage.Reference.Id;
            var jobId = args.Job.Reference.Id;

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Retrieve the status of the specific stage using the stage ID
            var stageStatus = queueStatus.StagesStatus[stageId];

            // Update the status of the job to "Complete"
            stageStatus.JobsStatus[jobId].Status = G4QueueModel.QueueStatusCodes.Complete;

            // Increment the count of completed jobs in the stage status,
            // ensuring it does not exceed the total number of jobs in the stage
            stageStatus.CompletedJobs = stageStatus.CompletedJobs >= args.Stage.Jobs.Count()
                ? args.Stage.Jobs.Count()
                : stageStatus.CompletedJobs + 1;

            // Calculate and update the stage's progress based on the number of completed jobs
            stageStatus.Progress = MeasureProgress(stageStatus, args.Stage);

            // Create and return a new JobStatusEventArgs object with the updated statuses
            return new()
            {
                JobStatus = stageStatus.JobsStatus[jobId],
                AutomationStatus = queueStatus,
                JobReference = jobId,
                StageStatus = stageStatus
            };
        }

        /// <summary>
        /// Marks a specific stage within the automation queue as complete and updates the corresponding queue statuses.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all stages and jobs.</param>
        /// <param name="args">The event arguments containing information about the stage that has been completed.</param>
        /// <returns>An instance of <see cref="StageEventArgs"/> containing the updated queue status, stage reference, and stage status after marking the stage as complete.</returns>
        public static StageEventArgs Complete(this AutomationQueueModel queueModel, StageEventArgs args)
        {
            // Retrieve the identifier for the stage from the event arguments
            var stageId = args.Stage.Reference.Id;

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Increment the count of completed stages in the queue status,
            // ensuring it does not exceed the total number of stages in the automation
            queueStatus.CompletedStages = queueStatus.CompletedStages >= args.Automation.Stages.Count()
                ? args.Automation.Stages.Count()
                : queueStatus.CompletedStages + 1;

            // Update the status of the specific stage to "Complete"
            queueStatus.StagesStatus[stageId].Status = G4QueueModel.QueueStatusCodes.Complete;

            // Measure and update the overall progress of automation stages
            queueStatus.Progress = MeasureProgress(queueStatus, args.Automation);

            // Create and return a new StageStatusEventArgs object with the updated statuses
            return new()
            {
                AutomationStatus = queueStatus,
                StageReference = stageId,
                StageStatus = queueStatus.StagesStatus[stageId]
            };
        }

        /// <summary>
        /// Marks a specific automation as complete within the automation queue and updates the corresponding queue statuses.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all automations.</param>
        /// <param name="args">The event arguments containing information about the automation invocation that triggered the status update.</param>
        /// <returns>An instance of <see cref="AutomationEventArgs"/> containing the updated queue status and automation reference after marking the automation as complete.</returns>
        public static AutomationEventArgs Complete(this AutomationQueueModel queueModel, AutomationEventArgs args)
        {
            // Determine the group ID. If GroupId is not provided, default to the automation reference ID.
            var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
                ? args.Automation.Reference.Id
                : args.Automation.GroupId;

            // Store the Automation object for easier access.
            var automation = args.Automation;

            // Retrieve the automation reference ID.
            var reference = automation.Reference.Id;

            // Access the overall queue status from the automation queue model.
            var queueStatus = queueModel.Status.ProgressStatus;

            // Update the performance data in the queue status model by creating a copy of the performance point.
            queueStatus.PerformancePoint = args.Response.PerformancePoint.Copy<G4AutomationPerformancePointModel>();

            // Update the status code of the automation to "Complete".
            queueStatus.Status = G4QueueModel.QueueStatusCodes.Complete;

            // Create and return a new AutomationStatusEventArgs object with the updated statuses.
            return new()
            {
                AutomationGroup = groupId,
                AutomationReference = reference,
                AutomationStatus = queueStatus
            };
        }

        /// <summary>
        /// Marks a specific rule within a job as complete and updates the corresponding job and queue statuses.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all jobs and stages.</param>
        /// <param name="args">The event arguments containing information about the rule that has been completed.</param>
        /// <returns>An instance of <see cref="JobEventArgs"/> containing the updated job status, queue status, job reference, and stage status after marking the rule as complete.</returns>
        public static JobEventArgs Complete(this AutomationQueueModel queueModel, RuleEventArgs args)
        {
            // Extract the identifiers for the stage, job, and rule from the event arguments
            var stageId = args.Rule.Reference.JobReference.StageReference.Id;
            var jobId = args.Rule.Reference.JobReference.Id;
            var ruleId = args.Rule.Reference.Id;

            // Get the total number of rules for the job
            var totalRules = args.Rule.Rules.GetRulesCount();

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Retrieve the status of the specific stage using the stage ID
            var stageStatus = queueStatus.StagesStatus[stageId];

            // Retrieve the status of the specific job within the stage using the job ID
            var jobStatus = stageStatus.JobsStatus[jobId];

            // Increment the count of completed rules in the job status,
            // ensuring it does not exceed the total number of rules
            jobStatus.CompletedRules = jobStatus.CompletedRules >= totalRules
                ? args.Rule.Rules.GetRulesCount()
                : jobStatus.CompletedRules + 1;

            // Retrieve the status of the specific rule using the rule ID
            var ruleStatus = jobStatus.RulesStatus[ruleId];

            // Update the status of the rule to "Complete"
            ruleStatus.Status = G4QueueModel.QueueStatusCodes.Complete;

            // Measure and update the progress of rule completion for the job status
            jobStatus.Progress = jobStatus.MeasureProgress();

            // Measure and update the progress of rule completion for the rule status
            ruleStatus.Progress = ruleStatus.MeasureProgress();

            // Create and return a new JobStatusEventArgs object with the updated statuses
            return new()
            {
                JobStatus = stageStatus.JobsStatus[jobId],
                AutomationStatus = queueStatus,
                StageStatus = stageStatus
            };
        }

        /// <summary>
        /// Validates the provided plugin manifest to ensure there are no circular references within its rules.
        /// </summary>
        /// <param name="manifest">The plugin manifest to be validated.</param>
        /// <exception cref="InvalidOperationException">Thrown when a circular reference is detected within the plugin's rules.</exception>
        public static void ConfirmTemplate(this IG4PluginManifest manifest)
        {
            // Combine the plugin's key with its aliases to form a list of identifiers to check for circular references
            var includes = new[] { manifest.Key }.Concat(manifest.Aliases ?? []);

            // Local function to recursively confirm that a rule does not reference any of the included plugin identifiers, preventing circular dependencies.
            static void ConfirmRule(G4RuleModelBase rule, IEnumerable<string> includes)
            {
                // Check if the current rule's plugin name is in the list of included identifiers
                if (includes.Contains(rule.PluginName, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Circular reference detected: The plugin '{rule.PluginName}' is referencing itself, directly or indirectly.");
                }

                // If the rule has no sub-rules, there is nothing further to validate
                if (rule.Rules?.Any() != true)
                {
                    return;
                }

                // Recursively validate each sub-rule to ensure no circular references exist
                foreach (var subRule in rule.Rules)
                {
                    ConfirmRule(subRule, includes);
                }
            }

            // Iterate through each top-level rule in the plugin manifest and validate it
            foreach (var rule in manifest.Rules)
            {
                ConfirmRule(rule, includes);
            }
        }

        /// <summary>
        /// Calculates the progress of a stage based on the total number of jobs and the number of completed jobs.
        /// </summary>
        /// <param name="stageStatus">The stage status model containing job information.</param>
        /// <param name="stage">The stage model defining the jobs within the stage.</param>
        /// <returns> A <see cref="double"/> representing the progress percentage. Returns <c>0.0</c> if no jobs are completed to avoid division by zero.</returns>
        public static double MeasureProgress(this G4AutomationStatusModel.StageStatusModel stageStatus, G4StageModel stage)
        {
            // Retrieve the total number of jobs in the stage
            var jobs = stage.Jobs.Count();

            // Count the number of jobs that have been completed within the stage
            var complete = stageStatus
                .JobsStatus
                .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

            // Calculate and return the progress as a percentage
            return complete == 0 ? 0D : (double)complete / jobs * 100;
        }

        /// <summary>
        /// Calculates the progress of a job based on the total number of rules and the number of completed rules.
        /// </summary>
        /// <param name="jobStatus">The job status model containing rule information.</param>
        /// <returns>A <see cref="double"/> representing the progress percentage. Returns <c>0.0</c> if no rules are completed to avoid division by zero.
        /// </returns>
        public static double MeasureProgress(this G4AutomationStatusModel.JobStatusModel jobStatus)
        {
            // Get the total number of rules for the job
            var rules = jobStatus.TotalRules;

            // Count the number of completed rules
            var complete = jobStatus
                .RulesStatus
                .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

            // Calculate the progress based on the number of completed rules
            return complete == 0 ? 0D : (double)complete / rules * 100;
        }

        /// <summary>
        /// Calculates the progress of a plugin based on the total number of rules and the number of completed rules.
        /// </summary>
        /// <param name="ruleStatus">The plugin status model containing rule information.</param>
        /// <returns>A <see cref="double"/> representing the progress percentage. Returns <c>0.0</c> if no rules are completed to avoid division by zero.
        /// </returns>
        public static double MeasureProgress(this G4AutomationStatusModel.PluginStatusModel ruleStatus)
        {
            // Get the total number of rules
            var rules = ruleStatus.TotalRules;

            // Count the number of completed rules
            var complete = ruleStatus
                .RulesStatus
                .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

            // Calculate the progress based on the number of completed rules
            return complete == 0 ? 0D : (double)complete / rules * 100;
        }

        /// <summary>
        /// Calculates the progress of an entire queue based on the total number of automation stages and the number of completed stages.
        /// </summary>
        /// <param name="queueStatus">The queue status model containing stage information.</param>
        /// <param name="automation">The automation model defining the stages.</param>
        /// <returns>A <see cref="double"/> representing the progress percentage. Returns <c>0.0</c> if no stages are completed to avoid division by zero.
        /// </returns>
        public static double MeasureProgress(this G4AutomationStatusModel queueStatus, G4AutomationModel automation)
        {
            // Total number of stages in the automation
            var stages = automation.Stages.Count();

            // Number of completed stages
            var complete = queueStatus
                .StagesStatus
                .Count(i => i.Value.Status == G4QueueModel.QueueStatusCodes.Complete);

            // Calculate progress as a percentage
            return complete == 0 ? 0D : (double)complete / stages * 100;
        }

        /// <summary>
        /// Creates a new automation response based on a collection of automation responses.
        /// </summary>
        /// <param name="responseCollection">A collection of tuples containing group IDs and their corresponding automation response models.</param>
        /// <returns>
        /// A dictionary where each key is a group ID and the value is the merged automation response model for that group.
        /// </returns>
        public static IDictionary<string, G4AutomationResponseModel> NewAutomationResponse(
            this IEnumerable<(string GroupId, G4AutomationResponseModel Response)> responseCollection)
        {
            // Group responses by their group ID to facilitate merging
            var responseGroups = responseCollection.GroupBy(i => i.GroupId);

            // Initialize a dictionary to store the final merged automation responses
            var results = new Dictionary<string, G4AutomationResponseModel>(StringComparer.OrdinalIgnoreCase);

            // Iterate through each group of responses and merge them into the results dictionary
            foreach (var responseGroup in responseGroups)
            {
                // Create a new automation response by merging the group's responses
                var (key, response) = NewAutomationResponse(responseGroup);

                // Add the merged response to the results dictionary using the group key
                results[key] = response;
            }

            // Return the dictionary containing all merged automation responses
            return results;
        }

        /// <summary>
        /// Creates a new automation response by merging sessions and calculating performance metrics based on a group of responses.
        /// </summary>
        /// <param name="group">A grouping of automation responses where the key represents the group identifier and the value is a tuple containing the response key and the automation response model.</param>
        /// <returns>
        /// A tuple containing the group ID and the newly created <see cref="G4AutomationResponseModel"/> which includes merged sessions and calculated performance metrics.
        /// If the group is empty, it returns a tuple with the group key and an empty response model.
        /// </returns>
        public static (string Key, G4AutomationResponseModel Response) NewAutomationResponse(
            this IGrouping<string, (string Key, G4AutomationResponseModel Response)> group)
        {
            // Dictionary to store merged sessions from all responses in the group
            var sessions = new Dictionary<string, G4ResponseModel>(StringComparer.OrdinalIgnoreCase);

            // Iterate through each response in the group and merge their sessions into the sessions dictionary
            foreach (var item in group.Select(i => i.Response))
            {
                sessions.Merge(item.Sessions);
            }

            // Calculate the aggregated performance metrics based on the merged sessions
            var performancePoint = new G4AutomationPerformancePointModel
            {
                AuthenticationTime = sessions.Average(i => ((G4AutomationPerformancePointModel)i.Value.PerformancePoint).AuthenticationTime),
                End = sessions.Select(i => i.Value.PerformancePoint.End).OrderDescending().First(),
                RunTime = sessions.Average(i => i.Value.PerformancePoint.RunTime),
                SessionTime = sessions.Average(i => ((G4AutomationPerformancePointModel)i.Value.PerformancePoint).SessionTime),
                SetupDelegationTime = sessions.Average(i => i.Value.PerformancePoint.SetupDelegationTime),
                SetupTime = sessions.Average(i => i.Value.PerformancePoint.SetupTime),
                Start = sessions.Select(i => i.Value.PerformancePoint.Start).Order().First(),
                TeardownDelegationTime = sessions.Average(i => i.Value.PerformancePoint.TeardownDelegationTime),
                TeardownTime = sessions.Average(i => i.Value.PerformancePoint.TeardownTime),
                Timeouts = sessions.Average(i => i.Value.PerformancePoint.Timeouts)
            };

            // Create a new automation response model with merged data providers, calculated performance points, and merged sessions
            var response = new G4AutomationResponseModel
            {
                DataProvider = group.SelectMany(i => i.Response.DataProvider),
                PerformancePoint = performancePoint,
                Sessions = sessions,
            };

            // Return the group key along with the newly created automation response model
            return (group.Key, response);
        }

        /// <summary>
        /// Generates new automations along with their associated data providers from the given automation model.
        /// </summary>
        /// <param name="automation">The base automation model.</param>
        /// <returns>An enumerable collection of tuples containing new automation models and their associated data providers.</returns>
        public static IEnumerable<(G4AutomationModel Automation, IDictionary<string, object> DataProvider)> NewAutomations(
                this G4AutomationModel automation)
        {
            // Call the overload with the default JSON serialization options
            return NewAutomations(automation, s_jsonOptions);
        }

        /// <summary>
        /// Generates new automation models based on the provided automation and options.
        /// </summary>
        /// <param name="automation">The original automation model.</param>
        /// <param name="options">Options for JSON serialization.</param>
        /// <returns>A collection of tuples containing the new automation models and their associated data providers.</returns>
        public static IEnumerable<(G4AutomationModel Automation, IDictionary<string, object> DataProvider)> NewAutomations(
            this G4AutomationModel automation, JsonSerializerOptions options)
        {
            // Default value containing the original automation and an empty data provider dictionary.
            var defaultValue = (automation, new Dictionary<string, object>());

            // Return the default value if the provided automation is null.
            if (automation == null)
            {
                return [defaultValue];
            }

            // Generate a unique group ID for the automation batch.
            var groupId = $"{Guid.NewGuid()}";

            // Assign the generated group ID to the automation model.
            automation.GroupId = groupId;

            // Retrieve the data source from the automation model.
            var dataSource = automation.DataSource;

            // Return the default value if the data source is null or has a default value.
            if (dataSource?.Source == default)
            {
                // Return the default value containing the initialized automation model.
                return [defaultValue];
            }

            // Create a new data table from the data source.
            var dataTable = NewDataTable(automation.DataSource);

            // Serialize the automation model and copy the data table.
            var automations = JsonSerializer.Serialize(automation, options).Copy(dataTable).ToList();

            // Initialize a list to hold the results.
            var results = new List<(G4AutomationModel Automation, IDictionary<string, object> DataProvider)>();

            // Iterate through each serialized string and data row.
            for (int i = 0; i < automations.Count; i++)
            {
                var (result, row) = automations[i];

                // Deserialize the string back into an automation model.
                var onAutomation = JsonSerializer.Deserialize<G4AutomationModel>(result, options);

                // Clear the data source for the new automation model.
                onAutomation.DataSource = null;

                // Assign the same group ID to each automation model.
                onAutomation.GroupId = groupId;

                // Set the iteration number for each automation model.
                onAutomation.Iteration = i + 1;

                // Add the data row to the automation model's context.
                onAutomation.Context["DataRow"] = row;

                // Set the iteration number for each rule in the automation model.
                onAutomation.SetIteration();

                // Add the new automation model and data provider dictionary to the results.
                results.Add((onAutomation, row.ConvertToDictionary()));
            }

            // Return the collection of new automation models and their data providers.
            return results;
        }

        /// <summary>
        /// Creates a new <see cref="DataTable"/> from the specified <see cref="G4DataProviderModel"/>.
        /// </summary>
        /// <param name="dataProvider">The data provider model used to create the <see cref="DataTable"/>.</param>
        /// <returns>A <see cref="DataTable"/> created from the specified data provider model.</returns>
        /// <exception cref="NotImplementedException">Thrown when the data provider type is not implemented.</exception>
        public static DataTable NewDataTable(this G4DataProviderModel dataProvider)
        {
            // Creates a DataTable from JSON data based on a data provider model.
            static DataTable NewFromJson(G4DataProviderModel dataProvider)
            {
                // Selects rows from a DataTable based on a filter expression.
                static DataTable SelectRows(DataTable dataTable, string filterExpression)
                {
                    // Check if the filter expression is empty
                    // If the filter expression is empty, return the original DataTable
                    if (filterExpression.Length == 0)
                    {
                        return dataTable;
                    }

                    // Select rows based on the filter expression
                    var rows = dataTable.Select(filterExpression);

                    // Create a new DataTable with the same structure as the original DataTable
                    var copiedDataTable = dataTable.Clone();

                    // Import selected rows into the new DataTable
                    foreach (DataRow row in rows)
                    {
                        copiedDataTable.ImportRow(row);
                    }

                    // Return the new DataTable containing the selected rows
                    return copiedDataTable;
                }

                // Check if the JSON data source is empty
                // If the JSON data source is empty, return an empty DataTable
                if (string.IsNullOrEmpty(dataProvider.Source))
                {
                    return new DataTable();
                }

                // Create a new DataTable to hold the JSON data
                var dataTable = new DataTable();

                // Ensure that the filter expression is not null
                dataProvider.Filter = (string.IsNullOrEmpty(dataProvider.Filter)) ? string.Empty : dataProvider.Filter;

                // Check if the JSON data source exists as a file
                // If the JSON data source exists as a file, read its contents
                if (System.IO.File.Exists(dataProvider.Source))
                {
                    dataProvider.Source = System.IO.File.ReadAllText(dataProvider.Source);
                }

                // Parse the JSON data into a JToken
                var token = Newtonsoft.Json.Linq.JToken.Parse(dataProvider.Source);

                // Check if the JSON data is an array
                // If the JSON data is not an array, return an empty DataTable
                if (token is not Newtonsoft.Json.Linq.JArray)
                {
                    return dataTable;
                }

                // Check if the JSON array is empty
                // If the JSON array is empty, return an empty DataTable
                if (((Newtonsoft.Json.Linq.JArray)token).Count == 0)
                {
                    return dataTable;
                }

                // Deserialize the JSON data into a DataTable
                dataTable = Newtonsoft.Json.JsonConvert.DeserializeObject<DataTable>(dataProvider.Source);

                // Select rows from the DataTable based on the filter expression
                return SelectRows(dataTable, filterExpression: dataProvider.Filter);
            }

            // Determine the data provider type and call the appropriate method
            return dataProvider.Type.ToUpper() switch
            {
                // If the data provider type is JSON, call the method to create a DataTable from JSON
                "JSON" => NewFromJson(dataProvider),

                // For any other data provider type, throw a NotImplementedException
                _ => throw new NotImplementedException()
            };
        }

        /// <summary>
        /// Creates and configures a new <see cref="ParallelOptions"/> instance based on the automation model's settings and a cancellation token.
        /// </summary>
        /// <param name="automation">The automation model containing settings for parallel execution.</param>
        /// <param name="cancellationToken">A cancellation token to observe while waiting for the parallel operations to complete.</param>
        /// <returns>A configured <see cref="ParallelOptions"/> instance with the specified cancellation token and maximum degree of parallelism.</returns>
        public static ParallelOptions NewParallelOptions(this G4AutomationModel automation, CancellationToken cancellationToken)
        {
            // Retrieve automation settings from the automation model
            var automationSettings = automation.Settings.AutomationSettings;

            // Determine the maximum degree of parallelism, ensuring it is at least 1
            var maxDegreeOfParallelism = automationSettings.MaxParallel < 1
                ? 1
                : automationSettings.MaxParallel;

            // Create and return a new ParallelOptions object with the configured settings
            return new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = maxDegreeOfParallelism
            };
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model.
        /// </summary>
        /// <param name="automation">The automation model associated with the queue.</param>
        /// <returns>A new queue model with the default status and an empty dictionary of properties.</returns>
        public static G4QueueModel NewQueueModel(this G4AutomationModel automation)
        {
            // Call the NewQueueModel method with the automation model, default status, and an empty dictionary of properties
            return NewQueueModel(
                automation,
                status: G4QueueModel.QueueStatusCodes.New,
                properties: new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model and status.
        /// </summary>
        /// <param name="automation">The automation model associated with the queue.</param>
        /// <param name="status">The status code of the queue.</param>
        /// <returns>A new queue model with the specified status.</returns>
        public static G4QueueModel NewQueueModel(this G4AutomationModel automation, int status)
        {
            // Call the NewQueueModel method with the automation model, specified status, and an empty dictionary of properties
            return NewQueueModel(
                automation,
                status,
                properties: new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model and properties, with the status set to "New".
        /// </summary>
        /// <param name="automation">The automation model associated with the queue.</param>
        /// <param name="properties">Additional properties associated with the queue.</param>
        /// <returns>A new queue model with the status set to "New".</returns>
        public static G4QueueModel NewQueueModel(this G4AutomationModel automation, IDictionary<string, object> properties)
        {
            // Call the NewQueueModel method with the automation model, "New" status, and provided properties
            return NewQueueModel(automation, status: G4QueueModel.QueueStatusCodes.New, properties);
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model, status, and properties.
        /// </summary>
        /// <param name="automation">The automation model associated with the queue.</param>
        /// <param name="status">The status of the queue.</param>
        /// <param name="properties">Additional properties associated with the queue.</param>
        /// <returns>A new queue model.</returns>
        public static G4QueueModel NewQueueModel(
            this G4AutomationModel automation, int status, IDictionary<string, object> properties)
        {
            // Create and return a new queue model with the specified parameters
            return new G4QueueModel
            {
                Created = DateTime.UtcNow,
                Automation = automation,
                Properties = properties,
                ProgressStatus = new G4AutomationStatusModel
                {
                    Status = status
                }
            };
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model, status, and properties.
        /// </summary>
        /// <param name="queue">A concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="queueModel">The dequeued <see cref="G4QueueModel"/> if successful; otherwise, null.</param>
        /// <returns><c>true</c> if a <see cref="G4QueueModel"/> was successfully dequeued; otherwise, <c>false</c>.</returns>
        public static bool TryDequeue(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            out G4QueueModel queueModel)
        {
            // Initialize the output parameter to null.
            queueModel = null;

            // Use LINQ to get the first group (outer dictionary key-value pair) that has a non-empty inner dictionary.
            var group = queue.FirstOrDefault(i => !i.Value.IsEmpty);

            // Check if a non-empty group was found.
            if (group.Equals(default(KeyValuePair<string, ConcurrentDictionary<string, G4QueueModel>>)))
            {
                return false;
            }

            // Use LINQ to get the first item (inner dictionary key-value pair) from the found group.
            var item = group.Value.FirstOrDefault();

            // Check if an item exists in the selected inner dictionary.
            if (item.Equals(default(KeyValuePair<string, G4QueueModel>)))
            {
                return false;
            }

            // Attempt to remove the selected item from the inner dictionary in a thread-safe manner.
            if (group.Value.TryRemove(item.Key, out queueModel))
            {
                // If the inner dictionary is now empty after removal, remove the entire group from the outer dictionary.
                if (group.Value.IsEmpty)
                {
                    queue.TryRemove(group.Key, out _);
                }

                // Return true if the removal of the item was successful.
                return true;
            }

            // Return false if the removal of the item was not successful.
            return false;
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model, status, and properties.
        /// </summary>
        /// <param name="queue">A concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="group">The key identifying the group to dequeue from.</param>
        /// <param name="queueModel">The dequeued <see cref="G4QueueModel"/> if successful; otherwise, null.</param>
        /// <returns><c>true</c> if a <see cref="G4QueueModel"/> was successfully dequeued; otherwise, <c>false</c>.</returns>
        public static bool TryDequeue(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            string group,
            out G4QueueModel queueModel)
        {

            // Initialize the output parameter.
            queueModel = null;

            // Check if the specified group exists and is non-empty.
            if (!queue.TryGetValue(group, out var innerQueue) || innerQueue.IsEmpty)
            {
                return false;
            }

            // Use LINQ to obtain the first item from the inner dictionary.
            var item = innerQueue.FirstOrDefault();

            // If no valid item is found, return false.
            if (item.Equals(default(KeyValuePair<string, G4QueueModel>)))
            {
                return false;
            }

            // Attempt to remove the selected item from the inner dictionary.
            if (innerQueue.TryRemove(item.Key, out queueModel))
            {
                // If the inner dictionary is empty after removal, remove the group from the outer dictionary.
                if (innerQueue.IsEmpty)
                {
                    queue.TryRemove(group, out _);
                }

                // Return true if the removal was successful.
                return true;
            }

            // Return false if the removal was not successful.
            return false;
        }

        /// <summary>
        /// Creates a new queue model based on the provided automation model, status, and properties.
        /// </summary>
        /// <param name="queue">A concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="group">The key identifying the group to dequeue from.</param>
        /// <param name="id">The identifier of the item to be dequeued.</param>
        /// <param name="queueModel">The dequeued <see cref="G4QueueModel"/> if successful; otherwise, null.</param>
        /// <returns><c>true</c> if a <see cref="G4QueueModel"/> was successfully dequeued; otherwise, <c>false</c>.</returns>
        public static bool TryDequeue(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            string group,
            string id,
            out G4QueueModel queueModel)
        {
            // Initialize the output parameter.
            queueModel = null;

            // Check if the specified group exists.
            if (!queue.TryGetValue(group, out var innerQueue))
            {
                return false;
            }

            // Attempt to remove the item with the specified id from the inner dictionary.
            if (innerQueue.TryRemove(id, out queueModel))
            {
                // If the inner dictionary becomes empty after removal, remove the group from the outer dictionary.
                if (innerQueue.IsEmpty)
                {
                    queue.TryRemove(group, out _);
                }

                // Return true if the item was successfully removed.
                return true;
            }

            // Return false if the specified item could not be removed.
            return false;
        }

        /// <summary>
        /// Creates a new automation response based on the current state of the automation queue.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of the automation.</param>
        /// <param name="response">The automation response model to be updated and returned.</param>
        /// <returns>
        /// A tuple containing the group ID associated with the automation and the updated automation response model.
        /// If the queue model is null, both elements of the tuple will be null.
        /// </returns>
        public static (string Key, G4AutomationResponseModel Response) NewResponse(this AutomationQueueModel queueModel, G4AutomationResponseModel response)
        {
            // If the queue model is null, return null for both key and response to indicate no update is possible
            if (queueModel == null)
            {
                return (null, null);
            }

            // Calculate the time elapsed since the automation was created and update the queue status
            queueModel.Status.TimeInQueue = DateTime.UtcNow - queueModel.Status.Created;

            // Assign the queue status properties to the response's data provider
            response.DataProvider = [queueModel.Status.Properties];
            response.Sessions.First().Value.DataProvider = [queueModel.Status.Properties];

            // Return the group ID and the updated response model as a tuple
            return (queueModel.Status.Automation.GroupId, response);
        }

        /// <summary>
        /// Updates the queue with the specified <see cref="G4QueueModel"/> by using its embedded group and id information.
        /// </summary>
        /// <param name="queue">The concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="newModel">The new <see cref="G4QueueModel"/> instance that should replace the existing one in the queue.</param>
        /// <returns><c>true</c> if the update was successful; otherwise, <c>false</c>.</returns>
        public static bool Update(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            G4QueueModel newModel)
        {
            // Check if the new model contains a valid group identifier.
            var isGroup = !string.IsNullOrEmpty(newModel?.Automation?.GroupId);

            // Check if the new model contains a valid id within its reference.
            var isId = !string.IsNullOrEmpty(newModel?.Automation?.Reference?.Id);

            // If either the group or the id is invalid, return false indicating failure.
            if (!isGroup || !isId)
            {
                return false;
            }

            // Call the overload that accepts group and id, passing the new model.
            return Update(
                queue,
                group: newModel.Automation.GroupId,
                id: newModel.Automation.Reference.Id,
                newModel);
        }

        /// <summary>
        /// Attempts to update the <see cref="G4QueueModel"/> with the specified identifier in the given group using the provided update function.
        /// </summary>
        /// <param name="queue">A concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="group">The key identifying the group.</param>
        /// <param name="id">The identifier of the item to update.</param>
        /// <param name="updateFactory">A function that receives the current <see cref="G4QueueModel"/> and returns its updated version.</param>
        /// <returns><c>true</c> if the item was successfully updated; otherwise, <c>false</c>.</returns>
        public static bool Update(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            string group,
            string id,
            Func<G4QueueModel, G4QueueModel> updateFactory)
        {
            // Check if the specified group exists.
            if (!queue.TryGetValue(group, out var innerQueue))
            {
                return false;
            }

            // Retrieve the current model for the given identifier.
            if (!innerQueue.TryGetValue(id, out var currentModel))
            {
                return false;
            }

            // Compute the updated model using the provided update function.
            var updatedModel = updateFactory(currentModel);

            // Atomically update the item in the inner dictionary.
            return innerQueue.TryUpdate(id, updatedModel, currentModel);
        }

        /// <summary>
        /// Attempts to update the <see cref="G4QueueModel"/> with the specified identifier in the given group
        /// by replacing it with the provided new model.
        /// </summary>
        /// <param name="queue">A concurrent dictionary where each key is a group containing a concurrent dictionary of <see cref="G4QueueModel"/> items.</param>
        /// <param name="group">The key identifying the group.</param><param name="id">The identifier of the item to update.</param>
        /// <param name="newModel">The new <see cref="G4QueueModel"/> instance to replace the current one.</param>
        /// <returns><c>true</c> if the item was successfully updated; otherwise, <c>false</c>.</returns>
        public static bool Update(
            this ConcurrentDictionary<string, ConcurrentDictionary<string, G4QueueModel>> queue,
            string group,
            string id,
            G4QueueModel newModel)
        {
            // Check if the specified group exists.
            if (!queue.TryGetValue(group, out var innerQueue))
            {
                return false;
            }

            // Retrieve the current model for the given identifier.
            if (!innerQueue.TryGetValue(id, out var currentModel))
            {
                return false;
            }

            // Atomically update the item in the inner dictionary by replacing it with the new model.
            return innerQueue.TryUpdate(id, newModel, currentModel);
        }

        /// <summary>
        /// Updates the status of a specific job within the automation queue based on the provided rule event arguments.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all jobs and stages.</param>
        /// <param name="args">The event arguments containing information about the rule that triggered the status update.</param>
        /// <returns>An instance of <see cref="RuleEventArgs"/> containing the updated job status, queue status, rule reference, rule status, and stage status after the update.</returns>
        public static RuleEventArgs UpdateStatus(this AutomationQueueModel queueModel, RuleEventArgs args)
        {
            // Extract the identifiers for the stage, job, and rule from the event arguments
            var stageId = args.Rule.Reference.JobReference.StageReference.Id;
            var jobId = args.Rule.Reference.JobReference.Id;
            var ruleId = args.Rule.Reference.Id;

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Retrieve the status of the specific stage using the stage ID
            var stageStatus = queueStatus.StagesStatus[stageId];

            // Retrieve the status of the specific job within the stage using the job ID
            var jobStatus = stageStatus.JobsStatus[jobId];

            // Decrement the number of pending rules for the job, ensuring it does not go below zero
            jobStatus.PendingRules = jobStatus.PendingRules <= 0
                ? 0
                : jobStatus.PendingRules - 1;

            // Update the status of the specific rule to 'Processing' and associate it with the job
            jobStatus.RulesStatus[ruleId] = G4AutomationStatusModel.PluginStatusModel.New(args.Rule, G4QueueModel.QueueStatusCodes.Processing);

            // Create and return a new RuleStatusEventArgs object with the updated statuses
            return new()
            {
                JobStatus = jobStatus,
                AutomationStatus = queueStatus,
                RuleReference = ruleId,
                RuleStatus = jobStatus.RulesStatus[ruleId],
                StageStatus = stageStatus
            };
        }

        /// <summary>
        /// Updates the status of a specific job within the automation queue based on the provided job event arguments.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all jobs and stages.</param>
        /// <param name="args">The event arguments containing information about the job that triggered the status update.</param>
        /// <returns>An instance of <see cref="JobEventArgs"/> containing the updated job status, queue status, and stage status after the update.</returns>
        public static JobEventArgs UpdateStatus(this AutomationQueueModel queueModel, JobEventArgs args)
        {
            // Extract the identifiers for the stage and job from the event arguments
            var stageId = args.Stage.Reference.Id;
            var jobId = args.Job.Reference.Id;

            // Calculate the total number of rules for the job
            var rules = args.Job.GetRulesCount();

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Retrieve the status of the specific stage using the stage ID
            var stageStatus = queueStatus.StagesStatus[stageId];

            // Decrement the count of pending jobs in the stage status, ensuring it does not go below zero
            stageStatus.PendingJobs = stageStatus.PendingJobs <= 0
                ? 0
                : stageStatus.PendingJobs - 1;

            // Initialize a new job status model for the job with updated details
            stageStatus.JobsStatus[jobId] = new G4AutomationStatusModel.JobStatusModel
            {
                CompletedRules = 0,
                Description = args.Stage.Description,
                Name = args.Stage.Name,
                PendingRules = rules,
                Progress = 0,
                Id = stageId,
                RulesStatus = [],
                Status = G4QueueModel.QueueStatusCodes.Processing,
                TotalRules = rules
            };

            // Create and return a new JobStatusEventArgs object with the updated statuses
            return new()
            {
                JobStatus = stageStatus.JobsStatus[jobId],
                AutomationStatus = queueStatus,
                StageStatus = stageStatus
            };
        }

        /// <summary>
        /// Updates the status of a specific stage within the automation queue based on the provided stage event arguments.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all stages and jobs.</param>
        /// <param name="args">The event arguments containing information about the stage that triggered the status update.</param>
        /// <returns>An instance of <see cref="StageEventArgs"/> containing the updated queue status and stage status after the update.</returns>
        public static StageEventArgs UpdateStatus(this AutomationQueueModel queueModel, StageEventArgs args)
        {
            // Extract the identifier for the stage from the event arguments
            var stageId = args.Stage.Reference.Id;

            // Access the overall queue status from the automation queue model
            var queueStatus = queueModel.Status.ProgressStatus;

            // Calculate the total number of jobs in the stage
            var jobs = args.Stage.Jobs.Count();

            // Calculate the total number of rules in the stage
            var rules = args.Stage.GetRulesCount();

            // Initialize a new dictionary to hold the status of each job within the stage
            var jobsStatus = new ConcurrentDictionary<string, G4AutomationStatusModel.JobStatusModel>();

            // Decrement the count of pending stages in the queue status, ensuring it does not go below zero
            queueStatus.PendingStages = queueStatus.PendingStages <= 0
                ? 0
                : queueStatus.PendingStages - 1;

            // Create and initialize a new stage status model with updated details
            queueStatus.StagesStatus[stageId] = new G4AutomationStatusModel.StageStatusModel
            {
                CompletedJobs = 0,
                CompletedRules = 0,
                Description = args.Stage.Description,
                JobsStatus = jobsStatus,
                Name = args.Stage.Name,
                PendingJobs = jobs,
                PendingRules = rules,
                Progress = 0,
                Id = stageId,
                Status = G4QueueModel.QueueStatusCodes.Processing,
                TotalJobs = jobs,
                TotalRules = rules
            };

            // Create and return a new StageStatusEventArgs object with the updated statuses
            return new()
            {
                AutomationStatus = queueStatus,
                StageStatus = queueStatus.StagesStatus[stageId]
            };
        }

        /// <summary>
        /// Updates the status of a specific automation within the queue based on the provided automation invoking event arguments.
        /// </summary>
        /// <param name="queueModel">The automation queue model containing the current status of all automations.</param>
        /// <param name="args">The event arguments containing information about the automation invocation that triggered the status update.</param>
        /// <returns>An instance of <see cref="AutomationEventArgs"/> containing the updated queue status and automation reference.</returns>
        public static AutomationEventArgs UpdateStatus(this AutomationQueueModel queueModel, AutomationEventArgs args)
        {
            // Creates a new queue status model for the specified automation.
            static G4AutomationStatusModel NewQueueStatusModel(G4AutomationModel automation)
            {
                // Get the reference to the automation
                var automationReference = automation.Reference;

                // Calculate the total number of stages in the automation
                var stages = automation.Stages.Count();

                // Calculate the total number of jobs in the automation
                var jobs = automation.Stages.SelectMany(i => i.Jobs).Count();

                // Calculate the total number of rules in the automation
                var rules = automation.Stages.Sum(i => i.GetRulesCount());

                // Initialize stages status dictionary
                var stagesStatus = new ConcurrentDictionary<string, G4AutomationStatusModel.StageStatusModel>();

                // Create and initialize a new queue status model
                return new G4AutomationStatusModel()
                {
                    CompletedJobs = 0,
                    CompletedRules = 0,
                    CompletedStages = 0,
                    Description = automationReference.Description,
                    Name = automationReference.Name,
                    PendingJobs = jobs,
                    PendingRules = rules,
                    PendingStages = stages,
                    Progress = 0,
                    Id = automationReference.Id,
                    StagesStatus = stagesStatus,
                    TotalJobs = jobs,
                    TotalRules = rules,
                    TotalStages = stages
                };
            }

            // Determine the group ID based on whether it is provided or default to automation reference ID
            var groupId = string.IsNullOrEmpty(args.Automation.GroupId)
                ? args.Automation.Reference.Id
                : args.Automation.GroupId;

            // Get the automation reference ID
            var reference = args.Automation.Reference.Id;

            // Initialize the queue status for the automation by creating a new queue status model
            queueModel.Status.ProgressStatus = NewQueueStatusModel(queueModel.Status.Automation);

            // Create and return the AutomationStatusEventArgs with updated statuses
            return new()
            {
                AutomationGroup = groupId,
                AutomationReference = reference,
                AutomationStatus = queueModel.Status.ProgressStatus
            };
        }
    }
}
