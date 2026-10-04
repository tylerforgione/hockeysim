# Preserve hidden random state in saves

Sports outcomes should be uncertain to the user, but fresh randomness on reload
would allow repeated retries until a favorable result appears. Preserve hidden
random state so the same save and actions produce the same result within the
same engine version. This also makes failures reproducible in tests and bug
investigations; it does not promise identical outcomes across engine upgrades.

The engine version is the release version; see
[ADR 0005](0005-self-contained-desktop-builds.md#versioning).
