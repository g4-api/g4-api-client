using G4.IntegrationTests.Framework;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

namespace G4.IntegrationTests.Engine
{
    internal class C0002(TestContext context) : TestCaseBase(context)
    {
        protected override IEnumerable<ActionRuleModel> OnActions(AutomationEnvironment environment)
        {
            // No action rules are defined for this test case
            return [];
        }

        protected override void OnAutomation(G4AutomationModel automation, AutomationEnvironment environment)
        {
            // Define the first stage with its own driver parameters and a job that registers a session parameter
            var stageOne = new G4StageModel
            {
                Jobs =
                [
                    new G4JobModel
                    {
                        Rules =
                        [
                            new ActionRuleModel
                            {
                                PluginName = "RegisterParameter",
                                Argument = "{{$ --Name:" + Guid.NewGuid() + " --Value:Foo Bar --Scope:Session}}"
                            }
                        ]
                    }
                ],
                DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = "SimulatorDriver",
                    ["driverBinaries"] = "."
                }
            };

            // Define the second stage with its own driver parameters and a job that registers a session parameter
            var stageTwo = new G4StageModel
            {
                Jobs =
                [
                    new G4JobModel
                    {
                        Rules =
                        [
                            new ActionRuleModel
                            {
                                PluginName = "RegisterParameter",
                                Argument = "{{$ --Name:" + Guid.NewGuid() + " --Value:Foo Bar --Scope:Session}}"
                            }
                        ]
                    }
                ],
                DriverParameters = new Dictionary<string, object>
                {
                    // Specifies that the second stage should use the driver provided by the test parameter "stageDriver"
                    ["driver"] = $"{environment.TestParameters["stageDriver"]}"
                }
            };

            // Assign both stages to the automation model
            automation.Stages = [stageOne, stageTwo];
        }
    }
}
