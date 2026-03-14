# Unity Smart Merge (UnityYAMLMerge)

This repository includes `.gitattributes` rules that tell Git to use a
custom merge driver named `unityyamlmerge` for Unity YAML assets (scenes,
prefabs, and `.asset` files). This reduces merge conflicts by performing a
semantic merge on Unity YAML files.

Files affected (examples):

- `*.unity` (scenes)
- `*.prefab` (prefabs)
- `*.asset` (ScriptableObjects and other Unity YAML assets)

What you need to do locally
--------------------------

1. Ensure you have Unity installed. The repository's project files reference
   Unity at `/Applications/Unity/Hub/Editor/6000.2.5f1/Unity.app` but any recent
   Unity installation with `UnityYAMLMerge` will work.

2. Configure your local git to point the `unityyamlmerge` driver at the
   UnityYAMLMerge executable. You can either run the included setup script
   (from the repo root):

```bash
./scripts/setup-unity-smart-merge.sh
```

The script will try to find `UnityYAMLMerge` on your PATH or at
`/Applications/Unity/Hub/Editor/6000.2.5f1/Unity.app`. You can also set the
environment variable `UNITY_EDITOR_PATH` to the Unity.app folder to point it
to another installation:

```bash
UNITY_EDITOR_PATH=/Applications/Unity/Hub/Editor/2022.3.20f1/Unity.app ./scripts/setup-unity-smart-merge.sh
```

3. Alternatively, configure git manually (local repo only):

```bash
git config --local merge.unityyamlmerge.name "Unity Smart Merge"
git config --local merge.unityyamlmerge.driver "/path/to/UnityYAMLMerge merge -p %O %A %B %L %P"
```

Notes
-----
- This repository configures the attributes for Unity YAML files, but Git
  merge drivers are local settings and are not stored in the repo. Each
  developer must configure their own git (or run the setup script).
- On Windows the UnityYAMLMerge executable path lives inside the Unity
  installation (e.g. `C:\Program Files\Unity\Hub\Editor\<version>\Editor\Data\Tools\UnityYAMLMerge.exe`). Adjust the path accordingly.
- If your team uses CI pipelines that perform merges, consider configuring
  the merge driver in CI (most CI runners allow setting repo-local git
  config) so merges performed by the pipeline also use Smart Merge.

References
----------
- Unity Manual: Smart Merge (UnityYAMLMerge)
