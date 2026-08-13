# OBS: репозиторий deb/rpm с автообновлением

Файлы для [openSUSE Build Service](https://build.opensuse.org). OBS собирает из
одного `.spec` пакеты сразу под несколько дистрибутивов — и rpm (Fedora, openSUSE),
и deb (Debian, Ubuntu) — и раздаёт их репозиторием. В отличие от файлов на странице
релиза, подключённый репозиторий даёт пользователю обычное `dnf update` / `apt upgrade`.

Сборка идёт из **готового бинаря** релиза, а не из исходников: у воркеров OBS нет
.NET SDK и нет доступа в сеть во время сборки.

## Архитектуры

Поддерживаются x86-64 и arm64. Ассеты релиза у них разные, каталог внутри архива
назван по .NET RID:

| архитектура | ассет релиза | каталог внутри архива |
|-------------|--------------|-----------------------|
| `x86_64` / `amd64` | `vantah-<ver>-linux-x64.tar.gz` | `vantah-<ver>-linux-x64` |
| `aarch64` / `arm64` | `vantah-<ver>-linux-arm64.tar.gz` | `vantah-<ver>-linux-arm64` |

**rpm** обе архитектуры собирает один `vantah.spec`: два `Source`, выбор архива и
каталога через `%ifarch`, `ExclusiveArch: x86_64 aarch64`. `BuildArch` из spec убран
намеренно — он жёстко проставляет архитектуру пакета и одинаков для всех целей, то
есть с ним aarch64-воркер выпустил бы пакет с меткой x86_64.

**deb** так не умеет: архив исходника задаётся в `.dsc` полем `DEBTRANSFORM-TAR`,
а оно указывает ровно один файл и не зависит от архитектуры сборки. Поэтому
arm64-deb вынесен в **отдельный пакет OBS** `vantah-arm64` — минимальное решение,
не требующее ни своих сервисов, ни правки debhelper-логики:

- пакет `vantah` — `Architecture: amd64` в `vantah.dsc`, `DEBTRANSFORM-TAR` на
  x64-архив; на arm64-цели планировщик OBS помечает его «excluded» и не собирает;
- пакет `vantah-arm64` — `Architecture: arm64` в `arm64/vantah-arm64.dsc`,
  `DEBTRANSFORM-TAR` на arm64-архив; `debian.rules` у обоих одинаковый.

Имя исходника у второго пакета другое (`Source: vantah-arm64`), чтобы в индексе
`Sources` не оказалось двух записей `vantah` одной версии. Бинарный пакет в обоих
случаях называется `vantah`, а бинарники разной архитектуры в репозитории apt
уживаются штатно (индексы `Packages` раздельные по архитектурам).

## Содержимое

- `vantah.spec` — сборка rpm из релизного `tar.gz`, x86_64 и aarch64
- `_service` — скачивание архивов с GitHub; режим не указан, то есть сервис
  отрабатывает **на сервере** при каждом коммите в пакет. С `mode="manual"` он
  запускался бы только локальным `osc`, и заливка через веб-интерфейс не дала бы
  исходника вовсе
- `vantah.changes` — журнал изменений в формате openSUSE
- `vantah.dsc`, `debian.control`, `debian.rules`, `debian.changelog` — deb для amd64
- `arm64/vantah-arm64.dsc`, `arm64/debian.control`, `arm64/debian.changelog` —
  deb для arm64 (`debian.rules` берётся тот же, из корня)

## Первая публикация

1. Аккаунт на https://build.opensuse.org (учётка openSUSE).
2. Домашний проект `home:<логин>` создаётся автоматически. В нём: **Create Package**,
   имя `vantah`.
3. Залить файлы — через веб-интерфейс (Add file) или через `osc`:

   ```bash
   osc checkout home:<логин>
   cd home:<логин>
   osc mkpac vantah
   cp /path/to/vantah/packaging/obs/{vantah.spec,_service,vantah.changes} vantah/
   cp /path/to/vantah/packaging/obs/{vantah.dsc,debian.control,debian.rules,debian.changelog} vantah/
   cd vantah
   osc add *
   osc commit -m "Initial package: vantah 0.3.4"   # сервис отработает на сервере
   ```

4. В настройках проекта (**Repositories → Add from a distribution**) добавить цели:
   openSUSE Tumbleweed, Fedora (последние две версии), Debian, Ubuntu LTS. Каждая
   цель — отдельная сборка, статус виден на странице пакета.
5. Когда все цели зелёные, на странице проекта появляется кнопка **Download package**
   с инструкцией подключения репозитория для каждого дистрибутива — её и давать
   пользователям.

`osc` ставится из пакета `osc` (в Debian/Ubuntu и Fedora он есть; на ALT — нет,
тогда проще заливать через веб-интерфейс).

## Что нужно доделать руками в веб-интерфейсе OBS для arm64

Ни одно из этих действий не описывается файлами в репозитории — только настройками
проекта и пакета на build.opensuse.org.

1. **Добавить архитектуры к существующим целям.** Project → **Repositories** →
   у каждой цели **Edit repository** → в списке Architectures отметить `aarch64`.
   Имена архитектур в OBS свои: `aarch64` одинаково означает и rpm-архитектуру
   `aarch64`, и deb-архитектуру `arm64` — отдельного пункта `arm64` в списке нет.
   Цели, где `aarch64` недоступен, оставить как есть.
2. **Создать второй пакет** `vantah-arm64` (**Create Package**) и залить в него:
   `arm64/vantah-arm64.dsc`, `arm64/debian.control`, `arm64/debian.changelog`,
   `debian.rules` (из корня, без изменений), `_service` и **копию `vantah.spec`**.
   Копия spec нужна не ради rpm, а потому что URL arm64-архива сервис
   `download_files` берёт именно из `Source1`; в `.dsc` адресов нет.
3. **Выключить rpm-цели у пакета `vantah-arm64`.** Package `vantah-arm64` →
   **Repositories** (или Meta → флаги `<build>`) → отключить все rpm-репозитории
   (openSUSE, Fedora) и оставить включёнными только Debian/Ubuntu. Иначе из копии
   spec соберутся вторые, лишние rpm с тем же именем и версией. В Meta это:

   ```xml
   <build>
     <disable/>
     <enable repository="Debian_12" arch="aarch64"/>
     <enable repository="xUbuntu_24.04" arch="aarch64"/>
   </build>
   ```

   (имена репозиториев подставить свои).
4. **Учесть, что arm64 на OBS собирается эмуляцией** (qemu-user на x86-воркерах),
   если у проекта нет нативных arm-воркеров. Для нас это почти не больно: сборка
   ничего не компилирует, только распаковывает архив и раскладывает файлы, но
   старт сборочного окружения под эмуляцией всё равно занимает единицы-десятки
   минут против секунд на x86-64. Первую зелёную сборку стоит просто подождать,
   а не считать зависшей.

## Обновление на новый релиз

1. В `vantah.spec` поднять `Version:` — URL обоих архивов собираются из неё,
   сервис `download_files` скачает новые сами.
2. В `vantah.dsc` и `arm64/vantah-arm64.dsc` поднять `Version:` и имя архива
   в `DEBTRANSFORM-TAR`.
3. Добавить записи сверху в `vantah.changes`, `debian.changelog` и
   `arm64/debian.changelog`.
4. `osc commit -m "Update to 0.3.6"` в обоих пакетах. Через веб то же самое:
   заменить содержимое файлов, сборка перезапустится.

## Проверено локально

`vantah.spec` собран `rpmbuild` в контейнере `fedora:latest`: пакет получается,
раскладка совпадает с `.deb`/`.rpm` из nfpm и с AUR (`/usr/lib/vantah` + симлинк
в `/usr/bin`). Замечания `rpmlint`, которые остаются и это нормально:

- `no-manual-page-for-binary` — man-страницы у GUI-приложения нет;
- `desktopfile-without-binary` — проверка не разбирает симлинк `/usr/bin/vantah`;
- `spelling-error` на `adguardvpn` и `cli` — словарь rpmlint.

Многоархитектурный вариант проверен на синтетических архивах с той же раскладкой,
что у релизных (реальных arm64-ассетов на момент правки ещё нет):

- `rpmspec -P --target x86_64|aarch64` — разворачивается нужный `%setup`
  и нужный каталог (`vantah-<ver>-linux-x64` / `vantah-<ver>-linux-arm64`);
- `rpmbuild -bb --target x86_64` и `--target aarch64` в `fedora:latest` — оба
  пакета собираются, `%{ARCH}` выходит `x86_64` и `aarch64`, списки файлов
  совпадают;
- `dpkg-buildpackage -b -aarm64` и `-aamd64` в `debian:12` с
  `arm64/debian.control` и `debian.control` — получаются `vantah_<ver>-1_arm64.deb`
  (`Architecture: arm64`) и `vantah_<ver>-1_amd64.deb`, раскладка та же.

Не проверено вживую: сама сборка на OBS с реальными arm64-ассетами и работа
`download_files` со вторым `Source` — первый релиз с arm64 надо прогнать руками.
