# Service Availability Monitoring and Alerting (SAMA)

## About

SAMA is a utility service aimed toward IT use. It monitors configured services to determine whether they are up or down and sends Slack alerts when such services' statuses change.

SAMA is written in C# for .NET Core. As such, it is cross-platform and can run on minimal, isolated environments, such as a Raspberry Pi.

## Features

- Global configuration for timeouts, check intervals, and retries (in `appsettings.json` or `appsettings.[EnvironmentName].json`)
- Web-based configuration for HTTP/HTTPS endpoints to monitor
- Optional checking for keywords when determining endpoint status
- Overview page with large, easy to interpret statuses

## Installation

Pre-compiled binaries are not provided. To compile, the .NET Core 2.0 (preview 2 or above) SDK needs to be installed. After that, simply running `dotnet publish` should be enough to perform a basic compilation.

Due to the current status of .NET Core's support for the Raspberry Pi, there is a helper batch file (`publish-rpi.bat`) provided to ease the compilation process.

The `rpi-prereqs` directory contains, among other things, a systemd service file that can help with installing SAMA as a daemon on a Raspberry Pi (or other systemd-based Linux systems).

## Usage

Once up and running, accessing the service will redirect the browser to the Overview page, `/Endpoints/Index`. To create, modify, and delete endpoints, navigate to `/Endpoints/List` and use the links on that page to perform the aforementioned actions.

The `appsettings.json` file has default settings for SAMA. They should be modified to fit your needs - including the addition of a Slack web hook (see the [Slack API docs](https://api.slack.com/custom-integrations/incoming-webhooks) for more info).

## Future planned/wished-for features (PRs are welcome!)

- Per-endpoint overrides for timeout, interval, retry settings
- Moving global configuration into database for web-based modification
- Expanded notification options beyond Slack (email, IFTTT?)
- Checks beyond HTTP/HTTPS (ping, TCP, more?)
- TLS checks for certificate expiration, validity
- Saving of up/down events in database, historical trending
- Saving of response times for more in-depth historical trending
- HTTP/HTTPS authentication option
- HTTP/HTTPS multi-step checks (e.g., log in and then verify keyword presence)
- Ability to accept HTTP/HTTPS non-200 responses as success

## License

This software is released under the ISC License.
