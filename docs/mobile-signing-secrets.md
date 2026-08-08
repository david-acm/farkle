# Mobile signing secrets — encode & rotate runbook

The device build workflows (`CD - Mobile Device Builds`, #338) sign the HotDice MAUI app with material
that **must never live in the repo**. The reference app kept it in Azure DevOps *secure files*; the
GitHub translation is **base64-encoded repository secrets**, decoded at build time into temp files /
a temp keychain and torn down after the run.

Until these secrets are set, the build jobs still run and produce an **unsigned** artifact (with a
warning) — so the pipeline is provable before the store credentials exist. Signing turns on
automatically once the secrets are present.

> Symptom → cause → fix notes live in `docs/lessons-learned.md`; this file is the setup/rotate procedure.

## Android — upload keystore

Google Play App Signing holds the real app key; CI signs with the **upload key** (see
`docs/play-app-signing.md`). Generate it once and keep the `.keystore` **and its passwords** in a
password manager — losing them means you can't publish updates.

```bash
keytool -genkey -v -keystore hotdice-upload.keystore \
  -alias hotdice -keyalg RSA -keysize 2048 -validity 10000
```

Encode for GitHub:

```bash
base64 -w0 hotdice-upload.keystore   # macOS: base64 -i hotdice-upload.keystore
```

Set these **repository secrets** (Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | the base64 string above |
| `ANDROID_KEYSTORE_PASSWORD` | keystore store password |
| `ANDROID_KEY_ALIAS` | `hotdice` (the `-alias`) |
| `ANDROID_KEY_PASSWORD` | key password (often same as the store password) |

## iOS — distribution certificate + provisioning profile

Needs an Apple Developer Program account (#341). Export the **Apple Distribution** certificate as a
`.p12` (with a password) from Keychain Access, and download the **App Store** provisioning profile
(`.mobileprovision`) for `com.davidacm.hotdice` from the Apple Developer portal.

```bash
base64 -i AppleDistribution.p12          > dist.p12.b64
base64 -i HotDice_AppStore.mobileprovision > profile.b64
```

Set these **repository secrets**:

| Secret | Value |
|---|---|
| `IOS_DIST_CERT_P12_BASE64` | base64 of the `.p12` |
| `IOS_DIST_CERT_PASSWORD` | the `.p12` export password |
| `IOS_PROVISIONING_PROFILE_BASE64` | base64 of the `.mobileprovision` |

The iOS job imports the `.p12` into a **temporary** keychain, installs the profile, extracts the
signing identity + profile name, and deletes the keychain and profile on `always()`.

## Versioning (no secret, but part of the release contract)

`.github/scripts/mobile-version.sh` stamps a **Play-safe monotonic** version:

- `version_code = github.run_number` — strictly increasing (Play's hard requirement) and far under
  the **2,100,000,000** `versionCode` ceiling (the script fails loudly if it's ever exceeded). This
  is the robust GitHub translation of the reference app's ADO daily-counter `<yyyyMMdd><dailyRev>`
  scheme, which can regress across day boundaries or a >99-builds/day day; `run_number` cannot.
- The build **date** is preserved for humans in `build_version` (`marketing.yyyyMMdd.run`), which
  feeds `CFBundleVersion` and the artifact name.

Covered by `tests/scripts/mobile-version.test.sh` (`bash tests/scripts/mobile-version.test.sh`).

## Rotation

1. Generate the new keystore/cert as above.
2. Update the corresponding repository secret(s) with the new base64 value.
3. Re-run `CD - Mobile Device Builds` (Actions → Run workflow) and confirm a signed artifact.
4. **Android:** the upload key can be reset via Play Console → App integrity if the upload key is
   lost (the app-signing key held by Google is unaffected). **iOS:** revoke the old certificate in
   the Apple Developer portal after the new one is confirmed working.
5. Delete any local `.b64`, `.p12`, `.keystore` scratch files.

## Guardrails

- No signing material is committed — CodeQL/secret-scanning must stay clean. The workflows read
  **secret names** only; values come from the encrypted store at run time.
- Secrets are decoded to `$RUNNER_TEMP` (ephemeral) and removed on `always()`.
