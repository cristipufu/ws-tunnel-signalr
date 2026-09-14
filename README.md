# ⚡ Tunnelite

[![CI](https://img.shields.io/github/actions/workflow/status/cristipufu/tunnelite/ci.yml?branch=master&label=CI&logo=github)](https://github.com/cristipufu/tunnelite/actions/workflows/ci.yml)
[![CD](https://img.shields.io/github/actions/workflow/status/cristipufu/tunnelite/deploy.yml?branch=master&label=CD&logo=microsoftazure)](https://github.com/cristipufu/tunnelite/actions/workflows/deploy.yml)
[![NuGet](https://img.shields.io/nuget/v/Tunnelite?logo=nuget&label=NuGet)](https://www.nuget.org/packages/Tunnelite/)
[![NuGet downloads](https://img.shields.io/nuget/dt/Tunnelite?logo=nuget&label=downloads)](https://www.nuget.org/packages/Tunnelite/)
[![Release](https://img.shields.io/github/v/release/cristipufu/tunnelite?logo=github&label=release)](https://github.com/cristipufu/tunnelite/releases/latest)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Tunnelite is a .NET tool that lets you set up a secure connection between a public web address and an application running on your local machine. It effectively makes your local app accessible from the internet.

```bash
# macOS / Linux with Homebrew
brew install tunnelite
tunnelite http://localhost:3000
```

You can also install the [.NET tool](#net-tool) or download a [standalone binary](#standalone-binaries).

**Website:** [tunnelite.com](https://tunnelite.com) · **Webhook tester:** [webhooks.tunnelite.com](https://webhooks.tunnelite.com)

## Contents

- [Use Cases](#-use-cases)
- [Installation](#-installation)
- [Usage](#-usage)
- [How It Works](#-how-it-works)
- [Self-Hosting](#-self-hosting)
- [Packages](#-packages)
- [Tests](#-tests)
- [License](#-license)

## 🚀 Use Cases

- Exposing locally-hosted web applications to the internet for testing or demo purposes.
- Quickly sharing dev builds during hackathons.
- Testing and debugging webhook integrations.
- Providing internet access to services running behind firewalls without exposing incoming ports.

## 📦 Installation

### Homebrew

On macOS and Linux, install from [Homebrew Core](https://formulae.brew.sh/formula/tunnelite):

```bash
brew install tunnelite
```

Homebrew installs the required .NET dependency automatically. No additional tap is needed.

To update an existing installation:

```bash
brew update
brew upgrade tunnelite
```

### .NET tool

Requires the .NET 10 SDK. To install Tunnelite as a global tool, use the following command:

```bash
dotnet tool install --global Tunnelite
```

To update an existing installation:

```bash
dotnet tool update --global Tunnelite
```

### Standalone binaries

Every [release](https://github.com/cristipufu/tunnelite/releases/latest) also ships a single self-contained executable, built with NativeAOT, that needs no .NET runtime installed:

| Platform | Archive |
| --- | --- |
| Linux x64 | `tunnelite-linux-x64.tar.gz` |
| Linux arm64 | `tunnelite-linux-arm64.tar.gz` |
| macOS Intel | `tunnelite-osx-x64.tar.gz` |
| macOS Apple Silicon | `tunnelite-osx-arm64.tar.gz` |
| Windows x64 | `tunnelite-win-x64.zip` |

A `SHA256SUMS` file is attached to each release for verification.

## 💻 Usage

Once installed, you can use the `tunnelite` command to create a tunnel to your local application:

```bash
tunnelite http://localhost:3000
```

This command returns a public URL with an auto-generated subdomain.

![The tunnelite CLI connecting a local application to a public URL](https://github.com/cristipufu/tunnelite/blob/master/docs/tunnelite-cli.gif?raw=true)

| Argument / option | Description |
| --- | --- |
| `<localUrl>` | The local URL to tunnel to. Use `http://` or `https://` for web applications, or `tcp://host:port` for raw TCP (self-hosted servers only). |
| `--publicUrl <url>` | The tunnel server to connect to. Defaults to `https://tunnelite.com`. |
| `--log <level>` | Client log verbosity: `Trace`, `Debug`, `Information`, `Warning`, `Error` or `Critical`. |

While the tunnel is running, press `c` to clear the screen and `q` to quit.

## 🔍 How It Works

Tunnelite creates a bridge between your local application and the internet using a WebSocket connection. It streams incoming data from a public URL directly to your local server, making your local app accessible from anywhere.

The managed version of Tunnelite supports HTTP(S), WebSocket (WS/WSS) and Server-Sent Events tunneling. If you need TCP tunneling, you'll have to host the server yourself.

### HTTP connection

```mermaid
sequenceDiagram
    actor EC as External Client
    participant PTS as Public Tunneling Server<br/>(Multiple Pods)
    participant AS as Azure SignalR
    participant TC as Tunneling Client
    participant IS as Intranet Server

    EC->>PTS: 1. HTTP Request
    PTS->>AS: 2. Notify Client (based on subdomain)
    AS-->>TC: 3. WSS Notification (contains pod name)
    TC->>PTS: 4. HTTP GET (with headers for pod routing)
    PTS-->>TC: 5. Stream HTTP Request Body
    TC->>IS: 6. Forward Request
    IS-->>TC: 7. Forward Response
    TC->>PTS: 8. HTTP POST (with headers for pod routing)
    PTS-->>EC: 9. Stream Response
    Note right of PTS: Steps 4-5 and 8-9 are routed<br/>to the same pod based on<br/>HTTP headers
```

<details>
<summary><b>TCP overview</b></summary>

<br/>

```mermaid
sequenceDiagram
    actor TC as Tunneling Client
    box rgb(222, 235, 247) Public Tunneling Server
        participant WS as WebSocket Handler
        participant TL as TCP Listener
    end
    actor EC as External Client

    TC->>WS: Connect via WebSocket
    TC->>WS: Register tunnel request
    WS->>TL: Start listening on random port
    TL-->>WS: Port number
    WS-->>TC: Tunnel registered (port number)

    Note over TC,EC: Some time later

    EC->>TL: Connect to generated port
    TL->>WS: New TCP connection
    WS-->>TC: Notify of new TCP connection
    TC->>WS: Begin tunneling data
    WS<<->>TC: Bi-directional data transfer
    TL<<->>EC: Bi-directional data transfer
```

</details>

<details>
<summary><b>TCP connection</b></summary>

<br/>

```mermaid
sequenceDiagram
    actor EC as External Client
    participant PTS as Public Tunneling Server<br/>(Multiple Pods)
    participant AS as Azure SignalR
    participant TC as Tunneling Client
    participant IS as Intranet Server

    EC->>PTS: 1. TCP Connection
    PTS->>AS: 2. Notify Client (NewTcpConnection)
    AS-->>TC: 3. WSS Notification (contains connection details)
    TC->>IS: 4. Open TCP Connection
    TC->>PTS: 5. Start StreamIncomingAsync
    PTS-->>TC: 6. Stream TCP Data (incoming)
    TC->>IS: 7. Forward Incoming Data
    IS-->>TC: 8. Send Outgoing Data
    TC->>PTS: 9. StreamOutgoingAsync (with outgoing data)
    PTS-->>EC: 10. Forward Outgoing Data
    Note right of PTS: Steps 5-6 and 9-10 use<br/>bi-directional streaming<br/>over SignalR
```

</details>

## 🏠 Self-Hosting

Tunnelite allows you to self-host the server, giving you full control over your tunneling infrastructure. To set up your own Tunnelite server, you'll need:

- Wildcard SSL certificate for your domain
- Wildcard DNS record pointing to your server's IP address

These allow Tunnelite to create secure subdomains for your tunnels and properly route traffic to your self-hosted server. The server project is `src/Tunnelite.Server`; the [deploy workflow](.github/workflows/deploy.yml) shows how the managed instance is published to Azure App Service.

Once your server is up, point the client at it:

```bash
tunnelite http://localhost:3000 --publicUrl https://your-server-domain
```

## 📚 Packages

| Package | Description |
| --- | --- |
| [Tunnelite](https://www.nuget.org/packages/Tunnelite/) | The `tunnelite` command-line tool. |
| [Tunnelite.Sdk](https://www.nuget.org/packages/Tunnelite.Sdk/) | `HttpTunnelClient` and `TcpTunnelClient` for opening tunnels from your own .NET code. |
| [Tunnelite.AspNetCore](https://www.nuget.org/packages/Tunnelite.AspNetCore/) | `app.UseTunnelite()` middleware that tunnels an ASP.NET Core app as soon as it starts. |

```csharp
var app = builder.Build();

app.UseTunnelite(); // prints the public URL once the tunnel is connected

app.Run();
```

## 🧪 Tests

`test/Tunnelite.Tests` starts the tunnel server and a local app in-process and drives real traffic through the tunnel: HTTP, Server-Sent Events, WebSockets (including large and fragmented messages), TCP, and a client speaking the original WebSocket wire format. Set `TUNNELITE_CLI` to a built `tunnelite` binary to also run the CLI end-to-end.

```bash
dotnet test test/Tunnelite.Tests
```

CI runs the suite on every pull request, plus a NativeAOT publish of the CLI that is tested the same way.

## 📄 License

This project is licensed under the [MIT License](LICENSE).
