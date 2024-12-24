namespace G4.UnitTests.Framework
{
    public static class ActionRuleStubs
    {
        public const string OnElement = "$[OnElement]";
        public const string OnArgument = "$[OnArgument]";
        public const string OnCondition = "$[OnCondition]";
        public const string OnOperator = "$[OnOperator]";
        public const string OnOperatorExpected = "$[OnOperatorExpected]";
        public const string OnAttribute = "$[OnAttribute]";
        public const string OnActual = "$[OnActual]";
        public const string OnPluginName = "$[OnPluginName]";
        public const string OnRegularExpression = "$[OnRegularExpression]";

        public const string RuleJsonArgumentRegularExpressionOperator =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonArgumentRegularExpression =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{" + OnArgument + "}\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"" +
            "}";

        public const string RuleJsonAttributeOperator =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonBoolean =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + "}}\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonExternal =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\":\"Assert\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"" +
            "}";

        public const string RuleJsonExternalCamel =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\":\"Assert\"," +
            "    \"onElement\":\"//positive\"," +
            "    \"argument\":\"{{$ --externalAssertion --match:" + OnActual + "}}\"" +
            "}";

        public const string RuleJsonExternalUntilCamel =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\":\"Assert\"," +
            "    \"onElement\":\"//positive\"," +
            "    \"argument\":\"{{$ --until:" + OnCondition + " --" + OnOperator + ":Foo Bar}}\"" +
            "}";

        public const string RuleJsonExternalUntilPascal =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\":\"Assert\"," +
            "    \"onElement\":\"//positive\"," +
            "    \"argument\":\"{{$ --Until:" + OnCondition + " --" + OnOperator + ":Foo Bar}}\"" +
            "}";

        public const string RuleJsonNoAttributeOperator =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonAttributeRegexOperator =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonNoAttributeRegexOperator =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"argument\":\"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"onElement\":\"" + OnElement + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"pluginName\": \"" + OnPluginName + "\"" +
            "}";

        public const string RuleJsonConditionDynamicFailOnException =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"SetCondition\"," +
            "    \"argument\": \"{{$ --FailOnException --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonConditionDynamic =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"SetCondition\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonConditionDynamicNegativeRules =
            "{" +
            "    \"$type\": \"Condition\"," +
            "    \"pluginName\": \"SetCondition\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"negativeRules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonConditionBoolean =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"SetCondition\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"onAttribute\":\"" + OnAttribute + "\"," +
            "    \"regularExpression\":\"" + OnRegularExpression + "\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonRepeatWithIterations =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeWhileLoop\"," +
            "    \"argument\": \"{{$ --Iterations:" + OnOperatorExpected + "}}\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeForLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeForLoop\"," +
            "    \"argument\": \"3\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeForLoopWithNestedLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeForLoop\"," +
            "    \"argument\": \"3\"," +
            "    \"rules\": [" +
            "       {" +
            "           \"$type\": \"Action\"," +
            "           \"pluginName\": \"InvokeForLoop\"," +
            "           \"argument\": \"3\"," +
            "           \"rules\": [" +
            "               {" +
            "                   \"$type\": \"Action\"," +
            "                   \"pluginName\": \"RegisterParameter\"," +
            "                   \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "               }" +
            "           ]" +
            "       }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeForTripleLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeForLoop\"," +
            "    \"argument\": \"3\"," +
            "    \"rules\": [" +
            "       {" +
            "           \"$type\": \"Action\"," +
            "           \"pluginName\": \"InvokeForLoop\"," +
            "           \"argument\": \"3\"," +
            "           \"rules\": [" +
            "               {" +
            "                  \"$type\": \"Action\"," +
            "                  \"pluginName\": \"InvokeForLoop\"," +
            "                  \"argument\": \"3\"," +
            "                  \"rules\": [" +
            "                      {" +
            "                          \"$type\": \"Action\"," +
            "                          \"pluginName\": \"RegisterParameter\"," +
            "                          \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "                      }" +
            "                  ]" +
            "               }" +
            "           ]" +
            "       }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeWhileLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeWhileLoop\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeWhileLoopWithTimeout =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeWhileLoop\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + " --Timeout:$[OnTimeoutExpected]}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"rules\": [" +
            "        {" +
            "            \"$type\": \"Action\"," +
            "            \"pluginName\": \"RegisterParameter\"," +
            "            \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "        }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeWhileLoopWithNestedLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeWhileLoop\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"rules\": [" +
            "       {" +
            "           \"$type\": \"Action\"," +
            "           \"pluginName\": \"InvokeWhileLoop\"," +
            "           \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "           \"locator\": \"Id\"," +
            "           \"onElement\": \"" + OnElement + "\"," +
            "           \"rules\": [" +
            "               {" +
            "                   \"$type\": \"Action\"," +
            "                   \"pluginName\": \"RegisterParameter\"," +
            "                   \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "               }" +
            "           ]" +
            "       }" +
            "    ]" +
            "}";

        public const string RuleJsonInvokeWhileTripleLoop =
            "{" +
            "    \"$type\": \"Action\"," +
            "    \"pluginName\": \"InvokeWhileLoop\"," +
            "    \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "    \"locator\": \"Id\"," +
            "    \"onElement\": \"" + OnElement + "\"," +
            "    \"rules\": [" +
            "       {" +
            "           \"$type\": \"Action\"," +
            "           \"pluginName\": \"InvokeWhileLoop\"," +
            "           \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "           \"locator\": \"Id\"," +
            "           \"onElement\": \"" + OnElement + "\"," +
            "           \"rules\": [" +
            "               {" +
            "                  \"$type\": \"Action\"," +
            "                  \"pluginName\": \"InvokeWhileLoop\"," +
            "                  \"argument\": \"{{$ --Condition:" + OnCondition + " --Operator:" + OnOperator + " --Expected:" + OnOperatorExpected + "}}\"," +
            "                  \"locator\": \"Id\"," +
            "                  \"onElement\": \"" + OnElement + "\"," +
            "                  \"rules\": [" +
            "                      {" +
            "                          \"$type\": \"Action\"," +
            "                          \"pluginName\": \"RegisterParameter\"," +
            "                          \"argument\": \"{{$ --Name:TestParameter --Value:Foo Bar}}\"" +
            "                      }" +
            "                  ]" +
            "               }" +
            "           ]" +
            "       }" +
            "    ]" +
            "}";
    }
}
