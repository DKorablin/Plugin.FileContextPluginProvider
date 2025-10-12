param(
    [Parameter(Mandatory=$true)]
    [string]$GitHubToken,

    [Parameter(Mandatory=$true)]
    [string]$BinDirectory,

    [Parameter(Mandatory=$true)]
    [string]$SolutionName,

    [Parameter(Mandatory=$true)]
    [string]$ReleaseVersion
)

# --- 1. Fetch Release Information (Done Once) ---
$headers = @{
    "Authorization" = "Bearer $GitHubToken"
    "Accept"        = "application/vnd.github.v3+json"
}
$apiUrl = "https://api.github.com/repos/DKorablin/Flatbed.Dialog.Lite/releases/latest"
$dependencyRelease = $null
try {
    Write-Host "Fetching latest dependency release information..."
    $dependencyRelease = Invoke-RestMethod -Uri $apiUrl -Headers $headers
    if ([string]::IsNullOrEmpty($dependencyRelease.tag_name)) {
        Write-Error "The latest dependency release does not have a valid tag name."
        exit 1
    }
    Write-Host "Successfully found dependency version: $($dependencyRelease.tag_name)"
} catch {
    Write-Error "An error occurred while fetching dependency release information: $_"
    exit 1
}

# --- 2. Process Each Framework Directory ---
$frameworkDirs = Get-ChildItem -Path $BinDirectory -Directory

foreach ($dir in $frameworkDirs) {
    $frameworkName = $dir.Name
    Write-Host "----------------------------------------------------"
    Write-Host "----- Processing framework: $frameworkName"
    Write-Host "----------------------------------------------------"

    # --- Step A: Organize original build artifacts into a 'Plugin' subfolder ---
    Write-Host "Organizing built artifacts into 'Plugin' subfolder..."
    $pluginFolderPath = Join-Path -Path $dir.FullName -ChildPath "Plugin"
    New-Item -ItemType Directory -Path $pluginFolderPath -Force | Out-Null
    
    Get-ChildItem -Path $dir.FullName -Force | ForEach-Object {
        if ($_.Name -ne "Plugin") {
            Move-Item -Path $_.FullName -Destination $pluginFolderPath
        }
    }

    # --- Step B: Download and Extract the Correct Dependency Asset ---
    $assetFrameworkToDownload = $frameworkName
    if ($frameworkName -eq 'netstandard2.0' -or $frameworkName -eq 'netstandard2.1') {
        $assetFrameworkToDownload = 'net8.0-windows'
        Write-Host "Redirecting asset search from '$frameworkName' to '$assetFrameworkToDownload'."
    }
    
    $requiredAssetFilename = "Flatbed.Dialog.Lite_$($dependencyRelease.tag_name)_${assetFrameworkToDownload}.zip"
    Write-Host "Searching for required asset: $requiredAssetFilename"

    $assetToDownload = $dependencyRelease.assets | Where-Object { $_.name -eq $requiredAssetFilename }
    if ($null -eq $assetToDownload) {
        Write-Error "The required asset '$requiredAssetFilename' was not found."
        exit 1
    }

    $downloadPath = Join-Path -Path $BinDirectory -ChildPath $assetToDownload.name
    $downloadHeaders = @{
        "Authorization" = "Bearer $GitHubToken"
        "Accept"        = "application/octet-stream"
    }

    Write-Host "Downloading $($assetToDownload.name)..."
    Invoke-WebRequest -Uri $assetToDownload.url -OutFile $downloadPath -Headers $downloadHeaders
    
    Write-Host "Extracting asset to '$($dir.FullName)'..."
    Expand-Archive -Path $downloadPath -DestinationPath $dir.FullName -Force

    Write-Host "Cleaning up downloaded dependency zip..."
    Remove-Item -Path $downloadPath

    # --- Step C: Create the Final Zipped Release Archive ---
    Write-Host "Creating final release archive for $frameworkName..."
    $zipFileName = "${SolutionName}_v${ReleaseVersion}_${frameworkName}.zip"
    $zipDestinationPath = Join-Path -Path $dir.Parent.FullName -ChildPath $zipFileName
    $zipSourceContentPath = Join-Path -Path $dir.FullName -ChildPath "*"

    Compress-Archive -Path $zipSourceContentPath -DestinationPath $zipDestinationPath -Force
    Write-Host "Successfully created release archive: $zipFileName"
}