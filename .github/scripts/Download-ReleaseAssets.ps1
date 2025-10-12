
param(
	[Parameter(Mandatory=$true)]
	[string]$GitHubToken,

	[Parameter(Mandatory=$true)]
	[string]$BinDirectory
)

# Set up the API call
$headers = @{
	"Authorization"	= "Bearer $GitHubToken"
	"Accept"		= "application/vnd.github.v3+json"
}
$apiUrl = "https://api.github.com/repos/DKorablin/Flatbed.Dialog.Lite/releases/latest"

try {
	Write-Host "Fetching latest release information..."
	$response = Invoke-RestMethod -Uri $apiUrl -Headers $headers
	$latestVersion = $response.tag_name

	if ([string]::IsNullOrEmpty($latestVersion)) {
		Write-Error "The latest release does not have a valid tag name."
		exit 1
	}

	# This is the standard way to send output back to the GitHub Actions step
	echo "latest_version=$latestVersion" >> $env:GITHUB_OUTPUT
	Write-Host "Successfully found latest version: $latestVersion"

	# Find the framework directories that were built
	$frameworkDirs = Get-ChildItem -Path $BinDirectory -Directory
	foreach ($dir in $frameworkDirs)
	{
		$frameworkName = $dir.Name
		Write-Host "----- Processing assets for framework: $frameworkName -----"

		$requiredAssetFilename = "Flatbed.Dialog.Lite_${latestVersion}_${frameworkName}.zip"
		Write-Host "Searching for required asset: $requiredAssetFilename"

		# Find the specific asset needed for this framework
		$assetToDownload = $response.assets | Where-Object { $_.name -eq $requiredAssetFilename }

		if ($null -eq $assetToDownload) {
			Write-Error "The required asset '$requiredAssetFilename' was not found in release '$latestVersion'."
			exit 1
		}

		# Prepare for download
		$destinationPath = Join-Path -Path $BinDirectory -ChildPath $assetToDownload.name
		$downloadHeaders = @{
			"Authorization"	= "Bearer $GitHubToken"
			"Accept"		= "application/octet-stream"
		}

		Write-Host "Downloading $($assetToDownload.name)..."
		Invoke-WebRequest -Uri $assetToDownload.url -OutFile $destinationPath -Headers $downloadHeaders
		Write-Host "Successfully downloaded to $destinationPath"
	}
} catch {
	Write-Error "An error occurred while fetching the release information: $_"
	exit 1
}