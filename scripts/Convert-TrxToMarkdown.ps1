<#
.SYNOPSIS
    Converts a .trx test results file to a Markdown (.md) file with summary and optional detailed test results, including error messages and stack traces.

.DESCRIPTION
    This script parses a .trx file, extracts summary information, and optionally organizes test results by test class.
    It then generates a Markdown file containing a summary table and, if specified, detailed tables for each test class with additional error information.

.PARAMETER TrxPath
    The file path to the input .trx file.

.PARAMETER MarkdownPath
    The file path to the output .md file.

.PARAMETER AddDetails
    A switch to include detailed test class results in the Markdown output. If not specified, only the summary table is generated.

.PARAMETER Level
    Specifies the level of detail to include in the report. Acceptable values are:
    - Pass (Default): Show all test details.
    - Skip: Show only Failed and Skipped test details.
    - Fail: Show only Failed test details.

.PARAMETER Title
    The title of the Markdown report. Defaults to "Test Results Summary" if not specified.

.EXAMPLE
    .\Convert-TrxToMd.ps1 -TrxPath "C:\Tests\Results.trx" -MarkdownPath "C:\Tests\Results.md" -AddDetails -Level Fail -Title "My Custom Test Report"
#>
param(
    [Parameter(Mandatory = $true, HelpMessage = "Path to the input .trx file.")]
    [string]$TrxPath,

    [Parameter(Mandatory = $true, HelpMessage = "Path to the output .md file.")]
    [string]$MarkdownPath,

    [Parameter(Mandatory = $false, HelpMessage = "Include detailed test class results.")]
    [switch]$AddDetails,

    [Parameter(Mandatory = $false, HelpMessage = "Level of detail to include: Pass, Skip, Fail.")]
    [ValidateSet("Pass", "Skip", "Fail", IgnoreCase = $true)]
    [string]$Level = "Fail",

    [Parameter(Mandatory = $false, HelpMessage = "Title of the Markdown report.")]
    [string]$Title = "Test Results Summary"
)

# Function to escape pipe characters in Markdown table cells
function Format-MarkdownPipes {
    param (
        [string]$Text
    )
    return $Text -replace '\|', '\|'
}

# Function to retrieve ErrorInfo elements (Message and StackTrace) if present
function Get-ErrorInfo {
    param (
        [System.Xml.XmlElement]$TestResult
    )

    if ($TestResult.Output.ErrorInfo) {
        $message = Format-MarkdownPipes $TestResult.Output.ErrorInfo.Message
        $stackTrace = Format-MarkdownPipes $TestResult.Output.ErrorInfo.StackTrace
    }
    else {
        $message = "N/A"
        $stackTrace = "N/A"
    }

    return @{
        "ErrorMessage" = $message
        "StackTrace"   = $stackTrace
    }
}

# Check if the input .trx file exists
if (!(Test-Path -Path $TrxPath)) {
    Write-Error "The specified .trx file does not exist: $TrxPath"
    exit 1
}

# Load the .trx XML file
try {
    [xml]$trx = Get-Content -Path $TrxPath
}
catch {
    Write-Error "Failed to load the .trx file. Ensure it is a valid XML file."
    exit 1
}

# Define the XML namespace
$namespace = @{ "ns" = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010" }

# Extract Summary Information
$summary = $trx.TestRun.ResultSummary.Counters

$summaryData = @{
    "Total Tests"   = $summary.total
    "Executed"      = $summary.executed
    "Passed"        = $summary.passed
    "Failed"        = $summary.failed
    "Skipped"       = $summary.notExecuted
}

# Initialize Markdown Content with the specified Title
$mdContent = "# $Title`n`n"

# Create Summary Table
$mdContent += "| Metric        | Count |`n"
$mdContent += "|---------------|-------|`n"

foreach ($key in $summaryData.Keys) {
    $mdContent += "| $key | $($summaryData[$key]) |`n"
}

$mdContent += "`n`n"

# Only proceed to add detailed test class results if AddDetails switch is set
if ($AddDetails) {

    # Extract Test Definitions to map testId to className
    $testDefinitions = @{}

    foreach ($testDef in $trx.TestRun.TestDefinitions.UnitTest) {
        $testId = $testDef.id
        $className = $testDef.TestMethod.className
        $testDefinitions[$testId] = $className
    }

    # Extract each test result and organize by class
    $testsByClass = @{}

    foreach ($testResult in $trx.TestRun.Results.UnitTestResult) {
        $testId = $testResult.testId
        $testName = $testResult.testName
        $outcome = $testResult.outcome
        $duration = $testResult.duration

        # Get the class name from TestDefinitions
        if ($testDefinitions.ContainsKey($testId)) {
            $className = $testDefinitions[$testId]
        }
        else {
            $className = "UnknownClass"
        }

        # Initialize the class group if it doesn't exist
        if (-not $testsByClass.ContainsKey($className)) {
            $testsByClass[$className] = @()
        }

        # Retrieve ErrorInfo if available
        $errorInfo = Get-ErrorInfo -TestResult $testResult

        # Add the test details to the class group
        $testsByClass[$className] += @{
            "TestName"     = $testName
            "Result"       = $outcome
            "Runtime"      = $duration
            "ErrorMessage" = $errorInfo.ErrorMessage
            "StackTrace"   = $errorInfo.StackTrace
        }
    }

    # Define which outcomes to include based on the Level parameter
    switch ($Level.ToLower()) {
        "pass" {
            # Include all test outcomes
            $outcomeFilter = @("Passed", "Failed", "Skipped")
        }
        "skip" {
            # Include only Failed and Skipped
            $outcomeFilter = @("Failed", "Skipped")
        }
        "fail" {
            # Include only Failed
            $outcomeFilter = @("Failed")
        }
        default {
            # Default to include all
            $outcomeFilter = @("Passed", "Failed", "Skipped")
        }
    }

    # Generate Markdown Tables for Each Test Class
    foreach ($class in $testsByClass.Keys) {
        # Filter tests based on the outcomeFilter
        $filteredTests = $testsByClass[$class] | Where-Object { $outcomeFilter -contains $_.Result }

        # Skip generating table if no tests match the filter
        if ($filteredTests.Count -eq 0) {
            continue
        }

        $mdContent += "`n## Test Class: $class`n`n"

        $mdContent += "| Test Scenario | Result | Runtime | Error Message | Stack Trace |`n"
        $mdContent += "|---------------|--------|---------|---------------|-------------|`n"

        foreach ($test in $filteredTests) {
            $testName     = Format-MarkdownPipes $test.TestName
            $result       = $test.Result
            $runtime      = $test.Runtime
            $errorMessage = if ($result -eq "Failed") { $test.ErrorMessage } else { "N/A" }
            $stackTrace   = if ($result -eq "Failed") { $test.StackTrace }  else { "N/A" }

            # Format the result with emojis for better readability
            switch ($result) {
                "Passed"   { $resultDisplay = "✅" }
                "Failed"   { $resultDisplay = "❌" }
                "Skipped"  { $resultDisplay = "⏭️" }
                default    { $resultDisplay = $result }
            }

            # Replace newlines in errorMessage and stackTrace with <br> for Markdown
            $errorMessage = $errorMessage -replace "`r`n|`n|`r", "<br>"
            $stackTrace   = $stackTrace   -replace "`r`n|`n|`r", "<br>"

            $mdContent += "| $testName | $resultDisplay | $runtime | $errorMessage | $stackTrace |`n"
        }

        # Append the separator with only one empty line
        $mdContent += "`n`n"
    }

    # After the loop, remove any trailing newlines and ensure only one empty line
    $mdContent = $mdContent.TrimEnd("`n") + "`n"
}

# Write the Markdown content to the specified file
try {
    $mdContent | Out-File -FilePath $MarkdownPath -Encoding UTF8
    Write-Output "Markdown report generated successfully at: $MarkdownPath"
    if ($AddDetails) {
        Write-Output "Detailed test class results are included with Level set to '$Level'."
    }
    else {
        Write-Output "Detailed test class results are omitted."
    }
}
catch {
    Write-Error "Failed to write to the Markdown file. Ensure you have write permissions."
    exit 1
}
