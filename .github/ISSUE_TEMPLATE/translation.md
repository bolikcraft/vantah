---
name: Translation
about: Fix a wrong translation or offer a new interface language
title: ''
labels: i18n
assignees: ''
---

Русский язык приветствуется — писать по-английски необязательно.

Only the Russian and English strings were written by the authors; every other
language is an unreviewed draft. Corrections are welcome, however small.

## Language

Language and culture code (e.g. `de`, `pt-BR`, `zh-Hans`):

## What is wrong

For each string, give the key (the `name` attribute in
`src/Vantah.App/Localization/Strings.<code>.resx`), the current value and the
suggested one:

| Key | Current | Suggested |
| --- | --- | --- |
|  |  |  |

## New language?

If you are proposing a language that does not exist yet, see the translation
section of [CONTRIBUTING.md](../../CONTRIBUTING.md) — a new language needs a
`.resx` file, an entry in `CultureSelector.Supported` and an entry in
`ConfigViewModel.AllLanguages`. A pull request is more convenient than an issue,
but an issue is fine too.

## Notes

Placeholders such as `{0}` must be preserved; product names (`Vantah`,
`AdGuard VPN`, `adguardvpn-cli`), CLI tokens (`socks`, `tun`, `auto`, `none`,
`script`, `http2`, `quic`, `release`, `beta`, `nightly`, `default`) and
abbreviations (`SOCKS`, `DNS`, `TUN`, `IPv4`, `IPv6`, `PID`) are not translated.
