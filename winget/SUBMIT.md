# Submitting to winget-pkgs

This repository ships the winget manifests under `winget/`. The CI workflow
[`.github/workflows/winget-publish.yml`](../.github/workflows/winget-publish.yml)
packages them into an artifact on every Microsoft Store release, but it does
**not** open the upstream PR. Submitting the manifests to
[`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) is a
manual step that takes about five minutes.

## Why the workflow doesn't submit

- `wingetcreate new` requires Sharprompt interactive input. It cannot run
  in CI without a TTY and fails with
  `Sharprompt requires an interactive environment`.
- `wingetcreate update` requires the package to already exist in
  `microsoft/winget-pkgs`. This package does not.
- The winget-pkgs GitHub App is the recommended path, but configuring it is
  a separate project.

## When you need to do this

Every time you bump `PackageVersion` in `winget/*.yaml`. The CI artifact is
the source of truth — pull it after the Store publish completes.

## Procedure

1. Wait for the `Publish CmdPal Extension to Microsoft Store` workflow run
   to complete successfully.
2. Download the `winget-manifests-<tag>` artifact from the matching
   `Publish to WinGet` run.
3. Fork [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs)
   if you have not already.
4. Create a branch named after the package and version:
   ```
   git checkout -b ruslanlap/DefinitionForCommandPalette-1.0.4
   ```
5. Copy the manifests into the canonical path:
   ```
   cp winget-manifests-1.0.4.zip /tmp/
   cd /tmp && unzip winget-manifests-1.0.4.zip -d manifests
   mkdir -p upstream/manifests/r/ruslanlap/DefinitionForCommandPalette/1.0.4.0
   cp -r manifests/* upstream/manifests/r/ruslanlap/DefinitionForCommandPalette/1.0.4.0/
   ```
6. Update `PackageVersion`, `InstallerUrl`, and `InstallerSha256` in the
   manifests to match the new release. The current values point at v1.0.0
   and need to be bumped before each submission.
7. Validate locally with `winget validate --manifest <path>`.
8. Commit, push, open a PR upstream.
9. Watch the validation checks. Common expected labels for this package
   type (MSIX bundle with no primary executable):
   - `Validation-Executable-Error`
   - `Validation-No-Executables`

   These are routine for MSIX-only packages. Comment on the PR explaining
   that the install path is `Plugins/DefinitionForCommandPalette` under the
   PowerToys Run installation directory, and link the relevant section of
   `deploy.yml` if the moderator asks.
10. Wait for a moderator to merge. Do not ping more than twice.

## Why hand-write instead of using wingetcreate

Hand-written manifests are easier to review, easier to bump across
versions, and easier to keep in sync with the rest of this repo's release
process. The win is small for new packages but compounds across versions.