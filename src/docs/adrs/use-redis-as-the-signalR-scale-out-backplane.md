ADR-001: Use Redis as the SignalR Scale-Out Backplane

**Status**: Accepted
**Date**: 2026-09-03

## Context

Chat.Api exposes SignalR hubs used by the React client for real-time communication. The application will be deployed to Kubernetes with multiple Chat.Api instances (pods) for horizontal scaling and high availability. A SignalR connection is established with a specific Chat.Api instance. For example:

                    Kubernetes Service
                    /       |       \
                   /        |        \
              Chat.Api   Chat.Api   Chat.Api
                Pod A      Pod B      Pod C
                  │          │          │
                User A     User B     User C

If a client connected to Pod A needs to receive a SignalR message while the message is initiated by Pod B, Pod B does not inherently know about the SignalR connections maintained by Pod A. Without a SignalR scale-out mechanism, messages sent using APIs such as Clients.User(...), Clients.Group(...), or Clients.All(...) may not reach clients connected to other instances. Kubernetes Service load balancing does not solve this problem because it distributes incoming connections; it does not synchronize SignalR connection state between application instances.

**Decision**

Use Redis as the SignalR scale-out backplane. Chat.Api will use the official ASP.NET Core SignalR Redis integration:

```

builder.Services
.AddSignalR()
.AddStackExchangeRedis(redisConnectionString);

```

Each Chat.Api instance will connect to the same Redis instance/cluster.

Something like this:

                       Redis
                    SignalR Backplane
                   /       |       \
                  /        |        \
             Pod A       Pod B       Pod C
               │           │           │
             User A      User B      User C

When a SignalR invocation originates from one instance, the SignalR Redis backplane allows the other SignalR instances to receive the invocation and deliver it to their locally connected clients. Redis is therefore used for cross-instance SignalR communication, not as the transport for the actual WebSocket connections.

**Consequences**

**Pros**:

* Allows Chat.Api to scale horizontally across multiple Kubernetes pods.
* SignalR clients can connect to any Chat.Api instance.
* Clients.User(...), Clients.Group(...), and similar SignalR operations can work across instances.
* No custom application-level synchronization of SignalR connections is required.
* The React client does not need to know which Kubernetes pod it is connected to.

**Cons**:

* Introduces Redis as an additional infrastructure dependency.
* Redis becomes part of the real-time messaging path.
* Redis availability and capacity must be monitored.
* Redis backplane traffic increases as the number of SignalR instances and messages increases.

### Important Considerations

Redis is not the source of truth for chat messages and is not intended to replace the application’s durable messaging infrastructure. RabbitMQ remains responsible for application events and asynchronous processing. The database remains the source of truth for persisted chat messages. SignalR/Redis is responsible only for real-time client notification and cross-instance SignalR delivery. The architecture is therefore:

                    ┌──────────────┐
                    │   RabbitMQ   │
                    │ App Events   │
                    └──────┬───────┘
                           │
                    ┌──────▼───────┐
                    │  Chat.Api    │
                    │ SignalR Hub  │
                    └──────┬───────┘
                           │
                        Redis
                     SignalR scale-out
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
           Pod A         Pod B        Pod C
              │            │            │
           Clients      Clients      Clients

**Alternatives Considered**

Kubernetes Session Affinity

Rejected as the primary solution. Session affinity can keep a client associated with the same pod, but it does not allow one SignalR instance to communicate with clients connected to another instance.

**Single Chat.Api Instance**

Rejected because it prevents horizontal scaling and creates a single point of failure.

**Azure SignalR Service**

Not selected at this time. It could be considered in the future if operating the SignalR infrastructure ourselves becomes undesirable.

**RabbitMQ Fan-Out / Redis Streams**

Rejected. ASP.NET Core SignalR ships an official, first-party Redis pub/sub backplane (`Microsoft.AspNetCore.SignalR.StackExchangeRedis`) with no equivalent officially supported RabbitMQ or Redis Streams integration. Building and maintaining a custom backplane on top of either would duplicate what the official library already provides, for no added benefit.

**Decision Summary**

Use the official SignalR Redis backplane to enable horizontal scaling of Chat.Api across Kubernetes pods. Redis provides the communication layer between SignalR server instances; it does not replace RabbitMQ or the database.