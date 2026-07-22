# Third-party notices

Vantah itself is distributed under the GNU General Public License v3.0 or later
(see [LICENSE](LICENSE)). It uses the third-party components listed below, each
under its own license. The list is derived from the `PackageReference` entries in
`src/Vantah.App/Vantah.App.csproj` and `src/Vantah.Core/Vantah.Core.csproj`
(`Vantah.Core` has no package references of its own).

Vantah does **not** include, bundle or distribute `adguardvpn-cli` or any other
AdGuard software; it merely runs the CLI as an external process. That tool is
installed by the user and covered by AdGuard's own license and terms.

## NuGet packages

| Component | Version | Purpose | License | Project |
| --- | --- | --- | --- | --- |
| Avalonia | 12.1.0 | Cross-platform UI framework Vantah is built on | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Desktop | 12.1.0 | Desktop (X11/Wayland) platform backends for Avalonia | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Themes.Fluent | 12.1.0 | Fluent visual theme for Avalonia controls | MIT | https://github.com/AvaloniaUI/Avalonia |
| Avalonia.Fonts.Inter | 12.1.0 | Bundled Inter font family used as the default UI font | MIT (package); the font itself is under SIL OFL 1.1, see below | https://github.com/AvaloniaUI/Avalonia |
| FluentAvaloniaUI | 3.0.1 | Additional Fluent-styled controls and dialogs | MIT | https://github.com/amwx/FluentAvalonia |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM primitives (observable properties, commands, source generators) | MIT | https://github.com/CommunityToolkit/dotnet |
| AvaloniaUI.DiagnosticsSupport | 2.2.3 | Bridge to the Avalonia Developer Tools; **Debug builds only** — excluded from any non-Debug configuration and therefore not part of released binaries | See note below | https://avaloniaui.net/ |

Licenses were read from the `license`/`licenseExpression` metadata of the
packages in the local NuGet cache (`~/.nuget/packages/<id>/<version>/<id>.nuspec`).

### Note on AvaloniaUI.DiagnosticsSupport

The package's `.nuspec` declares no `license` expression and ships no license
file; it is published by AvaloniaUI OÜ (`Copyright 2019-2026 © AvaloniaUI OÜ`) as
part of the Avalonia Developer Tools tooling. In `Vantah.App.csproj` it is
referenced with `IncludeAssets=None` / `PrivateAssets=All` for every
configuration other than `Debug`, so it is a development-time dependency only and
is not redistributed with Vantah releases.

## Fonts

**Inter** — the UI font, shipped inside the `Avalonia.Fonts.Inter` package.
Licensed under the **SIL Open Font License, Version 1.1**.
Project: https://github.com/rsms/inter — license text:
https://openfontlicense.org/

## .NET

Vantah targets `net10.0` and its self-contained builds embed the .NET runtime,
which is distributed by Microsoft under the MIT license
(https://github.com/dotnet/runtime).

## Country flags

The flag images in `src/Vantah.App/Assets/flags/` are rendered from
[flag-icons](https://github.com/lipis/flag-icons) (the 4x3 set) by Panayiotis Lipiridis,
licensed under the MIT License. A copy of that license ships with the repository as
`assets/flags/LICENSE.flag-icons`; `assets/flags/render.sh` regenerates the PNGs from
the upstream SVGs.
