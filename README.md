# Service Availability Monitoring and Alerting (SAMA)

## About

SAMA is a utility service aimed toward IT use. It monitors configured services to determine whether they are up or down and sends Slack alerts when such services' statuses change.

SAMA is written in C# for .NET Core. As such, it is cross-platform and can run on minimal, isolated environments, such as a Raspberry Pi.

## Features

- Web-based global configuration for timeouts, check intervals, and retries
- Web-based configuration for HTTP/HTTPS and ICMP Ping endpoints to monitor
- Optional checking for keywords when determining endpoint status
- Overview page with large, easy to interpret statuses
- Ability to accept HTTP/HTTPS non-200 responses as success
- User access control for modifying configuration (local and LDAP)

## Installation

Pre-compiled binaries are not currently provided. To compile, the .NET Core 2.0 SDK needs to be installed. After that, simply running `dotnet publish` should be enough to perform a basic compilation.

There is a helper batch file (`publish-rpi.bat`) provided to ease the compilation process for the Raspberry Pi.

The `rpi-prereqs` directory contains, among other things, a systemd service file that can help with installing SAMA as a daemon on a Raspberry Pi (or other systemd-based Linux systems).

## Usage

Once up and running, accessing the service will redirect the browser to the Overview page. To create, modify, and delete endpoints, log in and click the Endpoints link.

The `appsettings.json` file has default settings for SAMA. They should be modified to fit your needs by creating a copy of the file and naming it `appsettings.Production.json`. Modifications should go into that file.

## Future planned/wished-for features (PRs are welcome!)

- Per-endpoint overrides for timeout, interval, retry settings
- Expanded notification options beyond Slack (email, IFTTT?)
- More checks (TCP? Others?)
- TLS checks for certificate expiration, validity
- Saving of up/down events in database, historical trending
- Saving of response times for more in-depth historical trending
- HTTP/HTTPS authentication option
- HTTP/HTTPS multi-step checks (e.g., log in and then verify keyword presence)
- Ability to categorize endpoints in the Overview for easier monitoring

## License

This software is released under the ISC License.

---

[![Powered by SEP logo](https://raw.githubusercontent.com/sep/assets/master/images/powered-by-sep.svg?sanitize=true)](https://www.sep.com)

SAMA is supported by SEP: a Software Product Design + Development company. If you'd like to [join our team](https://www.sep.com/careers/open-positions/), don't hesitate to get in touch!


