#!/usr/bin/env bash
set -euo pipefail

# Configure a local git merge driver named 'unityyamlmerge' to point at
# Unity's UnityYAMLMerge tool. This script tries to locate the tool and
# configures the local repository git config. Run from the repo root.

# Usage:
#  ./scripts/setup-unity-smart-merge.sh
# Or set UNITY_EDITOR_PATH to the Unity Editor folder that contains the Tools dir
# e.g. /Applications/Unity/Hub/Editor/6000.2.5f1/Unity.app

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ -n "${UNITY_EDITOR_PATH-}" ]; then
  CANDIDATE="$UNITY_EDITOR_PATH/Contents/Tools/UnityYAMLMerge"
else
  # common UnityHub editor path (project's csproj referenced this version)
  CANDIDATE="/Applications/Unity/Hub/Editor/6000.2.5f1/Unity.app/Contents/Tools/UnityYAMLMerge"
fi

if command -v UnityYAMLMerge >/dev/null 2>&1; then
  DRIVER_CMD='UnityYAMLMerge merge -p %O %A %B %L %P'
elif command -v unityyamlmerge >/dev/null 2>&1; then
  DRIVER_CMD='unityyamlmerge merge -p %O %A %B %L %P'
elif [ -x "$CANDIDATE" ]; then
  DRIVER_CMD="\"$CANDIDATE\" merge -p %O %A %B %L %P"
else
  echo "Could not find UnityYAMLMerge on PATH or at default path: $CANDIDATE"
  echo "Please install a Unity Editor or set UNITY_EDITOR_PATH to your Unity.app path,"
  echo "then re-run this script, or configure git manually (see docs/UNITY_SMART_MERGE.md)."
  exit 1
fi

git -C "$REPO_ROOT" config --local merge.unityyamlmerge.name "Unity Smart Merge"
git -C "$REPO_ROOT" config --local merge.unityyamlmerge.driver "$DRIVER_CMD"

echo "Configured local git merge driver 'unityyamlmerge'."
echo "You can verify with: git -C $REPO_ROOT config --local --get merge.unityyamlmerge.driver"
