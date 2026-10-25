# Multiplayer directory

This service lists player-hosted games. Gameplay travels directly between the two game clients;
the directory does not relay game traffic.

## Run locally

```powershell
npm test
npm start
```

The server listens on `0.0.0.0:8787` by default. Set `HOST` or `PORT` to override either value.
Point the multiplayer menu at the server's HTTP URL.

Hosts must allow the selected game TCP port through their firewall. Internet hosts also need to
forward that TCP port through their router or provide another route that lets the guest reach the
host directly.

## HTTP endpoints

- `GET /health`
- `GET /games`
- `POST /games`
- `DELETE /games/:id`

Listings use a 60-second lease. Active hosts refresh them automatically.
