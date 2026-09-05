param (
    [switch]$Uninstall
)

$pluginName = "MsfsPhysicsCamera"

# Detect executable location:
# 1. Packaged release: MsfsPhysicsCamera.exe (same folder)
# 2. Debug build: bin\Debug\net10.0-windows\MsfsPhysicsCamera.exe
$releasePath = Join-Path $PSScriptRoot "MsfsPhysicsCamera.exe"
$debugPath = Join-Path $PSScriptRoot "bin\Debug\net10.0-windows\MsfsPhysicsCamera.exe"

$exePath = $null
if (Test-Path $releasePath) {
    $exePath = $releasePath
} elseif (Test-Path $debugPath) {
    $exePath = $debugPath
}

if (-not $Uninstall -and -not $exePath) {
    Write-Host "Could not find MsfsPhysicsCamera.exe." -ForegroundColor Red
    Write-Host "Looked in:"
    Write-Host "  $releasePath"
    Write-Host "  $debugPath"
    Write-Host "Build the project first, or ensure the script is in the same folder as the executable."
    Pause
    exit
}

if ($exePath) {
    Write-Host "Using executable: $exePath"
}

# Detect MSFS 2024 exe.xml location
$msStorePath = "$env:LOCALAPPDATA\Packages\Microsoft.Limitless_8wekyb3d8bbwe\LocalCache\exe.xml"
$steamPath = "$env:APPDATA\Microsoft Flight Simulator 2024\exe.xml"

$exeXmlPath = $null

if (Test-Path $msStorePath) {
    $exeXmlPath = $msStorePath
} elseif (Test-Path $steamPath) {
    $exeXmlPath = $steamPath
} else {
    Write-Host "Could not find MSFS 2024 exe.xml (checked MS Store and Steam paths)." -ForegroundColor Red
    Write-Host "You may need to run MSFS 2024 at least once, or create it manually."
    Pause
    exit
}

Write-Host "Found exe.xml at: $exeXmlPath"

# Load XML
[xml]$xml = Get-Content $exeXmlPath

if ($Uninstall) {
    # Remove existing node if uninstalling
    $nodes = $xml.SelectNodes("//Launch.Addon[Name='$pluginName']")
    if ($nodes.Count -gt 0) {
        foreach ($node in $nodes) {
            $xml.DocumentElement.RemoveChild($node) | Out-Null
        }
        $xml.Save($exeXmlPath)
        Write-Host "Successfully removed $pluginName from auto-start." -ForegroundColor Green
    } else {
        Write-Host "$pluginName was not found in auto-start."
    }
} else {
    # Install / Update
    $nodes = $xml.SelectNodes("//Launch.Addon[Name='$pluginName']")
    if ($nodes.Count -gt 0) {
        Write-Host "Updating existing entry for $pluginName..."
        foreach ($node in $nodes) {
            $xml.DocumentElement.RemoveChild($node) | Out-Null
        }
    } else {
        Write-Host "Adding new entry for $pluginName..."
    }

    $addonNode = $xml.CreateElement("Launch.Addon")

    $nameNode = $xml.CreateElement("Name")
    $nameNode.InnerText = $pluginName
    $addonNode.AppendChild($nameNode) | Out-Null

    $disabledNode = $xml.CreateElement("Disabled")
    $disabledNode.InnerText = "False"
    $addonNode.AppendChild($disabledNode) | Out-Null

    $pathNode = $xml.CreateElement("Path")
    $pathNode.InnerText = $exePath
    $addonNode.AppendChild($pathNode) | Out-Null

    $xml.DocumentElement.AppendChild($addonNode) | Out-Null

    $xml.Save($exeXmlPath)
    Write-Host "Successfully added $pluginName to auto-start!" -ForegroundColor Green
}

Write-Host "Press any key to exit..."
$Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
