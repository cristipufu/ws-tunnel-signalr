# Tunnelite

Tunnelite is a .NET tool that allows you to create a secure tunnel from a public URL to your local application running on your machine. 

## Installation

To install Tunnelite as a global tool, use the following command:

```bash
dotnet tool install --global Tunnelite
```

## Usage

Once installed, you can use the `tunnelite` command to create a tunnel to your local application. For example, to tunnel a local application running at http://localhost:3000, run:
```bash
tunnelite http://localhost:3000
```

This command returns a public URL with an auto-generated subdomain, such as `https://abc123.tunnelite.com`. 

## How It Works

Tunnelite works by establishing a websocket connection to the public server and streaming all incoming data to your local application, effectively forwarding requests from the public URL to your local server.

## HTTP Tunneling

<details>
  <summary>Http Connection Diagram</summary>

  <br/>

![image info](https://github.com/cristipufu/ws-tunnel-signalr/blob/master/docs/http_tunneling.png)

</details>



## TCP Tunneling

<details>
  <summary>TCP Overview Diagram</summary>

  <br/>

![image info](https://github.com/cristipufu/ws-tunnel-signalr/blob/master/docs/tcp_tunneling_global.png)

</details>

<details>
  <summary>TCP Connection Diagram</summary>
 
  <br/>
 
![image info](https://github.com/cristipufu/ws-tunnel-signalr/blob/master/docs/tcp_tunneling.png)

</details>



