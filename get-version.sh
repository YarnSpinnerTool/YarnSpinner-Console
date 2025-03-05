#!/bin/bash

# Get info about the current commit
most_recent_tag=$(git describe --tags --match="v*" --abbrev=0)
commits_since_tag=$(git rev-list $most_recent_tag..HEAD | wc -l | awk '{$1=$1};1')
sha=$(git log -1 --format=%H)
short_sha=$(git log -1 --format=%h)
branch=$(git rev-parse --abbrev-ref HEAD)

# A regex for extracting data from a version number: major, minor, patch,
# [prerelease]
REGEX='v(\d+)\.(\d+)\.(\d+)(-.*)?'

# Extract the data from the version number
major=$(echo $most_recent_tag | perl -pe "s|$REGEX|\1|" )
minor=$(echo $most_recent_tag | perl -pe "s|$REGEX|\2|" )
patch=$(echo $most_recent_tag | perl -pe "s|$REGEX|\3|" )
prerelease=$(echo $most_recent_tag | perl -pe "s|$REGEX|\4|" )

# Create the version strings we'll write into the AssemblyInfo files
OutputAssemblyVersion="$major.$minor.$patch.$commits_since_tag"
OutputAssemblyInformationalVersion="$major.$minor.$patch$prerelease+$commits_since_tag.Branch.$branch.Sha.$sha"
OutputAssemblyFileVersion="$major.$minor.$patch.$commits_since_tag"

# Calculate the semver from the version (should be the same as the version, but
# just in case)
SemVer="$major.$minor.$patch$prerelease"

# If there are any commits since the current tag, add that note
if [ "$commits_since_tag" -gt 0 ]; then
    SemVer="$SemVer+$commits_since_tag"
fi

# Update the AssemblyInfo.cs files
for infoFile in $(find . -name "AssemblyInfo.cs"); do
    perl -pi -e "s/AssemblyVersion\(\".*\"\)/AssemblyVersion(\"$OutputAssemblyVersion\")/" $infoFile
    perl -pi -e "s/AssemblyInformationalVersion\(\".*\"\)/AssemblyInformationalVersion(\"$OutputAssemblyInformationalVersion\")/" $infoFile
    perl -pi -e "s/AssemblyFileVersion\(\".*\"\)/AssemblyFileVersion(\"$OutputAssemblyFileVersion\")/" $infoFile
done

# If we're running in GitHub Workflows, output our calculated SemVer
if [[ -n $GITHUB_OUTPUT ]]; then
    echo "SemVer=$SemVer" >> "$GITHUB_OUTPUT"
    echo "ShortSha=$short_sha" >> "$GITHUB_OUTPUT"
fi

# Log our SemVer
echo $SemVer
