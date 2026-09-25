# Submitting to winget-pkgs

The repository keeps four manifest templates under `winget/`. The
`.github/workflows/winget-publish.yml` workflow turns those templates into a
clean, current-version manifest set after a signed GitHub release is available.

## Signing requirement

The distributed `.msixbundle` must be signed by a certificate chain trusted by
Windows. The release workflow supports either:

- SignPath Foundation via `SIGNPATH_API_TOKEN` plus the SignPath repository variables.
- A publicly trusted PFX code-signing certificate via `SIGNING_CERT_BASE64` and
  `SIGNING_CERT_PASSWORD`.

A self-signed certificate is not sufficient for winget validation and produces
`0x800B0109 / CERT_E_UNTRUSTEDROOT`.

## What CI now generates

For the requested release tag, `Publish to WinGet`:

1. Downloads `DefinitionForCommandPalette.msixbundle` from the GitHub Release.
2. Computes the bundle `InstallerSha256`.
3. Extracts `AppxSignature.p7x` and computes `SignatureSha256`.
4. Normalizes the release tag to a four-part `PackageVersion`.
5. Updates the manifest schema/version to 1.12.0.
6. Packages exactly these four current-version files:
   - `ruslanlap.DefinitionForCommandPalette.yaml`
   - `ruslanlap.DefinitionForCommandPalette.installer.yaml`
   - `ruslanlap.DefinitionForCommandPalette.locale.en-US.yaml`
   - `ruslanlap.DefinitionForCommandPalette.locale.fr-FR.yaml`

Historical folders under `winget/` are intentionally excluded from the
artifact so a submission cannot accidentally contain multiple package versions.

## Submission procedure

1. Ensure the release workflow completed and the GitHub Release contains the
   trusted-signed `DefinitionForCommandPalette.msixbundle`.
2. Run `Publish to WinGet` for that release tag.
3. Download the `winget-manifests-<tag>` artifact.
4. Copy the four files into:
   `manifests/r/ruslanlap/DefinitionForCommandPalette/<PackageVersion>/`
   in the `microsoft/winget-pkgs` fork.
5. Validate with:
   ```powershell
   winget validate --manifest <manifest-directory>
   ```
6. Commit, push, and open/update the upstream PR.
7. Wait for the winget validation pipeline and reviewer approval.

For the current v1.0.4 submission, upstream PR #431811 has already confirmed
that the only remaining validation blocker is the untrusted certificate chain.
Once a trusted-signed bundle replaces the release asset, regenerate the
manifests and update that PR with the new hashes.
