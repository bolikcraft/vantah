#!/bin/sh
# Package post-install / post-remove scriptlet (deb + rpm).
# Refreshes the desktop and icon caches so the Vantah menu entry and its icon
# appear (or disappear) immediately, without a re-login. Every step is guarded
# so a missing helper never fails the package transaction.
set -e

if command -v update-desktop-database >/dev/null 2>&1; then
    update-desktop-database /usr/share/applications >/dev/null 2>&1 || true
fi

if command -v gtk-update-icon-cache >/dev/null 2>&1; then
    gtk-update-icon-cache -qtf /usr/share/icons/hicolor >/dev/null 2>&1 || true
fi

exit 0
