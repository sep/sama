#!/bin/sh
set -e

# If sudo is installed but ENABLE_SUDO is not set to true, remove sudo access
if command -v sudo >/dev/null 2>&1 && [ "$ENABLE_SUDO" != "true" ]; then
    echo "WARNING: Sudo is installed but ENABLE_SUDO is not set to 'true'. Removing sudo access for security."
    sudo sed -i '/^app /d' /etc/sudoers
fi

exec dotnet SAMA.Web.dll "$@"
