param(
    [int]$Major = 1,
    [int]$Minor = 0
)

# Папка, где лежит скрипт (корень проекта)
$ProjectDir = $PSScriptRoot

Write-Host "=== UpdateVersion.ps1 ==="
Write-Host "ProjectDir (from PSScriptRoot): $ProjectDir"
Write-Host "Major: $Major"
Write-Host "Minor: $Minor"

# Текущая дата
$date = Get-Date
$year  = $date.ToString("yy")      # 26 для 2026
$month = $date.ToString("MM")      # 02
$day   = $date.ToString("dd")      # 25
$build    = $year
$revision = $month + $day           # "0225"

$version = "$Major.$Minor.$build.$revision"
Write-Host "Generated version: $version"

# Путь к файлу VersionInfo.cs
$versionFilePath = Join-Path -Path $ProjectDir -ChildPath "Properties\VersionInfo.cs"
Write-Host "Target file: $versionFilePath"

# Создаём папку Properties, если её нет
$propertiesDir = Join-Path -Path $ProjectDir -ChildPath "Properties"
if (-not (Test-Path -Path $propertiesDir)) {
    Write-Host "Creating Properties directory..."
    New-Item -ItemType Directory -Path $propertiesDir -Force | Out-Null
}

# Содержимое файла с атрибутами версии
$content = @"
using System.Reflection;

[assembly: AssemblyVersion("$version")]
[assembly: AssemblyFileVersion("$version")]
"@

# Запись файла (UTF-8 без BOM)
Set-Content -Path $versionFilePath -Value $content -Encoding UTF8
Write-Host "File written successfully."
Write-Host "=========================="