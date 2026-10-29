# Multiplayer directory

This service maintains short-lived, player-hosted session listings. It is transport-neutral: a
listing identifies its gameplay transport and connection data, while gameplay traffic stays out of
the directory service.

Listing creation, refresh, and removal require a per-session bearer token. Public browser results
never expose that token. Listings expire automatically when a host stops refreshing its lease.

## Run locally

```powershell
npm test
npm start
```

The server listens on `0.0.0.0:8787` by default. Set `HOST` or `PORT` to override either value.
Point an HTTP directory integration client at the server's URL.

The main-menu multiplayer flow uses Epic Online Services lobbies for discovery and reliable ordered
EOS P2P for gameplay, with NAT traversal and relay fallback. It does not require this HTTP service
or an exposed TCP port. The direct TCP peer and this directory remain available for integration
tooling.

The Unity project pins EOS Plugin 6.2.0. Configure its product and platform settings under
`Assets/StreamingAssets/EOS/` using the product, sandbox, deployment, and game-client credentials
from the Epic Developer Portal. This directory is excluded from Git. Players authenticate through
EOS Connect device identities; the game browser does not require an Epic account login.

Hosts publish two-player lobbies after their metadata and connection handlers are ready. The
browser compares hashed content identities, and the gameplay handshake still validates the full
content identity and protocol. Started games reject new joins. Host teardown destroys the lobby;
guest teardown leaves it. Cancelling a pending host or join cleans up membership when its SDK
callback completes. Lobby membership events admit the remote P2P participant; gameplay packets
retain their existing framing, revision checks, and compressed snapshots.
Active hosts and guests keep networking running when the window loses focus. Disconnecting
restores the application's previous background setting.

## HTTP endpoints

- `GET /health`
- `GET /games`
- `POST /games`
- `DELETE /games/:id`

Listings use a 60-second lease. Active hosts refresh them automatically.

`POST` and `DELETE` require `Authorization: Bearer <session-owner-token>`. Set `TRUST_PROXY=1` only
when the service is behind a trusted reverse proxy that overwrites `X-Forwarded-For`.
