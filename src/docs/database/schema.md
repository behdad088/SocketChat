# Profile Document
Identity service stores user information in a PostgreSQL database. The schema is designed to support efficient retrieval and management of user data, including authentication and authorization details.
The Chat Service will have its own Profile document table to avoid cross-service dependencies, guarantee fast queries, and ensure that user data is available even if the Identity service is down. The Profile document will contain essential user information, such as user ID, username, and email.

Any changes to the user data in the Identity service will be published to the Chat Service via an event bus, ensuring that the Profile document remains up-to-date. The Chat Service will subscribe to these events and update its Profile document accordingly.

NOTE: Event handling is idempotent and order-tolerant: an incoming event whose `Version` is less than or equal to the stored document's `Version` is ignored. This protects against duplicate and out-of-order delivery from the event bus.

## Tables

| Table                              | data             | Description |
|------------------------------------|------------------|-------------|
| `mt_doc_profiledocument`           | `Profile`        | Stores essential user information for the Chat Service, including user ID, username, and email. This table is updated via events from the Identity service to ensure data consistency and availability. |
| `mt_doc_versionprofiledocument`    | `VersionProfile` | Stores a snapshot of the profile document for every change. This table keeps track of all the changes that happen to the profile document. |
| `mt_doc_userchanneldocument`       | `UserChannel`    | One row per user per conversation. Holds the per-user view of a conversation (state, pin, read position). |
| `mt_doc_userchannelversiondocument`| `UserChannelVersion` | Stores a snapshot of the user channel document for every change. |
| `mt_doc_conversationdocument`      | `Conversation`   | Stores conversation-level information shared by all participants. |
| `mt_doc_messagedocument`           | `Message`        | Stores the messages sent in a conversation. |
| `mt_doc_messageversiondocument`    | `MessageVersion` | Stores a snapshot of the message document for every content edit. |

## schema
`mt_doc_profiledocument`

| Properties     | Type     | IsNullable | description                                                                            |
|----------------|----------|------------|----------------------------------------------------------------------------------------|
| Id             | string   | no         | string in guid format                                                                  |
| Username       | string   | no         | username of the user                                                                   |
| Firstname      | string   | no         | user firstname                                                                         |
| Lastname       | string   | no         | user lastname                                                                          |
| DisplayName    | string   | yes        | If the display name is null, the app can pick firstname and lastname as display name   |
| Email          | string   | no         | user email address                                                                     |
| PhoneNumber    | string   | no         | user phone number                                                                      |
| ProfilePicture | string   | yes        | user profile picture                                                                   |
| IsActive       | boolean  | yes        | whether the user account is active                                                     |
| LastOnline     | datetime | yes        | UTC timestamp                                                                          |
| Quote          | string   | yes        | user general message in the profile                                                    |
| Version        | int      | no         | version number, incremented on every change                                            |

NOTE: The `Version` field is used to track changes to the profile document. Each time a change is made, the version number is incremented, and a new entry is created in the `mt_doc_versionprofiledocument` table to record the change.

NOTE: The online status will be stored in the cache and will be written to the profile document (`LastOnline`) when the user goes offline.

**Access patterns**
*  **Id**: Retrieve the profile document by user ID.


`mt_doc_versionprofiledocument`

| Field          | Type     | IsNullable | description                                                                            |
|----------------|----------|------------|----------------------------------------------------------------------------------------|
| Id             | string   | no         | {guid}:{version_id}, example:  `d9360022-d706-4670-bf05-6c7e0a043732:1`                |
| Username       | string   | no         | username of the user                                                                   |
| Firstname      | string   | no         | user firstname                                                                         |
| Lastname       | string   | no         | user lastname                                                                          |
| DisplayName    | string   | yes        | If the display name is null, the app can pick firstname and lastname as display name   |
| Email          | string   | no         | user email address                                                                     |
| PhoneNumber    | string   | no         | user phone number                                                                      |
| ProfilePicture | string   | yes        | user profile picture                                                                   |
| IsActive       | boolean  | yes        | whether the user account is active                                                     |
| LastOnline     | datetime | yes        | UTC timestamp                                                                          |
| Quote          | string   | yes        | user general message in the profile                                                    |
| Version        | int      | no         | version number this snapshot corresponds to                                            |

# User Channel Document
This table marks the channel between two users; it will be used to retrieve the conversation between two users. The table will be updated when a new conversation is created between two users.

When a user starts a new conversation with another user, we first check for an existing conversation. If there is none, **two** entries are created in this table — one per participant — sharing a single unique conversation ID. Both rows must be created in the same transaction as the Conversation document.

Because the Id `{user_id}:{peer_user_id}` is deterministic, concurrent creation (both users initiating a conversation at the same time) is handled by upserting on the Id: whichever transaction commits second detects the existing row and reuses its ConversationId instead of creating a duplicate.

Each user owns their own row, so per-user preferences (state, pin, read position) apply only to that user and never leak to the other participant.

## schema
`mt_doc_userchanneldocument`

| Field             | Type     | IsNullable | description                                                                                                              |
|-------------------|----------|------------|----------------------------------------------------------------------------------------------------------------------------|
| Id                | string   | no         | {user_id}:{peer_user_id}, example:  `d9360022-d706-4670-bf05-6c7e0a043732:3d7babc6-b1ed-485c-b598-82d607ee7b4d`          |
| ConversationId    | string   | no         | conversation id, example:  `01KX6WMD905AN68KKFWQVDNCHZ`                                                                  |
| UserId            | string   | no         | id of the user who owns this row, example:  `d9360022-d706-4670-bf05-6c7e0a043732`                                       |
| State             | string   | yes        | active, muted or archived                                                                                                |
| IsPinned          | boolean  | no         | true if the conversation is pinned                                                                                       |
| LastReadMessageId | string   | yes        | ULID of the last message this user has read. Unread messages are those with Id > LastReadMessageId. Null = nothing read. |
| LastMessageAt     | datetime | yes        | UTC timestamp of the latest message in the conversation, denormalized here so the chat list can be sorted by recency.    |
| Version           | int      | no         | version number, incremented on every change                                                                              |

NOTE: Read state is tracked with `LastReadMessageId` rather than a per-message status. Marking a conversation as read is a single write to this row, and because message IDs are ULIDs (lexicographically sortable by time), the unread count is simply the number of messages with `Id > LastReadMessageId` sent by the other participant.

NOTE: `LastMessageAt` is duplicated from the Conversation document onto both participants' channel rows whenever a message is sent, so that "list my conversations, newest first" is a single indexed query on this table without joining to `mt_doc_conversationdocument`.

**Access patterns**
*  **Id**: {user_id}:{peer_user_id}. to get the conversation between two users.
*  **UserId**: to get all the conversations for a user, sorted by `LastMessageAt` descending.

**indexes**
*  **Id**: primary key
*  **(UserId, LastMessageAt)**: composite secondary index to list a user's conversations ordered by recency.

`mt_doc_userchannelversiondocument`

| Field             | Type     | IsNullable | description                                                                                                                               |
|-------------------|----------|------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| Id                | string   | no         | {user_id}:{peer_user_id}:{version_id}, example:  `d9360022-d706-4670-bf05-6c7e0a043732:3d7babc6-b1ed-485c-b598-82d607ee7b4d:1`            |
| ConversationId    | string   | no         | conversation id, example:  `01KX6WMD905AN68KKFWQVDNCHZ`                                                                                   |
| UserId            | string   | no         | id of the user who owns this row, example:  `d9360022-d706-4670-bf05-6c7e0a043732`                                                        |
| State             | string   | yes        | active, muted or archived                                                                                                                 |
| IsPinned          | boolean  | no         | true if the conversation is pinned                                                                                                        |
| Version           | int      | no         | version number this snapshot corresponds to                                                                                               |

NOTE: Changes to `LastReadMessageId` and `LastMessageAt` do NOT increment `Version` or create a version snapshot — they change on nearly every message and would flood this table. Only user preference changes (State, IsPinned) are versioned.

# Conversation Document
This table stores the conversation channel information shared by all participants, like the participant list. The table will be updated when a new message is sent in the conversation.

## schema
`mt_doc_conversationdocument`

| Field         | Type     | IsNullable | description                                              |
|---------------|----------|------------|----------------------------------------------------------|
| Id            | string   | no         | conversation id, example:  `01KX6WMD905AN68KKFWQVDNCHZ`  |
| Participants  | array    | no         | Array of user ids in the conversation                    |
| CreatedAt     | datetime | no         | UTC timestamp of when the conversation was created       |
| LastMessageAt | datetime | yes        | UTC timestamp of the latest message in the conversation  |

NOTE: The chat-list query reads last-activity time from the user channel documents, not from here; this copy is the conversation-level source of truth, kept for future-proofing (e.g. group conversations, where updating one conversation document beats fanning out to N channel rows per message). It also allows the denormalized `LastMessageAt` on channel rows to be repaired/backfilled if they drift.

NOTE: `LastMessageAt` is written on every message send, which makes this document a write hot spot for busy conversations. We use a patch/partial update (not full-document optimistic concurrency) for this field, or be prepared to retry on version conflicts.

**Access patterns**
*  **Id**: {conversation id}. to get the conversation between two users.

# Message Document
This table stores the messages sent in a conversation. The table will be updated when a new message is sent in the conversation. The message id will be a ULID and will be unique across all conversations. The message id will be used to retrieve the message, and the ULID ordering will be used to sort the messages in the conversation. The message id will be generated by the chat service.

## schema
`mt_doc_messagedocument`

| Field          | Type     | IsNullable | description                                                        |
|----------------|----------|------------|--------------------------------------------------------------------|
| Id             | string   | no         | {message_id}, example: `01KX6Y4MJQ9512TXYZB2CTHGP5`                |
| ConversationId | string   | no         | conversation id, example:  `01KX6WMD905AN68KKFWQVDNCHZ`            |
| SenderId       | string   | no         | user id of the sender                                              |
| Content        | string   | no         | the content of the message                                         |
| RepliedTo      | string   | yes        | the message id this message responds to. Can be null               |
| Reactions      | array    | yes        | array of reaction objects: `{ userId, emoji, createdAt }`          |
| CreatedAt      | datetime | no         | UTC timestamp                                                      |
| UpdatedAt      | datetime | yes        | UTC timestamp of the last content edit                             |
| IsEdited       | boolean  | no         | true if the content has been edited                                |
| IsDeleted      | boolean  | no         | true if the message has been deleted (soft delete)                 |
| DeletedAt      | datetime | yes        | UTC timestamp of deletion. Null if not deleted                     |
| Version        | int      | no         | version number, incremented on every content edit                  |

NOTE: Messages are soft-deleted: `IsDeleted` is set to true and `Content` is cleared, so clients can render a "message deleted" placeholder and replies to the message stay resolvable.

NOTE: Read/delivery state is not stored per message — it is derived from `LastReadMessageId` on the user channel document (see User Channel Document). This avoids rewriting every message document when a user opens a conversation.

NOTE: Adding or removing a reaction does NOT increment `Version` or create a version snapshot — only content edits and deletion are versioned.

**Access patterns**
*  **Id**: {message id}. to get a single message.
*  **ConversationId + Id cursor**: to page through the messages in a conversation, newest first (keyset pagination: `ConversationId = ? AND Id < {cursor} ORDER BY Id DESC LIMIT n`).

**indexes**
*  **Id**: primary key
*  **(ConversationId, Id)**: composite secondary index for keyset pagination of a conversation's messages.

`mt_doc_messageversiondocument`

| Field          | Type     | IsNullable | description                                                        |
|----------------|----------|------------|--------------------------------------------------------------------|
| Id             | string   | no         | {message_id}:{version_id}, example: `01KX6Y4MJQ9512TXYZB2CTHGP5:1` |
| ConversationId | string   | no         | conversation id, example:  `01KX6WMD905AN68KKFWQVDNCHZ`            |
| SenderId       | string   | no         | user id of the sender                                              |
| Content        | string   | no         | the content of the message at this version                        |
| RepliedTo      | string   | yes        | the message id this message responds to. Can be null               |
| CreatedAt      | datetime | no         | UTC timestamp                                                      |
| UpdatedAt      | datetime | yes        | UTC timestamp of the last content edit                             |
| IsEdited       | boolean  | no         | true if the content has been edited                                |
| Version        | int      | no         | version number this snapshot corresponds to                        |
