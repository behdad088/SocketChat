# Chat Message

**Status:** Accepted
**Date:** 2026-08-12

In this ADR, we will discuss the design and implementation of the chat message feature in our application. The chat
message feature allows users to send and receive messages in real-time, enabling seamless communication between users.
There are several key considerations to take into account when designing this feature, including message storage,
delivery, and user interface and experience. There are multiple ways to design the delivery of the chat messages with
making sure the messages are delivered and persisted in the system. In this ADR, we will explore different approaches to
message delivery and persistence, and evaluate their pros and cons.

## Database First Approach

When a user sends a message, the message is first stored in the database before being delivered to the recipient. This
approach ensures that messages are persisted in the system and can be retrieved later if needed. However, it may
introduce some latency in message delivery, as the message must be written to the database before it can be sent to the
recipient.

### pros:
- Ensures message persistence in the system.
- Allows for message retrieval and history tracking.

### cons:
- Introduces latency in message delivery.
- May require additional database resources to handle high message volumes.
- Scalability concerns as the number of messages increases.

## RMQ First Approach

In this approach, when a user sends a message, the message is first sent to a message queue (e.g., RabbitMQ) before
being stored in the database. Publishing a message to RabbitMQ can be considered the point at which the message enters
the system and a background worker can pick it up and store the message in the database and publish an event to the
recipient.

### pros:
- Allows for message persistence in the system, as the message can be stored in the database later.
- Can handle high message volumes more efficiently, as the message queue can buffer messages and process them.

### cons:
- Requires additional infrastructure to manage the message queue.
- Latency in message delivery may still be present, as the message must be processed by the background worker before
being sent to the recipient.
- Scalability concerns as the number of messages increases, as the message queue may become a bottleneck.


### Real-time Delivery With RMQ

In this approach, when a user sends a message, the message is first sent to a message queue (e.g., RabbitMQ) and then
delivered to the recipient in real-time. Messages will be store in the database for persistence via background service.
This approach allows for real-time message delivery while still ensuring that messages are persisted in the system.

However, this approach will introduce some complexity in the system, as it can cause some inconsistencies in message
delivery and persistence. For example, In a heavy load delivery if the background service fails to store the message or
falls behind in picking up the message to store the database, the message may be delivered to the recipient but not
persisted in the system. If the user closes the app or reload the page, the messages that were delivered will disappear.
This can lead to issues with message history and retrieval, as the message may not be available in the database.

To solve the above issue, we introduce a short-lived, server-side distributed cache (e.g., Redis) that holds messages
that have been delivered but not yet persisted in the server-side database. When a message is sent from user A to user
B, it is delivered to user B in real-time via RabbitMQ/SignalR and, at the same time, written into this distributed
cache. The cache plays the same role a client-side database would have played, except shared by every device belonging
to both participants instead of duplicated per device: if the background service falls behind or fails to persist the
message, any device of either user can recover it from the cache on load or reconnect, not just the device that
happened to be online at the moment of delivery.

The cache is organized as one key per conversation holding the set of pending message ids
(`chat:pending:{conversationId}`, a plain Redis Set), plus one key per message holding its body
(`chat:pending:msg:{messageId}`), with a TTL on the message-body key that comfortably exceeds the expected worst-case
persistence latency. On load or reconnect, a client fetches persisted messages from the server-side database and
pending messages from the cache (the conversation's id set, then the individual message keys) and merges both by
message id (ULID) to reconstruct the complete history. When the background service successfully persists a message, it
removes the id from the conversation's pending set and deletes the message's body key; the per-key TTL is a safety net
if that cleanup step is ever missed; a read that finds a pending id whose body key has already expired simply skips
it, since the message is by then guaranteed to already be in the database.

**Message send → delivery → persistence flow:**

![alt text](<Message send.svg>)

### pros:
- Allows for real-time message delivery while still ensuring message persistence in the system.
- Can handle high message volumes more efficiently, as the message queue can buffer messages and process them.
- Provides a fallback mechanism in case the background service fails to store the message in the server-side database,
consistently across every device of both participants rather than duplicated per device.

### cons:
- Introduces additional complexity in the system, as it requires a distributed cache and background service.
- Shifts the durability risk during the pending window: instead of independent per-device local storage, the cache
becomes the only copy of a not-yet-persisted message across the whole system, so it needs to be more durable than a
purely best-effort cache (e.g. replication or persistence enabled), not just fast.

### Client-Side Message History Cache (IndexedDB)

To avoid re-fetching the full message history from the server on every reload, the client also maintains a local
read-through cache of already-persisted messages in IndexedDB. This is a pure performance optimization, not a
durability mechanism; the server-side database remains the sole source of truth, so a missing, stale, or evicted
IndexedDB cache never causes data loss or inconsistency; it only means falling back to a normal fetch.

On load, the client renders whatever it has cached locally, then asks the server for anything newer than its local
high-water mark (`LastMessageId`, already tracked per conversation); a standard delta/incremental sync. The
reconciled view a client renders is therefore a three-way merge: the local IndexedDB cache (fast, possibly slightly
stale), the delta of persisted messages newer than the local high-water mark, and the pending messages held in the
distributed cache (not yet persisted at all).

Edits and deletes require their own invalidation path, since a delta query by `Id` only surfaces new messages, not
changes to ones already cached; a message's `Id` is assigned once at creation and never changes, so it cannot be used
to detect an edit to an old message. Each message also carries a `RevisionId`: a fresh ULID minted and written to that
message whenever it is edited or deleted, distinct from its permanent `Id`. A raw timestamp was considered for this
instead, but rejected: clock skew and same-millisecond collisions could cause a strict "greater than" comparison to
silently miss an edit. A ULID avoids that, the same way it already does for message ordering. Deletes must be soft
deletes (`IsDeleted`/
`DeletedAt`), since a hard-deleted row leaves nothing for a late-syncing client to discover; a missed live invalidation
event for a hard delete would be unrecoverable, defeating the same pull-based backstop pattern used everywhere else in
this design.

The client does not track or send any per-message state to do this; it keeps exactly two values per conversation: the
highest `Id` it has seen, and the highest `RevisionId` it has seen. On reconnect, it asks the server for every message
in that conversation where `Id` is greater than its stored id checkpoint, or `RevisionId` is greater than its stored
revision checkpoint; one query, one pair of ULIDs, regardless of whether the conversation has ten messages or a
hundred thousand, and regardless of how many of them changed while the client was away. The database's `WHERE` clause
does the filtering; the client never enumerates or decides which specific messages to ask about, it only upserts by id
whatever rows come back. This also means the same check, run once per conversation, is batched into a single request
across every conversation the client knows about when the app starts or the connection re-establishes; not one request
per conversation, and not on every page render; since each conversation's checkpoint pair is just one small row the
client already has locally.

**Reconnect / reconciliation flow (per conversation, batched across all conversations in one request):**


![alt text](<Reconnect flow.svg>)

The local cache is cleared on logout, since it persists message content on disk between sessions.

## Conclusion

The plan is to go with the Real-time Delivery With RMQ, as it allows for real-time message delivery while still
ensuring message persistence in the system. It also provides a fallback mechanism in case the background service fails
to store the message in the server-side database. However, it does introduce additional complexity in the system, as it
requires a distributed cache and background service. The benefits of real-time message delivery and message
persistence outweigh the added complexity, making this approach the best choice for our chat message feature.

## Consequences

- Message ids are minted server-side (ULID), on receipt of the send request over the SignalR hub connection, before
the message is published to RabbitMQ. The client only ever generates a temporary local identifier to correlate its own
optimistic UI entry until the server-assigned id is returned.
- If the SignalR send call fails or times out client-side, the message is marked `failed` with a resend option; resend
reuses the same client-generated correlation id rather than minting a new one. Because the API layer is horizontally
scaled, a resend can land on a different instance than the original attempt, so the same distributed cache maps
`correlationId -> ULID` with a TTL longer than the retry window. On a resend, the hub checks this cache first and
returns the existing ULID instead of minting/publishing again, preventing a lost-ack retry from becoming a second real
message. This is a separate guard from the Postgres insert-guard, which dedupes by ULID at persistence time and would
not catch this case, since a resend without the cache would mint a new ULID each time.
- Recipients can see a message before it is durably persisted in the server-side database; during that gap, durability
is provided by the server-side distributed cache rather than any one device's local storage, so it applies uniformly
regardless of which device is online.
- If the distributed cache loses a pending entry before the background service persists it (e.g. a cache restart
without persistence enabled, or eviction under memory pressure), that message is permanently lost. Unlike a per-device
client-side cache, this risk is not scoped to one device; a cache-level failure can affect every in-flight message in
the system at once, which is why the cache needs some durability guarantee rather than being purely best-effort. This
is an accepted risk, bounded by keeping the pending window short.
- Cleanup on persistence (removing the pending id and deleting its message-body key) is the fast path; the on-load or
reconnect reconciliation against the database and cache is the authoritative correctness backstop, and must tolerate a
pending id whose body key has already expired (treated as already persisted) regardless of whether the explicit
cleanup step ran.
- The pending-message set per conversation is expected to stay small (messages should be persisted within seconds),
so a plain Set is used instead of a sorted set; ordering across pending and persisted messages is resolved at merge
time using the ULID's own sortability, not by Redis itself.
- Because the pending cache is shared server-side rather than per-device, every device belonging to both participants
sees the same reconciled view. The cross-device inconsistency that a per-device client-side cache would introduce (one
device seeing a message before another) does not apply here.
- Message history is additionally cached client-side in IndexedDB purely for reload performance; it is never the only
copy of anything, so losing it (eviction, browser data clear) only costs a slower reload, never data loss.
- Reconciling on load/reconnect is therefore a three-way merge: the local IndexedDB cache, the delta of persisted
messages newer than the client's local `LastMessageId`, and the pending messages in the distributed cache.
- Edits and deletes are tracked via a separate `RevisionId` (a fresh ULID minted on each edit/delete, distinct from
the message's permanent `Id`), not a raw timestamp, avoiding clock-skew and same-millisecond collision hazards. This
lets a client catch up on changes to messages it already has cached, not just on new ones. Deletes are soft deletes,
so this catch-up query can discover a deletion even if the live invalidation event for it was missed.
- The IndexedDB cache is cleared on logout, to avoid leaving message content readable on shared devices after a
session ends.
