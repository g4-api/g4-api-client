<#
.SYNOPSIS
    Generates a Markdown report from a Cobertura XML coverage file.

.DESCRIPTION
    This script reads a Cobertura XML coverage report from a specified file path or URL and generates a comprehensive Markdown report. The report includes overall coverage statistics and, optionally, detailed information about packages, classes, and individual lines of code. Additionally, badges representing line and branch coverage can be included.

.PARAMETER Title
    The title to be used as the Markdown report's main heading.

.PARAMETER CoberturaXmlPath
    Path or URL to the input Cobertura XML file.

.PARAMETER MarkdownPath
    Path to the output Markdown (.md) file.

.PARAMETER AddBadges
    Include badges for line and branch coverage in the report.

.PARAMETER AddPackages
    Include detailed package information in the report.

.PARAMETER AddClassSummary
    Include a summary table of classes within each package.

.PARAMETER AddClassDetails
    Include detailed information for each class, including line-by-line coverage.

.EXAMPLE
    .\GenerateCoverageReport.ps1 -Title "My Coverage Report" -CoberturaXmlPath "coverage.xml" -MarkdownPath "Report.md" -AddBadges -AddPackages -AddClassSummary -AddClassDetails

.NOTES
    Ensure that the Cobertura XML file is accessible and properly formatted.
#>
param(
    [Parameter(Mandatory = $false, HelpMessage = "The title to be used as the Markdown report's main heading.")]
    [string]$Title = "Tests Coverage Summary",

    [Parameter(Mandatory = $true, HelpMessage = "Path or URL to the input Cobertura XML file.")]
    [string]$CoberturaXmlPath,

    [Parameter(Mandatory = $true, HelpMessage = "Path to the output Markdown (.md) file.")]
    [string]$MarkdownPath,

    [Parameter(Mandatory = $false, HelpMessage = "Include badges for line and branch coverage.")]
    [switch]$AddBadges,

    [Parameter(Mandatory = $false, HelpMessage = "Include detailed package information.")]
    [switch]$AddPackages,

    [Parameter(Mandatory = $false, HelpMessage = "Include a summary table of classes within each package.")]
    [switch]$AddClassSummary,

    [Parameter(Mandatory = $false, HelpMessage = "Include detailed information for each class.")]
    [switch]$AddClassDetails
)

# Determine if the Cobertura XML source is a URL or a file path
if ($CoberturaXmlPath -match "^http(s)?://") {
    # Load XML content from URL
    $xml = [xml](Invoke-WebRequest -Uri $CoberturaXmlPath).Content
} else {
    # Load XML content from file
    [xml]$xml = Get-Content -Path $CoberturaXmlPath
}

# Extract coverage rates and convert them to percentages
$lineCoverage   = [math]::Round($xml.coverage.'line-rate', 2) * 100
$branchCoverage = [math]::Round($xml.coverage.'branch-rate', 2) * 100

# Initialize the Markdown content with the custom report title
$markdown = @"
# $Title
"@

# Optionally add coverage badges to the report
if ($AddBadges) {
    $markdown += @"
    
![Line Coverage](https://img.shields.io/badge/Line%20Coverage-$lineCoverage%25-success?style=flat) ![Branch Coverage](https://img.shields.io/badge/Branch%20Coverage-$branchCoverage%25-success?style=flat)
    
"@
}

# Add the overview section with basic coverage statistics
$markdown += @"
    
## Overview

- **Lines Covered**: $($xml.coverage.'lines-covered')
- **Valid Lines**: $($xml.coverage.'lines-valid')
- **Branches Covered**: $($xml.coverage.'branches-covered')
- **Valid Branches**: $($xml.coverage.'branches-valid')

"@

# Optionally include detailed package information
if ($AddPackages) {
    $markdown += @"
    
## Packages

"@

    foreach ($package in $xml.coverage.packages.package) {
        $packageName = $package.name
        $packageLineRate = [math]::Round($package.'line-rate', 2) * 100
        $packageBranchRate = [math]::Round($package.'branch-rate', 2) * 100
        $packageComplexity = $package.complexity

        $markdown += @"
    
### Package: $packageName

- **Line Coverage Rate**: $packageLineRate%
- **Branch Coverage Rate**: $packageBranchRate%
- **Complexity**: $packageComplexity

"@

        # Optionally include a summary of classes within the package
        if ($AddClassSummary) {
            $markdown += @"
    
#### Classes Summary

| Class Name                            | Filename            | Line Coverage Rate | Branch Coverage Rate | Complexity |
|---------------------------------------|---------------------|--------------------|----------------------|------------|

"@

            foreach ($class in $package.classes.class) {
                $className = $class.name
                $classFilename = $class.filename
                $classLineRate = [math]::Round($class.'line-rate', 2) * 100
                $classBranchRate = [math]::Round($class.'branch-rate', 2) * 100
                $classComplexity = $class.complexity

                $markdown += @"
| $className | $classFilename | $classLineRate% | $classBranchRate% | $classComplexity |

"@
            }
        }

        # Optionally include detailed information for each class
        if ($AddClassDetails) {
            $markdown += "`n#### Class Details`n"

            foreach ($class in $package.classes.class) {
                $className = $class.name
                $classFilename = $class.filename
                $classLineRate = [math]::Round($class.'line-rate', 2) * 100
                $classBranchRate = [math]::Round($class.'branch-rate', 2) * 100
                $classComplexity = $class.complexity

                $markdown += @"
    
##### $className

- **Filename**: `$classFilename`
- **Line Coverage Rate**: $classLineRate%
- **Branch Coverage Rate**: $classBranchRate%
- **Complexity**: $classComplexity

###### Lines

| Line Number | Hits | Branch |
|-------------|------|--------|

"@

                foreach ($line in $class.lines.line) {
                    $lineNumber = $line.number
                    $hits = $line.hits
                    $branch = $line.branch

                    $markdown += @"
| $lineNumber | $hits | $branch |

"@
                }
            }
        }
    }
}

# Write the generated Markdown content to the specified output file
$markdown | Out-File -FilePath $MarkdownPath -Encoding utf8
Write-Host "Coverage Summary: $MarkdownPath"
