# Keep changes to the same application reviewable

Begin at a clean local checkpoint of your practice project or a clone of this
reference repository. From that Git root, inspect:

If the practice project has no Git history, run `git init` in its root first.
Review `.gitignore` and `git status --short`; keep credentials, local session
files, and build outputs out of the checkpoint. For the earlier practice files:

```text
git add -- .gitignore global.json feature-brief.md contracts.md learning-notes.md src tests .kiro
git diff --cached
git commit -m "Save reviewed local consent checkpoint"
```

Inspect the staged diff before committing. Configure your own Git identity if
requested. A cloned reference already has history; skip the initialization step.
If a listed path is missing, check the folder and earlier exercise outputs first.

From the saved Git root:

```text
git status --short
git diff
```

Create one branch for a small consent change:

```text
git switch -c feature/consent-review
```

Make a small reviewed change, run the relevant checks, and inspect the diff
before committing. Configure your own Git name and email if Git requests them.
Do not copy credentials into configuration committed with the project.

For an optional second working copy, choose unused branch and folder names:

```text
git worktree add ../consent-tests -b feature/consent-tests
git worktree list
```

Give each agent one branch and one workspace. Separate folders do not prevent
conflicts in shared contracts. Review changes together and rerun the checks.
Do not let multiple agents edit the same working copy concurrently.

When the second worktree is clean and its work is preserved, remove it using
`git worktree remove ../consent-tests`. Inspect its status first; do not use a
force option to discard unreviewed work. Publishing a branch uses your own fork
or authorized repository; it is not necessary for the local exercise.
