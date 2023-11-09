# Service Availability Monitoring and Alerting (SAMA)

## About

SAMA is a utility service aimed toward IT use. It monitors configured services to determine whether they are up or down and sends Slack alerts when such services' statuses change.

SAMA is written in C# for .NET 6. As such, it is cross-platform and can run on minimal, isolated environments, such as a Raspberry Pi.

## Features

- Web-based global configuration for timeouts, check intervals, and retries
- Web-based configuration for HTTP/HTTPS and ICMP Ping endpoints to monitor
- Optional checking for keywords when determining endpoint status
- Overview page with large, easy to interpret statuses
- Ability to accept HTTP/HTTPS non-200 responses as success
- User access control for modifying configuration (local and LDAP)

## Installation

### Docker

SAMA is [Dockerized](https://github.com/sep/sama/pkgs/container/sama). Linux-based images are available for amd64, armv7, and arm64 platforms.

When running Dockerized SAMA, the following should be taken into account:

- Mount the volume `/opt/sama-docker` for database persistence: `-v /my/host/location:/opt/sama-docker`
- By default, SAMA listens on port 80: use `-p 80:80` to expose it
- If running behind a reverse proxy, enable "forwarded"-type headers: `-e ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`

SAMA has the ability to listen on other ports as well as use HTTPS. For example:

```
-e ASPNETCORE_URLS="https://*:443"
-e ASPNETCORE_Kestrel__Certificates__Default__Path=/opt/sama-docker/ssl.pem
-e ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/opt/sama-docker/ssl.key
-e ASPNETCORE_Kestrel__EndpointDefaults__Protocols=Http1AndHttp2
```

If the above parameters are specified and the `ssl.pem` and `ssl.key` files exist, then SAMA will listen on port 443 with HTTPS.

Basic example:

```
docker volume create sama_data
docker run -d --name=sama --restart=unless-stopped -p 80:80 -v sama_data:/opt/sama-docker ghcr.io/sep/sama:latest
```

### Docker Compose (recommended)

Based on the above Docker information, a `/opt/sama-docker/docker-compose.yml` file might look as follows:

```yaml
version: "3.9"
services:
  sama:
    image: ghcr.io/sep/sama:latest
    restart: unless-stopped
    ports:
      - "443:443"
    environment:
      - ASPNETCORE_Kestrel__Certificates__Default__Path=/opt/sama-docker/ssl.pem
      - ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/opt/sama-docker/ssl.key
      - ASPNETCORE_Kestrel__EndpointDefaults__Protocols=Http1AndHttp2
      - ASPNETCORE_URLS=https://*:443
      - DefaultHttpVersion=11_OrHigher
    volumes:
      - /opt/sama-docker:/opt/sama-docker
```

### Manual compilation

Individual pre-compiled binaries are not currently provided. To compile, the .NET 6.0 SDK needs to be installed. After that, simply running `dotnet publish` should be enough to perform a basic compilation.

There is a helper batch file (`publish-rpi.bat`) provided to ease the compilation process for the Raspberry Pi.

The `rpi-prereqs` directory contains, among other things, a systemd service file that can help with installing SAMA as a daemon on a Raspberry Pi (or other systemd-based Linux systems).

## Usage

Once up and running, accessing the service will redirect the browser to the Overview page. To create, modify, and delete endpoints, log in and click the Endpoints link.

The `appsettings.json` file has default settings for SAMA. They should be modified to fit your needs by creating a copy of the file and naming it `appsettings.Production.json`. Modifications should go into that file.

The `DefaultHttpVersion` setting, available via the app settings files and overridable via environment variables, controls how the .NET HTTP client accesses HTTP(S) endpoints. By default, it uses HTTP 1.1 with a fallback to HTTP 1.0. In some unusual circumstances, you may need to change that behavior. This setting allows such changes. The following values are supported (on most platforms):

- `10_Exact`
- `10_OrHigher`
- `11_OrLower` (default)
- `11_Exact`
- `11_OrHigher`
- `20_OrLower`
- `20_Exact`
- `20_OrHigher`
- `30_OrLower`
- `30_Exact`
- `30_OrHigher`

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

SAMA is supported by SEP: a Software Product Design + Development company. If you'd like to [join our team](https://sep.com/careers-at-sep/open-positions/), don't hesitate to get in touch!
