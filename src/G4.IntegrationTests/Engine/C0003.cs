using G4.IntegrationTests.Framework;
using G4.Models;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using System;
using System.Collections.Generic;

namespace G4.IntegrationTests.Engine
{
    internal class C0003(TestContext context) : TestCaseBase(context)
    {
        protected override IEnumerable<ActionRuleModel> OnActions(AutomationEnvironment environment)
        {
            // No action rules are defined for this test case
            return [];
        }

        protected override void OnAutomation(G4AutomationModel automation, AutomationEnvironment environment)
        {
            var stage = new G4StageModel
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
                ]
            };

            automation.Stages = [stage];
        }

        public class C0003A(TestContext context) : TestCaseBase(context)
        {
            protected override IEnumerable<ActionRuleModel> OnActions(AutomationEnvironment environment)
            {
                // No action rules are defined for this test case
                return [];
            }

            protected override void OnAutomation(G4AutomationModel automation, AutomationEnvironment environment)
            {
                var stage = new G4StageModel
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
                    ]
                };
                automation.DriverParameters = new Dictionary<string, object>
                {
                    ["driver"] = $"{environment.TestParameters["automationDriver"]}"
                };
                automation.Stages = [stage];
            }
        }
    }
}
