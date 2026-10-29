"use strict";

const http = require("node:http");
const crypto = require("node:crypto");
const { URL } = require("node:url");

const DEFAULT_PORT = 8787;
const DEFAULT_LEASE_MS = 60_000;
const MAX_BODY_BYTES = 64 * 1024;
const OWNER_TOKEN_PATTERN = /^[A-Za-z0-9_-]{32,256}$/;
const LISTING_ID_PATTERN = /^[A-Za-z0-9_-]{1,128}$/;

function sendJson(response, statusCode, value) {
  const body = JSON.stringify(value);
  response.writeHead(statusCode, {
    "content-type": "application/json; charset=utf-8",
    "content-length": Buffer.byteLength(body),
    "cache-control": "no-store",
    "access-control-allow-origin": "*",
    "access-control-allow-methods": "GET,POST,DELETE,OPTIONS",
    "access-control-allow-headers": "authorization,content-type",
  });
  response.end(body);
}

function getRemoteAddress(request, trustProxy) {
  const forwarded = trustProxy ? request.headers["x-forwarded-for"] : undefined;
  const source = typeof forwarded === "string" ? forwarded.split(",")[0] : request.socket.remoteAddress;
  return String(source || "127.0.0.1").trim().replace(/^::ffff:/, "");
}

function getOwnerToken(request) {
  const authorization = request.headers.authorization;
  const match = typeof authorization === "string" && authorization.match(/^Bearer (.+)$/i);
  const token = match ? match[1].trim() : "";
  if (!OWNER_TOKEN_PATTERN.test(token)) {
    const error = new Error("A valid listing owner token is required.");
    error.statusCode = 401;
    throw error;
  }
  return token;
}

function hashOwnerToken(token) {
  return crypto.createHash("sha256").update(token, "utf8").digest();
}

function ownsListing(record, token) {
  const candidate = hashOwnerToken(token);
  return candidate.length === record.ownerTokenHash.length
    && crypto.timingSafeEqual(candidate, record.ownerTokenHash);
}

function normalizeInteger(value, fallback, minimum, maximum, label) {
  if (value === undefined || value === null || value === "") {
    return fallback;
  }
  const parsed = Number(value);
  if (!Number.isInteger(parsed) || parsed < minimum || parsed > maximum) {
    throw new Error(`${label} must be an integer from ${minimum} through ${maximum}.`);
  }
  return parsed;
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

function normalizeRegistration(input, request, now, leaseMs, trustProxy) {
  const requestedID = typeof input.id === "string" ? input.id.trim() : "";
  const id = requestedID || crypto.randomUUID();
  if (!LISTING_ID_PATTERN.test(id)) {
    throw new Error("The listing ID is invalid.");
  }
  const transport = String(input.transport || "direct-tcp").trim().toLowerCase().slice(0, 32);
  if (!/^[a-z0-9._-]+$/.test(transport)) {
    throw new Error("The transport identifier is invalid.");
  }
  const port = normalizeInteger(input.port, 0, 0, 65535, "Port");
  if (transport === "direct-tcp" && port === 0) {
    throw new Error("A valid TCP port is required for direct TCP listings.");
  }
  const requestedAddress = typeof input.address === "string" ? input.address.trim() : "";
  const remoteAddress = getRemoteAddress(request, trustProxy);
  const isLocalRequest = remoteAddress === "127.0.0.1" || remoteAddress === "::1";
  const maxPlayers = normalizeInteger(input.maxPlayers, 2, 1, 32, "Maximum players");
  const currentPlayers = normalizeInteger(
    input.currentPlayers,
    1,
    0,
    maxPlayers,
    "Current players",
  );
  const requestedName = String(input.name || "").trim();
  return {
    id,
    name: (requestedName || "Player's Game").slice(0, 64),
    transport,
    joinCode: String(input.joinCode || "").trim().slice(0, 512),
    address: transport === "direct-tcp"
      ? (isLocalRequest && requestedAddress ? requestedAddress : remoteAddress)
      : "",
    port,
    currentPlayers,
    maxPlayers,
    state: String(input.state || "waiting").trim().slice(0, 32),
    hostFaction: String(input.hostFaction || "").trim().slice(0, 128),
    contentFingerprint: String(input.contentFingerprint || "").trim().slice(0, 1024),
    expiresAt: now + leaseMs,
  };
}

function createMatchmakerServer(options = {}) {
  const leaseMs = options.leaseMs || DEFAULT_LEASE_MS;
  const now = options.now || Date.now;
  const trustProxy = options.trustProxy === true;
  const games = new Map();

  function removeExpired() {
    const current = now();
    for (const [id, record] of games) {
      if (record.game.expiresAt <= current) {
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
        sendJson(response, 200, { games: Array.from(games.values(), (record) => record.game) });
        return;
      }
      if (request.method === "POST" && url.pathname === "/games") {
        const ownerToken = getOwnerToken(request);
        const input = await readJson(request);
        const requestedID = typeof input.id === "string" ? input.id.trim() : "";
        const existing = requestedID ? games.get(requestedID) : null;
        if (existing && !ownsListing(existing, ownerToken)) {
          const error = new Error("The listing owner token does not match.");
          error.statusCode = 403;
          throw error;
        }
        const listing = normalizeRegistration(input, request, now(), leaseMs, trustProxy);
        games.set(listing.id, {
          game: listing,
          ownerTokenHash: existing?.ownerTokenHash || hashOwnerToken(ownerToken),
        });
        sendJson(response, existing ? 200 : 201, listing);
        return;
      }
      if (request.method === "DELETE" && url.pathname.startsWith("/games/")) {
        const id = decodeURIComponent(url.pathname.slice("/games/".length));
        const record = games.get(id);
        if (!record) {
          sendJson(response, 404, { removed: false });
          return;
        }
        const ownerToken = getOwnerToken(request);
        if (!ownsListing(record, ownerToken)) {
          const error = new Error("The listing owner token does not match.");
          error.statusCode = 403;
          throw error;
        }
        games.delete(id);
        sendJson(response, 200, { removed: true });
        return;
      }
      sendJson(response, 404, { error: "Not found." });
    } catch (error) {
      sendJson(response, Number(error.statusCode) || 400, { error: error.message });
    }
  });

  return server;
}

if (require.main === module) {
  const port = Number(process.env.PORT || DEFAULT_PORT);
  const host = process.env.HOST || "0.0.0.0";
  const trustProxy = process.env.TRUST_PROXY === "1";
  createMatchmakerServer({ trustProxy }).listen(port, host, () => {
    process.stdout.write(`Matchmaker listening on http://${host}:${port}\n`);
  });
}

module.exports = { createMatchmakerServer };
