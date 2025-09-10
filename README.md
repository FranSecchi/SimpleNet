# SimpleNet

A user-friendly networking solution for Unity games that provides easy-to-use tools for network synchronization, RPC calls, and multiplayer functionality.

## Overview

SimpleNet is a lightweight and efficient networking package for Unity that simplifies the implementation of multiplayer features in your games. Built on top of LiteNetLib, it provides a robust foundation for distributed authority client-server architecture with features like object synchronization, RPC calls, and scene management.

## Features

- **Easy-to-use API**: Simple and intuitive interface for implementing multiplayer functionality
- **Object Synchronization**: Automatic synchronization of transforms, rigidbodies, and custom properties
- **RPC System**: Remote Procedure Calls with support for client-to-server, server-to-client, and bidirectional communication
- **Scene Management**: Built-in support for networked scene loading and object spawning
- **LAN Discovery**: Automatic server discovery on local networks
- **Debug Tools**: Built-in debugging window for monitoring network activity
- **Rollback System**: Support for state reconciliation and prediction
- **Customizable**: Extensible architecture allowing for custom serialization and transport layers

## Requirements

- Unity 2023.1 or later
- .NET Standard 2.0

## Installation

1. Open the Package Manager in Unity
2. Click the "+" button in the top-left corner
3. Select "Add package from git URL"
4. Enter the package URL
5. Click "Add"
6. Install

## Basic Usage

### Transport Setup

SimpleNet uses UDP as its default transport solution, which is optimized for real-time game networking. The UDP transport is automatically configured when you start the network manager, but you can customize its settings or implement your own Transport solution:

```csharp
// Configure UDP transport settings
var udpTransport = new UDPSolution();
udpTransport.SetBandwidthLimit(1024 * 1024); // Set bandwidth limit to 1MB/s
NetManager.SetTransport(udpTransport);

```

The UDP transport provides:
- Reliable and unreliable message delivery
- Automatic packet fragmentation
- Connection management
- Bandwidth control

### Setting up a Server

```csharp
// Create a NetManager component in your scene
var netManager = gameObject.AddComponent<NetManager>();

// Configure server settings
netManager.serverName = "My Game Server";
netManager.maxPlayers = 10;
netManager.useLAN = true; // Enable LAN discovery

// Start the server
NetManager.StartHost();

// Optional: Configure server info before starting
var serverInfo = new ServerInfo
{
    ServerName = "My Game Server",
    MaxPlayers = 10,
    Port = 7777
};
NetManager.StartHost(serverInfo);

//Optional: start LAN broadcast for local game sessions
NetManager.StartBroadcast();
```

### Connecting as a Client

```csharp
// Start as a client
Netmanager.StartClient();
// Connect to a server
NetManager.ConnectTo("localhost"); // or specific IP address
// Optional: find and connect to LAN game sessions
NetManager.StartDiscovery();
List<ServerInfo> servers = NetManager.GetDiscoveredServers();
NetManager.ConnectTo(servers[0].Adress)
```

### Networked Objects

```csharp
// Add NetBehaviour to your GameObject
public class MyNetworkedObject : NetBehaviour
{
    //Synchronized variable
    [Sync] private float health = 100f;
    
    //RPC call
    public void TriggerHit(float damage)
    {
        CallRPC("TakeDamage", damage);
    }
    [NetRPC]
    public void TakeDamage(float amount)
    {
        health -= amount;
    }
}
```

## Distributed Authority Architecture

SimpleNet implements a distributed authority architecture where:

- The server acts as the authority for game state and validation
- Clients can own and control specific objects
- Object ownership can be transferred between clients
- The server validates and reconciles state changes
- Built-in rollback system for handling network latency

### Object Ownership

```csharp
// Request ownership of an object
Own(Netmanager.ConnectionId(), ownChildren);

// Check if local client owns the object
if (IsOwned)
{
    // Perform owner-specific logic
}
```

## Debugging

SimpleNet includes a built-in debugging window to help monitor and troubleshoot network issues:

```csharp
// Open the debug window
Window > SimpleNet > Network Debug

// Enable debug logging
NetManager.DebugLog = true;
```

The debug window provides:
- Real-time network statistics
- Connection status
- Message traffic monitoring
- Object synchronization status
- RPC call tracking

## Best Practices

1. **Object Synchronization**
   - Use the `[Sync]` attribute for variables that need to be synchronized
   - Implement proper interpolation for smooth movement
   - Set appropriate sync precision to balance accuracy and bandwidth

2. **RPC Calls**
   - Use appropriate RPC direction (ClientToServer, ServerToClient, Bidirectional)
   - Validate input on the server side
   - Keep RPC parameters small and efficient

3. **Performance**
   - Use object pooling for frequently spawned objects
   - Implement proper cleanup of network objects

4. **Security**
   - Always validate critical operations on the server
   - Use proper authority checks before performing actions
   - Implement proper authentication if needed

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Author

- Francisco Secchi - FranySan

## Acknowledgments

- Built on top of [LiteNetLib](https://github.com/RevenantX/LiteNetLib)
- Uses [MessagePack](https://github.com/neuecc/MessagePack-CSharp) for efficient serialization
