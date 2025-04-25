# Set error preference to stop on errors
$ErrorActionPreference = "Stop"

Write-Host "Getting latest tag..."
$latestTag = git describe --tags --abbrev=0 2>$null

if (-not $latestTag) {
    Write-Host "No existing tags found. Starting with 0.0.0"
    $latestTag = "0.0.0"
} else {
    Write-Host "Latest tag found: $latestTag"
}

# Split the tag into parts
$tagParts = $latestTag.Split('.')

# Check if the tag has at least three parts (major.minor.patch)
if ($tagParts.Length -lt 3) {
    Write-Error "Tag format '$latestTag' is not in the expected major.minor.patch format."
    exit 1
}

# Get the patch version, convert to integer, and increment
$patchVersion = [int]$tagParts[-1]
$newPatchVersion = $patchVersion + 1

# Construct the new tag
$newTag = "$($tagParts[0]).$($tagParts[1]).$newPatchVersion"

Write-Host "New tag will be: $newTag"

# Apply the new tag to the latest commit
Write-Host "Applying tag $newTag to the latest commit..."
git tag $newTag

# Push the new tag to the remote
Write-Host "Pushing tag $newTag to remote..."
git push origin $newTag

Write-Host "Tagging and pushing complete."