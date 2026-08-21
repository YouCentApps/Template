<#
.SYNOPSIS
    Renames the Template project to a new app name.
.DESCRIPTION
    Replaces all occurrences of "Template" with the specified new name
    in all text files, project files, folder names, and solution files.
    Preserves original casing patterns (Template -> NewName, template -> newname, TEMPLATE -> NEWNAME).
.PARAMETER NewName
    The new application name in PascalCase (e.g., "MyApp", "Podium", "Promise").
.EXAMPLE
    .\Rename-Template.ps1 -NewName "MyApp"
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[A-Z][a-zA-Z0-9]+$')]
    [string]$NewName
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$TemplateRoot = Split-Path -Parent $ScriptDir

Write-Host "=== Template Rename Tool ===" -ForegroundColor Cyan
Write-Host "Renaming 'Template' -> '$NewName'" -ForegroundColor Cyan
Write-Host "Root: $TemplateRoot" -ForegroundColor Gray
Write-Host ""

# Build case variants
$oldPascal = "Template"                     # Template
$newPascal = $NewName                       # MyApp
$oldLower  = "template"                     # template
$newLower  = $NewName.ToLower()             # myapp
$oldUpper  = "TEMPLATE"                     # TEMPLATE
$newUpper  = $NewName.ToUpper()             # MYAPP

# File extensions to process for text replacement
$textExtensions = @(
    "*.cs", "*.csproj", "*.slnx", "*.sln", "*.razor", "*.css", "*.html",
    "*.json", "*.js", "*.xml", "*.xaml", "*.manifest", "*.md", "*.txt",
    "*.ps1", "*.yml", "*.yaml", "*.props", "*.targets", "*.config", "*.http"
)

# Step 1: Replace text content in files
Write-Host "Step 1: Replacing text in files..." -ForegroundColor Yellow
$filesProcessed = 0
$filesChanged = 0

foreach ($ext in $textExtensions) {
    $files = Get-ChildItem -Path $TemplateRoot -Filter $ext -Recurse -File -ErrorAction SilentlyContinue |
             Where-Object { $_.FullName -notmatch '\\(bin|obj|\.vs|node_modules)\\' -and $_.FullName -notmatch '\\wwwroot\\lib\\' -and $_.Name -ne 'libman.json' }

    foreach ($file in $files) {
        $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
        if ($null -eq $content) { continue }
        $filesProcessed++

        $original = $content

        # Replace in order: UPPER, Pascal, lower (most specific first for mixed-case scenarios)
        $content = $content -creplace [regex]::Escape($oldUpper), $newUpper
        $content = $content -creplace [regex]::Escape($oldPascal), $newPascal
        $content = $content -creplace [regex]::Escape($oldLower), $newLower

        if ($content -ne $original) {
            Set-Content -Path $file.FullName -Value $content -NoNewline
            $filesChanged++
            Write-Host "  Updated: $($file.FullName.Replace($TemplateRoot, '.'))" -ForegroundColor Green
        }
    }
}

Write-Host "  Processed $filesProcessed files, changed $filesChanged files." -ForegroundColor Gray
Write-Host ""

# Step 2: Rename files containing "Template" in their name
Write-Host "Step 2: Renaming files..." -ForegroundColor Yellow
$renamedFiles = 0

# Get all files with "Template" in name (deepest first to avoid path conflicts)
$filesToRename = Get-ChildItem -Path $TemplateRoot -Recurse -File -ErrorAction SilentlyContinue |
                 Where-Object { $_.Name -match 'Template' -and $_.FullName -notmatch '\\(bin|obj|\.vs)\\' -and $_.FullName -notmatch '\\wwwroot\\lib\\' } |
                 Sort-Object { $_.FullName.Length } -Descending

foreach ($file in $filesToRename) {
    $newFileName = $file.Name -creplace $oldPascal, $newPascal
    if ($newFileName -ne $file.Name) {
        $newPath = Join-Path $file.DirectoryName $newFileName
        Rename-Item -Path $file.FullName -NewName $newFileName
        $renamedFiles++
        Write-Host "  Renamed: $($file.Name) -> $newFileName" -ForegroundColor Green
    }
}

Write-Host "  Renamed $renamedFiles files." -ForegroundColor Gray
Write-Host ""

# Step 3: Rename directories containing "Template" in their name
Write-Host "Step 3: Renaming directories..." -ForegroundColor Yellow
$renamedDirs = 0

# Process deepest directories first to avoid path invalidation
$dirsToRename = Get-ChildItem -Path $TemplateRoot -Recurse -Directory -ErrorAction SilentlyContinue |
                Where-Object { $_.Name -match 'Template' -and $_.FullName -notmatch '\\(bin|obj|\.vs)\\' -and $_.FullName -notmatch '\\wwwroot\\lib\\' } |
                Sort-Object { $_.FullName.Length } -Descending

foreach ($dir in $dirsToRename) {
    $newDirName = $dir.Name -creplace $oldPascal, $newPascal
    if ($newDirName -ne $dir.Name) {
        $newPath = Join-Path $dir.Parent.FullName $newDirName
        Rename-Item -Path $dir.FullName -NewName $newDirName
        $renamedDirs++
        Write-Host "  Renamed: $($dir.Name) -> $newDirName" -ForegroundColor Green
    }
}

Write-Host "  Renamed $renamedDirs directories." -ForegroundColor Gray
Write-Host ""

# Step 4: Rename the root Template folder itself
$rootTemplateDir = $TemplateRoot
$parentDir = Split-Path -Parent $rootTemplateDir
$rootDirName = Split-Path -Leaf $rootTemplateDir
if ($rootDirName -eq "Template") {
    $newRootName = $newPascal
    $newRootPath = Join-Path $parentDir $newRootName
    Write-Host "Step 4: Renaming root folder..." -ForegroundColor Yellow
    Rename-Item -Path $rootTemplateDir -NewName $newRootName
    Write-Host "  Renamed: $rootDirName -> $newRootName" -ForegroundColor Green
} else {
    Write-Host "Step 4: Root folder is not 'Template', skipping..." -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== Done! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Update Azure Storage connection string in user secrets or appsettings" -ForegroundColor White
Write-Host "  2. Run the DbInit tool to create tables: dotnet run --project Tools/$newPascal.DbInit" -ForegroundColor White
Write-Host "  3. Build and run: dotnet build && dotnet run --project $newPascal.Api" -ForegroundColor White
Write-Host ""
