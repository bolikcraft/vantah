#
# spec для openSUSE Build Service. Собирает пакет из готового бинаря, который
# уже собран в CI проекта: воркеры OBS не имеют .NET SDK и доступа в сеть, а
# self-contained сборка воспроизводима только тем же SDK, что и в релизе.
#

Name:           vantah
Version:        0.3.7
Release:        0
Summary:        Unofficial GUI and tray front-end for the AdGuard VPN CLI
License:        GPL-3.0-or-later
URL:            https://github.com/bolikcraft/vantah
# Два ассета: сборки под разные архитектуры лежат в разных архивах, каталог внутри
# назван по .NET RID (linux-x64 / linux-arm64), а не по %%{_arch}. download_files
# тянет оба Source вне зависимости от архитектуры сборки — это нормально, лишний
# архив просто не распаковывается.
Source0:        https://github.com/bolikcraft/vantah/releases/download/v%{version}/vantah-%{version}-linux-x64.tar.gz
Source1:        https://github.com/bolikcraft/vantah/releases/download/v%{version}/vantah-%{version}-linux-arm64.tar.gz

# Каталоги /usr/share/icons/hicolor/* принадлежат этому пакету; без него проверка
# openSUSE «directories not owned by a package» роняет сборку.
BuildRequires:  hicolor-icon-theme

%if 0%{?suse_version}
BuildRequires:  update-desktop-files
%endif

# Бинарь self-contained: рантайм .NET внутри. Библиотеки ниже он и Avalonia
# грузят динамически, поэтому автоматические зависимости их не видят.
Requires:       hicolor-icon-theme
Recommends:     libicu
Recommends:     fontconfig

# BuildArch здесь не указывается намеренно: он жёстко задаёт архитектуру готового
# пакета и одинаков для всех целей сборки, из-за чего на aarch64-воркере получился бы
# пакет с меткой x86_64. Архитектура берётся от цели сборки, а список допустимых —
# из ExclusiveArch: под остальные архитектуры ассетов релиза просто нет.
ExclusiveArch:  x86_64 aarch64

# Бинарь уже собран, отладочные пакеты из него не извлечь.
%global debug_package %{nil}
%global __strip /bin/true

%description
Vantah is a desktop GUI client for Linux - a window plus a system-tray icon -
that acts as a front-end for the official adguardvpn-cli command-line tool. It
does not implement a VPN itself: it runs adguardvpn-cli as an external process,
parses its output and shows the state in a graphical interface.

Vantah is an independent, unofficial project. It is not affiliated with AdGuard,
and is not developed, sponsored or endorsed by them. It does not include, bundle
or distribute adguardvpn-cli or any other AdGuard software - install the CLI
yourself; a valid AdGuard VPN account/subscription is required.

%prep
# -T гасит автораспаковку Source0, -b 1 распаковывает Source1 вместо него.
%ifarch x86_64
%setup -q -n vantah-%{version}-linux-x64
%endif
%ifarch aarch64
%setup -q -T -b 1 -n vantah-%{version}-linux-arm64
%endif

%build
# Нечего собирать: в архиве уже лежит готовый бинарь.

%install
install -Dm755 vantah %{buildroot}%{_prefix}/lib/vantah/vantah
install -d %{buildroot}%{_bindir}
ln -s %{_prefix}/lib/vantah/vantah %{buildroot}%{_bindir}/vantah

install -Dm644 share/applications/vantah.desktop \
    %{buildroot}%{_datadir}/applications/vantah.desktop
install -Dm644 share/metainfo/io.github.bolikcraft.vantah.metainfo.xml \
    %{buildroot}%{_datadir}/metainfo/io.github.bolikcraft.vantah.metainfo.xml

for icon in share/icons/hicolor/*/apps/vantah.png; do
    size=$(basename $(dirname $(dirname $icon)))
    install -Dm644 $icon \
        %{buildroot}%{_datadir}/icons/hicolor/$size/apps/vantah.png
done

%if 0%{?suse_version}
%suse_update_desktop_file vantah
%endif

%files
%license LICENSE
%doc README.md
%{_bindir}/vantah
%{_prefix}/lib/vantah
%{_datadir}/applications/vantah.desktop
%{_datadir}/metainfo/io.github.bolikcraft.vantah.metainfo.xml
%{_datadir}/icons/hicolor/*/apps/vantah.png

%changelog
* Fri Aug 14 2026 bolikcraft <bolikcraft@gmail.com> - 0.3.7-0
- Update to 0.3.7; a second tray icon no longer stays behind after the screen
  is unlocked, and quitting from the tray menu no longer crashes.
* Thu Aug 13 2026 bolikcraft <bolikcraft@gmail.com> - 0.3.6-0
- Update to 0.3.6; the interface now defaults to English and settings are no
  longer written to the working directory when ~/.config does not exist yet.
* Tue Aug 04 2026 bolikcraft <bolikcraft@gmail.com> - 0.3.5-0
- Update to 0.3.5; aarch64 builds are now available upstream.
* Wed Jul 29 2026 bolikcraft <bolikcraft@gmail.com> - 0.3.4-0
- Initial OBS package, built from the upstream release tarball.
