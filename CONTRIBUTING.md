# Contributing to Vantah

Vantah is an unofficial GUI front-end for `adguardvpn-cli` on Linux. Bug reports,
translations and pull requests are welcome.

## Project layout

- `src/Vantah.Core` — the non-UI core: running the CLI, parsing its output,
  configuration, storage, localization plumbing. No Avalonia references here;
  everything is covered by xUnit tests.
- `src/Vantah.App` — the Avalonia 12 application: views, view models, tray icon,
  resource strings.
- `tests/Vantah.Core.Tests`, `tests/Vantah.App.Tests` — xUnit tests. UI tests run
  on headless Avalonia and need neither an X server nor `xvfb-run`.

New logic belongs in `Vantah.Core` whenever it can be expressed without UI types —
that is the part that can be tested properly.

## Building and running

Requirements: **.NET 10 SDK** and the `adguardvpn-cli` tool installed and
available on `PATH` (Vantah does not bundle it; a valid AdGuard VPN
account/subscription is needed for it to do anything).

```bash
dotnet build Vantah.slnx
dotnet run --project src/Vantah.App
```

A plain build is framework-dependent and fast; the self-contained single-file
publish is described in the [README](README.md).

## Tests

```bash
dotnet test Vantah.slnx
```

Everything must pass before a pull request is opened, and the CI workflow
(`.github/workflows/ci.yml`, build + tests on `ubuntu-latest`) must be green on
the branch. A red CI run is a blocker regardless of how small the change looks.

## Translations

Translations are the most useful contribution to Vantah, and the easiest one:
they touch resource files only, no C# knowledge required.

### Where the strings live

- All UI strings are in `src/Vantah.App/Localization/Strings.<code>.resx`.
- `Strings.resx` is the **neutral** resource and its values are **English**
  (`<NeutralLanguage>en</NeutralLanguage>` in `Vantah.App.csproj`), which is why
  there is no `Strings.en.resx` — the `en` culture reads the neutral resource
  directly, and so does any locale Vantah does not translate.
- `Strings.resx` is therefore also the **reference for translators**: translate
  from it.
- The key set is declared as constants in
  `src/Vantah.App/Localization/LocKeys.cs`. Every language file must contain
  **exactly the same keys** as the neutral one — no missing keys, no extra keys.

### Rules

1. **Never change the `name` attribute of a `data` element.** Translate the
   `<value>` only.
2. **Placeholders `{0}`, `{1}`, … must survive the translation.** They are
   substituted with `string.Format`; a lost or invented placeholder is not a
   cosmetic issue but a `FormatException` at runtime (or a value that silently
   disappears). Their order inside the sentence may differ from the original if
   the target language requires it — the set of placeholders is what must match.
3. **Do not translate these:**
   - the product names `Vantah`, `AdGuard VPN`, and the tool name
     `adguardvpn-cli`;
   - CLI tokens shown in the UI as-is: `socks`, `tun`, `auto`, `none`, `script`,
     `http2`, `quic`, `release`, `beta`, `nightly`, `default` — they are the
     literal values the CLI accepts;
   - the abbreviations `SOCKS`, `DNS`, `TUN`, `IPv4`, `IPv6`, `PID`.
4. Keep the register and length close to the original: labels sit on buttons and
   in a tray menu, and an overly long string will be clipped.

### Adding a new language

Three places, all of which are checked by tests:

1. Add `src/Vantah.App/Localization/Strings.<code>.resx` — copy
   `Strings.resx` and translate the values. `<code>` is a .NET culture name
   and is not necessarily two letters: where the region matters, use the full
   name (`pt-BR`, `zh-Hans`). The file name must match the code exactly.
2. Add the same code to `CultureSelector.Supported` in
   `src/Vantah.Core/Localization/CultureSelector.cs`.
3. Add a `LanguageOption` to `ConfigViewModel.AllLanguages` in
   `src/Vantah.App/ViewModels/ConfigViewModel.cs`, with the language written in
   **its own name** (`Deutsch`, `Português (Brasil)`, `简体中文`), not in English
   or Russian.

### Fixing an existing translation

Only the English and Russian strings were written by the authors. **Every other
language is a draft produced by a machine translation model and has not been
reviewed by a native speaker.** If a string reads unnatural, wrong or too long —
a correction is very welcome, even a one-line one. There is no need to review a
whole file; fix what you noticed.

### Verifying

```bash
dotnet test Vantah.slnx
```

`tests/Vantah.App.Tests/Localization/LocalizerTests.cs` catches the usual
mistakes:

- a language file whose key set differs from the neutral one (missing or extra
  keys);
- a lost or added `{0}`-style placeholder;
- a missing satellite assembly — a language that silently falls back to the
  neutral English strings instead of its own;
- a key present in `LocKeys.cs` but not in `Strings.resx`, and vice versa.

## Commits and pull requests

- [Conventional Commits](https://www.conventionalcommits.org/): `feat(scope): …`,
  `fix(scope): …`, `refactor(...)`, `docs: …`, `chore: …`, `test: …`.
- The subject line in this repository is written in Russian; English is
  acceptable too. Keep it short and in the imperative/descriptive form used by
  the existing history (`git log --oneline -30`).
- The body explains **why**, not what — what changed is visible in the diff.
  State the problem the change solves and, when relevant, the alternative that
  was rejected.
- Commit messages are written in the author's own voice, as plain prose. Do not
  add trailers about tooling, generators or assistants (including
  `Co-Authored-By:` for anything that is not a human co-author), and do not
  mention them in the message text.
- One logical change per commit; keep unrelated reformatting out.
- Pull requests target `main` and must build and pass tests.

## Language

Русский язык приветствуется в issues и pull request'ах — писать по-английски
необязательно. То же касается сообщений коммитов: история проекта ведётся
на русском.
