#!/bin/sh
set -e

# Sudo-enabled image entrypoint
# Runs as root, conditionally enables sudo access, then drops to app user

if [ "$ENABLE_SUDO" = "true" ]; then
    echo "INFO: ENABLE_SUDO=true, enabling sudo access for app user."
    echo "app ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers
else
    echo "INFO: ENABLE_SUDO is not set to 'true'. Sudo is installed but not enabled."
fi

# Drop privileges and run as app user
exec runuser -u app -- dotnet SAMA.Web.dll "$@"
