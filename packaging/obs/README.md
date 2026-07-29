# OBS: репозиторий deb/rpm с автообновлением

Файлы для [openSUSE Build Service](https://build.opensuse.org). OBS собирает из
одного `.spec` пакеты сразу под несколько дистрибутивов — и rpm (Fedora, openSUSE),
и deb (Debian, Ubuntu) — и раздаёт их репозиторием. В отличие от файлов на странице
релиза, подключённый репозиторий даёт пользователю обычное `dnf update` / `apt upgrade`.

Сборка идёт из **готового бинаря** релиза, а не из исходников: у воркеров OBS нет
.NET SDK и нет доступа в сеть во время сборки.

## Содержимое

- `vantah.spec` — сборка пакета из релизного `tar.gz`
- `_service` — скачивание архива с GitHub и проверка sha256 (`mode="manual"`,
  запускается вручную при смене версии)
- `vantah.changes` — журнал изменений в формате openSUSE

## Первая публикация

1. Аккаунт на https://build.opensuse.org (учётка openSUSE).
2. Домашний проект `home:<логин>` создаётся автоматически. В нём: **Create Package**,
   имя `vantah`.
3. Залить три файла — через веб-интерфейс (Add file) или через `osc`:

   ```bash
   osc checkout home:<логин>
   cd home:<логин>
   osc mkpac vantah
   cp /path/to/vantah/packaging/obs/{vantah.spec,_service,vantah.changes} vantah/
   cd vantah
   osc service manualrun     # скачает архив релиза и проверит сумму
   osc add *
   osc commit -m "Initial package: vantah 0.3.4"
   ```

4. В настройках проекта (**Repositories → Add from a distribution**) добавить цели:
   openSUSE Tumbleweed, Fedora (последние две версии), Debian, Ubuntu LTS. Каждая
   цель — отдельная сборка, статус виден на странице пакета.
5. Когда все цели зелёные, на странице проекта появляется кнопка **Download package**
   с инструкцией подключения репозитория для каждого дистрибутива — её и давать
   пользователям.

`osc` ставится из пакета `osc` (в Debian/Ubuntu и Fedora он есть; на ALT — нет,
тогда проще заливать через веб-интерфейс).

## Обновление на новый релиз

1. В `_service` заменить версию в `path` и `filename`, в `verify_file` — sha256
   (берётся из файла `.sha256` рядом с ассетом релиза).
2. В `vantah.spec` поднять `Version:`.
3. Добавить запись сверху в `vantah.changes`.
4. `osc service manualrun && osc commit -m "Update to 0.3.5"`.

## Проверено локально

`vantah.spec` собран `rpmbuild` в контейнере `fedora:latest`: пакет получается,
раскладка совпадает с `.deb`/`.rpm` из nfpm и с AUR (`/usr/lib/vantah` + симлинк
в `/usr/bin`). Замечания `rpmlint`, которые остаются и это нормально:

- `no-manual-page-for-binary` — man-страницы у GUI-приложения нет;
- `desktopfile-without-binary` — проверка не разбирает симлинк `/usr/bin/vantah`;
- `spelling-error` на `adguardvpn` и `cli` — словарь rpmlint.
