## What and why

Describe the problem the change solves. What changed is visible in the diff — the
description should explain the reason.

Closes #

## Type of change

- [ ] Bug fix
- [ ] New feature
- [ ] Translation (new language or a correction)
- [ ] Refactoring / docs / build

## Checklist

- [ ] `dotnet build Vantah.slnx` succeeds
- [ ] `dotnet test Vantah.slnx` passes
- [ ] CI is green on this branch
- [ ] Commit messages follow Conventional Commits, explain the reason in the
      body, and carry no trailers about tooling
- [ ] Logic that does not need the UI lives in `Vantah.Core` and is covered by
      tests
- [ ] For UI changes: checked in a running application, not only in a build

## Translations only

- [ ] Key set matches `Strings.resx` (no missing or extra keys)
- [ ] Placeholders `{0}`, `{1}`, … are preserved
- [ ] Product names, CLI tokens and abbreviations left untranslated
- [ ] For a new language: `.resx` file, `CultureSelector.Supported` and
      `ConfigViewModel.AllLanguages` all updated

## Screenshots

For UI changes, before/after.
