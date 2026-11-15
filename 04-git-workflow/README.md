# Keep changes to the same application reviewable

Begin at a clean local checkpoint of your practice project or a clone of this
reference repository. From that Git root, inspect:

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
