---
name: release-jellylearning
description: Cut a JellyLearning release - changelog, tests, tag, GitHub Actions release and manifest.json update, then verify the catalog install. Use when asked to tag, release, ship or publish a new JellyLearning version.
---

# Release JellyLearning

Tagging and pushing publish to a public repo: **get the user's go before tagging**, and say which version.
Versions are `vX.Y.Z` (new feature: minor; fix: patch). The tag fires `.github/workflows/build.yml`, which runs the
tests, builds `JellyLearning_X.Y.Z.zip` (+ `.sha256`), creates the GitHub release, and commits the new version to
`manifest.json` on `main` (`build/update-manifest.py`). Remotes: `origin` pushes to GitHub **and** Forgejo.

## Steps

1. `git status` clean, on `main`, and `git pull --rebase origin main` first (the manifest commits come from CI).
2. Add the release to the `changelog` in `build.yaml` (newest first, one short entry per version): it is the release
   notes and the catalog entry. Check `build/release-notes.py` prints sensibly.
3. `dotnet test`, then `build/package.sh vX.Y.Z` and `unzip -l artifacts/release/*.zip`: the plugin dll, Markdig.dll
   and meta.json must be at the zip root. Delete `artifacts/release` afterwards. Commit and push the changelog.
4. Tag and push: `git tag -a vX.Y.Z -m "JellyLearning X.Y.Z"` then `git push origin vX.Y.Z`.
5. Watch GitHub (Actions tab; `gh` is not installed here, and unauthenticated API calls work only for a public repo):
   `test`, `release`, and the "manifest.json: add vX.Y.Z" commit by github-actions on `main`. Then `git pull`.
6. Verify the catalog: use the `test-catalog-install` skill against the published manifest.
7. Tell the user the release URL and the result; remind them the Forgejo mirror has no CI manifest commit until pulled
   and pushed.

## If the release job fails after the tag exists

- A draft release may exist: the user deletes it on GitHub first (otherwise `gh release create` says it exists).
- Fix the workflow, push to `main`, then either move the tag (`git tag -f -a vX.Y.Z -m …`, `git push -f origin vX.Y.Z`;
  auto mode blocks the force-push, so give the user the commands) or tag the next patch version (simpler).
- History: `v1.0.0` failed because `artifacts/release/*` also matched the staging folder; the glob is now `*.zip` and
  `*.sha256`. Any new step in the release job should be tried locally first.

## Don't

- Don't edit `manifest.json` by hand for JellyLearning versions (checksum and URL come from the built zip).
- Don't re-tag a version users may already have installed once the catalog lists it.
- File Transformation's entry in `manifest.json` is a copy of its author's (12.2 build, checksum included); update it
  by copying from `https://www.iamparadox.dev/jellyfin/plugins/manifest.json` when a new version appears.
