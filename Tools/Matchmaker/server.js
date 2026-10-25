"use strict";

const http = require("node:http");
const crypto = require("node:crypto");
const { URL } = require("node:url");

const DEFAULT_PORT = 8787;
const DEFAULT_LEASE_MS = 60_000;
const MAX_BODY_BYTES = 64 * 1024;

function sendJson(response, statusCode, value) {
  const body = JSON.stringify(value);
  response.writeHead(statusCode, {
    "content-type": "application/json; charset=utf-8",
    "content-length": Buffer.byteLength(body),
    "access-control-allow-origin": "*",
    "access-control-allow-methods": "GET,POST,DELETE,OPTIONS",
    "access-control-allow-headers": "content-type",
  });
  response.end(body);
}

function getRemoteAddress(request) {
  const forwarded = request.headers["x-forwarded-for"];
  const source = typeof forwarded === "string" ? forwarded.split(",")[0] : request.socket.remoteAddress;
  return String(source || "127.0.0.1").trim().replace(/^::ffff:/, "");
}

async function readJson(request) {
  const chunks = [];
  let length = 0;
  for await (const chunk of request) {
    length += chunk.length;
    if (length > MAX_BODY_BYTES) {
      throw new Error("Request body is too large.");
    }
    chunks.push(chunk);
  }
  if (chunks.length === 0) {
    return {};
  }
  return JSON.parse(Buffer.concat(chunks).toString("utf8"));
}

function normalizeRegistration(input, request, now, leaseMs) {
  const port = Number(input.port);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error("A valid TCP port is required.");
  }
  const id = typeof input.id === "string" && input.id.trim() ? input.id.trim() : crypto.randomUUID();
  const requestedAddress = typeof input.address === "string" ? input.address.trim() : "";
  const remoteAddress = getRemoteAddress(request);
  const isLocalRequest = remoteAddress === "127.0.0.1" || remoteAddress === "::1";
  return {
    id,
    name: String(input.name || "Player's Game").trim().slice(0, 64),
    address: isLocalRequest && requestedAddress ? requestedAddress : remoteAddress,
    port,
    hostFaction: String(input.hostFaction || "").trim().slice(0, 128),
    contentFingerprint: String(input.contentFingerprint || "").trim().slice(0, 1024),
    expiresAt: now + leaseMs,
  };
}

function createMatchmakerServer(options = {}) {
  const leaseMs = options.leaseMs || DEFAULT_LEASE_MS;
  const now = options.now || Date.now;
  const games = new Map();

  function removeExpired() {
    const current = now();
    for (const [id, game] of games) {
      if (game.expiresAt <= current) {
        games.delete(id);
      }
    }
  }

  const server = http.createServer(async (request, response) => {
    try {
      if (request.method === "OPTIONS") {
        sendJson(response, 204, {});
        return;
      }

      const url = new URL(request.url, "http://matchmaker.local");
      removeExpired();
      if (request.method === "GET" && url.pathname === "/health") {
        sendJson(response, 200, { ok: true, games: games.size });
        return;
      }
      if (request.method === "GET" && url.pathname === "/games") {
        sendJson(response, 200, { games: Array.from(games.values()) });
        return;
      }
      if (request.method === "POST" && url.pathname === "/games") {
        const listing = normalizeRegistration(await readJson(request), request, now(), leaseMs);
        games.set(listing.id, listing);
        sendJson(response, 200, listing);
        return;
      }
      if (request.method === "DELETE" && url.pathname.startsWith("/games/")) {
        const id = decodeURIComponent(url.pathname.slice("/games/".length));
        const removed = games.delete(id);
        sendJson(response, removed ? 200 : 404, { removed });
        return;
      }
      sendJson(response, 404, { error: "Not found." });
    } catch (error) {
      sendJson(response, 400, { error: error.message });
    }
  });

  return server;
}

if (require.main === module) {
  const port = Number(process.env.PORT || DEFAULT_PORT);
  const host = process.env.HOST || "0.0.0.0";
  createMatchmakerServer().listen(port, host, () => {
    process.stdout.write(`Matchmaker listening on http://${host}:${port}\n`);
  });
}

module.exports = { createMatchmakerServer };
