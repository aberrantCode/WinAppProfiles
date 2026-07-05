---
name: release-to-main
description: Use when the user wants to merge dev into main for a production release — covers rebasing dev from main if behind, automatic semantic versioning from conventional commits, merge commit, release tagging, and syncing dev back onto main.
installed-from: llm_skills
---

# Release to Main

Promotes `dev` → `main` as a versioned production release.
Run this when `dev` is stable and ready to ship.

---

## Prerequisites: Detect the production branch and target repo

This skill says `main` throughout for readability, but the production branch may be
named differently (`master`, `release`, …). **Detect it from the remote — never
assume.** Likewise, derive the GitHub repo from `origin`, so a fork that also has an
`upstream` remote never targets the wrong repository.

```bash
git fetch origin --prune

# Production branch: read origin/HEAD; refresh it, then probe as a last resort.
PROD=$(git symbolic-ref --quiet --short refs/remotes/origin/HEAD 2>/dev/null | sed 's|^origin/||')
if [ -z "$PROD" ]; then
  git remote set-head origin -a >/dev/null 2>&1
  PROD=$(git symbolic-ref --quiet --short refs/remotes/origin/HEAD 2>/dev/null | sed 's|^origin/||')
fi
if [ -z "$PROD" ]; then
  for cand in main master; do
    git ls-remote --heads origin "$cand" | grep -q . && PROD="$cand" && break
  done
fi

# Target repo: always derive from origin. NEVER bare `gh repo view` — for a fork with
# an `upstream` remote it resolves to the UPSTREAM parent, so the release would tag and
# publish against the wrong repository. Handing gh the origin URL pins it to the fork.
REPO=$(gh repo view "$(git remote get-url origin)" --json nameWithOwner -q .nameWithOwner 2>/dev/null) \
  || REPO=$(git remote get-url origin | sed -E 's#.*github.com[:/]##; s#\.git$##')

echo "Production branch: $PROD"
echo "Target repo:       $REPO"

git ls-remote --heads origin "$PROD"
git ls-remote --heads origin dev
```

If `$PROD` or `$REPO` cannot be resolved, or either branch is missing on the remote,
stop and tell the user — do not create or guess them.

### Rename gate: prefer `main` over `master` — before any other action

If the production branch is `master`, **stop and offer to rename it to `main` first**.
This is the preferred convention; do the rename before assessing state, versioning, or
merging — nothing else happens until this is resolved.

```bash
if [ "$PROD" = "master" ]; then
  echo "Production branch is 'master'. The preferred convention is 'main'."
fi
```

When `$PROD` is `master`, ask via **AskUserQuestion** (this gate runs first, ahead of Step 1):

```
AskUserQuestion(
  questions: [{
    question: "This repo's production branch is 'master'. Rename it to 'main' before releasing?",
    header: "Rename branch",
    options: [
      { label: "Yes — rename master → main", description: "GitHub-side atomic rename: moves the default branch, retargets open PRs, adds redirects. Then continue the release into main." },
      { label: "No — release into master", description: "Keep master as the production branch for this release." }
    ]
  }]
)
```

- **Yes** — perform the rename via GitHub's branch-rename API (atomic: moves the
  default-branch pointer, retargets open PRs, sets up redirects), then resync locally and
  set `PROD=main`:

  ```bash
  gh api -X POST "repos/$REPO/branches/master/rename" -f new_name=main \
    --jq '{name: .name}'

  git branch -m master main 2>/dev/null || true   # rename local branch if present
  git fetch origin --prune
  git branch -u origin/main main 2>/dev/null || true
  git remote set-head origin -a >/dev/null 2>&1
  PROD=main
  echo "Renamed. Production branch is now: $PROD"
  ```

  If `master` is a protected branch, the rename API still works and carries the
  protection rule to `main`; no manual reprotection is needed.

- **No** — keep `PROD=master` and continue. Every `main` reference below means `master`.

### Substitution convention

**For the rest of this skill, every `main` / `origin/main` means `$PROD` / `origin/$PROD`,
and every `gh … --repo` uses the `$REPO` resolved here — not `gh repo view` auto-detection.**

---

## Workflow

Follow every step in order. Do not skip steps, do not reorder them.

### Step 1 — Fetch and assess state

```bash
git fetch origin
```

Capture two counts:

```bash
# How many commits main has that dev does not (dev is behind main)
BEHIND=$(git rev-list origin/dev..origin/main --count)

# How many commits dev has that main does not (the release payload)
AHEAD=$(git rev-list origin/main..origin/dev --count)

echo "dev is $BEHIND behind main, $AHEAD ahead of main"
```

- If `$AHEAD` is 0 — there is nothing to release. Stop and tell the user.
- If `$BEHIND` > 0 — dev must be rebased onto main before the merge (Step 2).
- If `$BEHIND` is 0 — skip Step 2 and go straight to Step 3.

---

### Step 2 — Rebase dev onto main (only if dev is behind)

Switch to dev and stash any uncommitted local work first:

```bash
git checkout dev

# Stash everything including untracked files
git stash --include-untracked

# Rebase dev onto the latest main
git rebase origin/main
```

If rebase produces conflicts:
1. Show conflicting files with `git status`.
2. Resolve obvious ones yourself. For non-obvious conflicts, use **AskUserQuestion**:
   ```
   AskUserQuestion(
     questions: [{
       question: "Rebase conflict in <files>. How would you like to proceed?",
       header: "Conflicts",
       options: [
         { label: "I'll resolve manually", description: "Pause — you fix the files, then tell me to continue" },
         { label: "Abort the rebase", description: "Run git rebase --abort and stop the workflow" }
       ]
     }]
   )
   ```
3. After resolution: `git add <resolved-files>` then `git rebase --continue`.
4. If unresolvable: `git rebase --abort` — stop and report to user.

After a clean rebase, restore stashed work and force-push dev:

```bash
git stash pop   # only if stash was created
git push --force-with-lease origin dev
```

---

### Step 3 — Determine the next version (automatic)

Get the most recent tag. **Guard: never fall back to a fake `v0.0.0` string** — that string is not in git's object store and will cause `git log v0.0.0..HEAD` to fail with exit 128.

```bash
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null)

if [ -z "$LAST_TAG" ]; then
  IS_FIRST_RELEASE=true
  echo "No previous tags found — this is the first release. Starting from v0.0.0."
  LAST_TAG="(none)"
else
  IS_FIRST_RELEASE=false
  echo "Last release: $LAST_TAG"
fi
```

Parse MAJOR, MINOR, PATCH from `$LAST_TAG` (strip leading `v`; use `0.0.0` as the base when `IS_FIRST_RELEASE=true`). Then scan commit subjects to determine the bump type — **use a range only when a real previous tag exists**:

```bash
if [ "$IS_FIRST_RELEASE" = true ]; then
  # No real previous tag — scan all commits on dev
  git log origin/dev --format="%s"
else
  git log "$LAST_TAG"..origin/dev --format="%s"
fi
```

Apply conventional commit rules (in priority order):

| Pattern in any commit subject or body | Bump |
|---------------------------------------|------|
| `BREAKING CHANGE` in body, or `!:` in subject (e.g. `feat!:`) | **major** |
| Subject starts with `feat:` or `feat(` | **minor** |
| Anything else (`fix:`, `chore:`, `refactor:`, `docs:`, etc.) | **patch** |

Compute the three candidate versions (new_patch, new_minor, new_major) and present them via **AskUserQuestion**, pre-selecting the auto-detected bump:

```
AskUserQuestion(
  questions: [{
    question: "What version should this release be tagged as?",
    header: "Version",
    options: [
      { label: "vX.Y.Z+1", description: "Patch bump (auto-detected from commits) (Recommended)" },
      { label: "vX.Y+1.0", description: "Minor bump" },
      { label: "vX+1.0.0", description: "Major bump" }
    ]
  }]
)
```

Store the chosen version as `$VERSION`.

---

### Step 4 — Confirm the release

Show a summary panel to the user, then ask for confirmation via **AskUserQuestion**:

```
Release summary:
  From branch  : dev
  Into branch  : main
  Last release : $LAST_TAG
  New version  : $VERSION
  Commits      : $AHEAD commits

AskUserQuestion(
  questions: [{
    question: "Merge dev into main as $VERSION?",
    header: "Confirm",
    options: [
      { label: "Yes — ship it", description: "Proceed with merge, tag, and dev sync" },
      { label: "No — abort", description: "Stop the workflow without making any changes" }
    ]
  }]
)
```

If the user aborts, stop cleanly with no git changes made.

---

### Step 5 — Merge dev into main

```bash
git checkout main
```

Before pulling or merging, check whether local `main` has diverged from `origin/main`:

```bash
LOCAL_AHEAD=$(git rev-list origin/main..HEAD --count)
LOCAL_BEHIND=$(git rev-list HEAD..origin/main --count)
echo "local main is $LOCAL_AHEAD ahead, $LOCAL_BEHIND behind origin/main"
```

Act on the result:

| State | Action |
|-------|--------|
| `LOCAL_AHEAD=0, LOCAL_BEHIND=0` | In sync — proceed |
| `LOCAL_AHEAD=0, LOCAL_BEHIND>0` | Pull only: `git pull origin main` |
| `LOCAL_AHEAD>0, LOCAL_BEHIND=0` | Local main has unpushed commits — use **AskUserQuestion** (see below) |
| Both > 0 (diverged) | **STOP** — local and remote main have diverged; do not merge. Report to user and abort |

**When local main is ahead of origin/main**, use **AskUserQuestion**:

```
AskUserQuestion(
  questions: [{
    question: "Local main has $LOCAL_AHEAD commit(s) not on origin/main:\n<git log --oneline origin/main..HEAD>\nHow should these be handled?",
    header: "Local commits",
    options: [
      { label: "Push them to origin/main first", description: "Publish the local commits, then continue the release merge" },
      { label: "Abort — I need to review these commits", description: "Stop the workflow without making any changes" }
    ]
  }]
)
```

- If **Push first**: `git push origin main`, then continue.
- If **Abort**: stop cleanly.

Once local and remote main are in sync, perform the merge:

```bash
git merge --no-ff origin/dev -m "release: $VERSION"
git push origin main
```

`--no-ff` creates a merge commit that preserves the full dev history on main.

---

### Step 6 — Tag and publish the release

**Guard: check the tag doesn't already exist before pushing.**

```bash
if git rev-parse "$VERSION" >/dev/null 2>&1; then
  # Tag already exists — stop and ask user
  AskUserQuestion(
    questions: [{
      question: "Tag $VERSION already exists locally or on remote. How would you like to proceed?",
      header: "Tag conflict",
      options: [
        { label: "Pick a different version", description: "Go back and choose a new version string" },
        { label: "Abort", description: "Stop the workflow without making any further changes" }
      ]
    }]
  )
fi
```

```bash
git tag "$VERSION"
git push origin "$VERSION"
```

Confirm the tag is visible:

```bash
git tag --list "$VERSION"
```

Then **publish a GitHub Release** from the tag. This is required for any repo using
`/releases/latest` API (e.g. install.ps1 remote installers). A bare git tag is NOT
sufficient — the API returns 404 until a Release is published.

**Guard: reuse the `$REPO` resolved in Prerequisites** — derived from `origin`, never
bare `gh repo view` (which targets a fork's `upstream` parent and can also fail with
exit 128 on unusual remote URLs). Re-derive only if `$REPO` was lost between Bash
invocations:

```bash
if [ -z "$REPO" ]; then
  REPO=$(gh repo view "$(git remote get-url origin)" --json nameWithOwner -q .nameWithOwner 2>/dev/null) \
    || REPO=$(git remote get-url origin | sed -E 's#.*github.com[:/]##; s#\.git$##')
fi
if [ -z "$REPO" ]; then
  echo "ERROR: Could not resolve GitHub repo from origin."
  echo "Check: gh auth status && git remote -v"
  # STOP — do not proceed with gh commands
fi
echo "GitHub repo (from origin): $REPO"
```

Summarise commits since the previous tag to generate release notes. **Guard: only use a git range when the previous tag resolves as a real git object** — using a fake tag string (e.g. `v0.0.0` that was never actually tagged) causes `git log` to fail with exit 128, and `2>/dev/null` would silently produce empty notes.

```bash
if [ "$IS_FIRST_RELEASE" = true ]; then
  # No real previous tag — include all commits reachable from $VERSION
  NOTES=$(git log "$VERSION" --format="- %s")
else
  # Find the tag immediately before $VERSION
  PREV_TAG=$(git describe --tags --abbrev=0 "${VERSION}^" 2>/dev/null)
  if [ -z "$PREV_TAG" ]; then
    # No prior tag found — fall back to all commits
    NOTES=$(git log "$VERSION" --format="- %s")
  else
    # Validate PREV_TAG resolves before using it in a range
    if git rev-parse "$PREV_TAG" >/dev/null 2>&1; then
      NOTES=$(git log "$PREV_TAG..$VERSION" --format="- %s")
    else
      echo "WARNING: Previous tag '$PREV_TAG' cannot be resolved — falling back to all commits"
      NOTES=$(git log "$VERSION" --format="- %s")
    fi
  fi
fi

# Guard: if notes are empty, provide a fallback rather than publishing a blank release
if [ -z "$NOTES" ]; then
  NOTES="(release notes unavailable — check git log manually)"
fi
```

Create the release, always passing `--repo` explicitly:

```bash
gh release create "$VERSION" \
  --title "$VERSION" \
  --notes "$NOTES" \
  --repo "$REPO"
```

Confirm:

```bash
gh release view "$VERSION" --repo "$REPO" \
  --json tagName,publishedAt \
  --jq '"Released: \(.tagName) at \(.publishedAt)"'
```

---

### Step 7 — Sync dev from main and push

After the merge, main has a new merge commit that dev does not. Bring dev forward so it includes that commit and the two branches stay in sync:

```bash
git checkout dev
git rebase origin/main   # replays dev's commits on top of main's new HEAD
git push --force-with-lease origin dev
```

Because dev was just merged into main, this rebase is typically a fast-forward and produces no conflicts.

If rebase conflicts arise here (rare — just merged), use the same **AskUserQuestion** conflict pattern from Step 2.

---

### Step 8 — Show final state

```bash
git log --oneline -5 origin/main
echo "---"
git log --oneline -3 origin/dev
```

Report: PR complete, version tagged, dev synced.

---

## Quick Reference

```
0. Detect $PROD (from origin/HEAD) and $REPO (from origin); if $PROD=master, AskUserQuestion to rename → main
1. Ensure $PROD and dev exist on remote
2. git fetch origin — check BEHIND / AHEAD counts
3. [if BEHIND > 0] Rebase dev onto main + force-push dev
4. Determine next version from conventional commits
5. Confirm release with user (AskUserQuestion)
6. git merge --no-ff origin/dev -m "release: $VERSION" → push main
7. git tag $VERSION → push tag
8. Rebase dev onto main + force-push dev
9. Show final log
```

---

## Error Recovery

| Situation | Recovery |
|---|---|
| `$AHEAD` is 0 | Nothing to release — stop and tell user |
| No previous tags (`IS_FIRST_RELEASE=true`) | Normal — start from `0.0.0`, scan all commits on dev without a range |
| Rebase conflict (Step 2 or 7) | Use AskUserQuestion: resolve manually or abort |
| Local main ahead of origin/main | Use **AskUserQuestion**: push local commits first, or abort |
| Local main diverged from origin/main | **STOP** — do not merge; report to user and abort |
| Push to main rejected | `git pull --rebase origin main` then retry — never force-push main |
| Tag already exists | Guard in Step 6 catches this — use AskUserQuestion: pick a different version or abort |
| `$REPO` unset / origin not resolvable | **STOP** — `$REPO` is derived from `origin` (never bare `gh repo view`, which targets a fork's upstream parent). Run `gh auth status` and `git remote -v` to diagnose; do not run any `gh` commands without `$REPO` |
| Repo is a fork with an `upstream` remote | Correct behaviour: `$REPO` from `origin` targets the fork. Bare `gh repo view` would have targeted the upstream parent. Expect version-tag collisions with upstream — normal for a fork |
| Production branch is `master`, not `main` | Prerequisites' rename gate offers to rename `master` → `main` via the GitHub API before any other action. If declined, `$PROD=master` and every `main` in this skill means `master` |
| Production branch detection failed (`$PROD` empty) | `origin/HEAD` was unset and main/master probing found nothing. Run `git remote set-head origin -a`, or set `$PROD` manually, then re-run |
| `gh` not authenticated | `gh auth login` — pause until authenticated |
| Release notes empty after `git log` | Fallback message already set — check git log manually; do not suppress with `2>/dev/null` in note generation |
| dev ahead count wrong after rebase | Re-run `git fetch origin` and recount before proceeding |
