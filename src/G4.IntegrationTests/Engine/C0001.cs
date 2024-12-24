using G4.IntegrationTests.Framework;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System.Collections.Generic;

namespace G4.IntegrationTests.Engine
{
    internal class C0001(TestContext context) : TestCaseBase(context)
    {
        protected override IEnumerable<ActionRuleModel> OnActions(AutomationEnvironment environment)
        {
            // No action rules are defined for this test case
            return [];
        }

        protected override void OnAutomation(G4AutomationModel automation, AutomationEnvironment environment)
        {
            // Define a job with specific driver parameters and associated action rules
            var job = new G4JobModel
            {
                DriverParameters = new Dictionary<string, object>
                {
                    // Specifies that the job should use the 'stage()' driver function to inherit the stage's driver
                    ["driver"] = $"{environment.TestParameters["jobDriver"]}"
                },
                Rules = new List<ActionRuleModel>
                {
                    // Plugin responsible for registering a parameter within the session scope
                    new()
                    {
                        PluginName = "RegisterParameter",
                        Argument = "{{$ --Name:TestParameter --Value:Foo Bar --Scope:Session}}"
                    }
                }
            };

            // Define a stage with its own driver parameters and assign the previously defined job to it
            var stage = new G4StageModel
            {
                Jobs = [job],
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                }
            };

            // Assign the stage to the automation model
            automation.Stages = [stage];
        }
    }
}
