"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const { createMatchmakerServer } = require("./server");

const OWNER_TOKEN = "0123456789abcdef0123456789abcdef";

function ownerHeaders(ownerToken = OWNER_TOKEN) {
  return {
    authorization: `Bearer ${ownerToken}`,
    "content-type": "application/json",
  };
}

async function withServer(options, callback) {
  const server = createMatchmakerServer(options);
  await new Promise((resolve) => server.listen(0, "127.0.0.1", resolve));
  const { port } = server.address();
  try {
    await callback(`http://127.0.0.1:${port}`);
  } finally {
    await new Promise((resolve, reject) => server.close((error) => (error ? reject(error) : resolve())));
  }
}

test("registers, lists, refreshes, and removes a game", async () => {
  await withServer({}, async (baseUrl) => {
    const registration = {
      name: "Leia's Game",
      address: "192.168.1.20",
      port: 27888,
      currentPlayers: 1,
      maxPlayers: 2,
      state: "waiting",
      hostFaction: "rebellion",
      contentFingerprint: "base|1|default|",
    };
    let response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify(registration),
    });
    assert.equal(response.status, 201);
    const created = await response.json();
    assert.ok(created.id);
    assert.equal(created.address, registration.address);
    assert.equal(created.transport, "direct-tcp");
    assert.equal(created.currentPlayers, 1);
    assert.equal(created.maxPlayers, 2);
    assert.equal(created.state, "waiting");
    assert.equal(JSON.stringify(created).includes(OWNER_TOKEN), false);

    response = await fetch(`${baseUrl}/games`);
    assert.deepEqual((await response.json()).games, [created]);

    registration.id = created.id;
    registration.name = "Updated Game";
    response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify(registration),
    });
    assert.equal(response.status, 200);
    assert.equal((await response.json()).name, "Updated Game");

    response = await fetch(`${baseUrl}/games/${created.id}`, {
      method: "DELETE",
      headers: ownerHeaders(),
    });
    assert.equal(response.status, 200);
    response = await fetch(`${baseUrl}/games`);
    assert.deepEqual((await response.json()).games, []);
  });
});

test("expires abandoned games", async () => {
  let clock = 1_000;
  await withServer({ leaseMs: 50, now: () => clock }, async (baseUrl) => {
    await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify({ name: "Short", port: 1234 }),
    });
    clock += 51;
    const response = await fetch(`${baseUrl}/games`);
    assert.deepEqual((await response.json()).games, []);
  });
});

test("rejects malformed registrations", async () => {
  await withServer({}, async (baseUrl) => {
    const response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify({ name: "No Port" }),
    });
    assert.equal(response.status, 400);
    assert.match((await response.json()).error, /port/i);
  });
});

test("rejects listing mutation without the owner token", async () => {
  await withServer({}, async (baseUrl) => {
    let response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify({ name: "Protected", port: 27888 }),
    });
    const created = await response.json();

    response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders("fedcba9876543210fedcba9876543210"),
      body: JSON.stringify({ ...created, name: "Hijacked" }),
    });
    assert.equal(response.status, 403);

    response = await fetch(`${baseUrl}/games/${created.id}`, {
      method: "DELETE",
      headers: ownerHeaders("fedcba9876543210fedcba9876543210"),
    });
    assert.equal(response.status, 403);

    response = await fetch(`${baseUrl}/games`);
    assert.equal((await response.json()).games[0].name, "Protected");
  });
});

test("validates player counts as bounded integers", async () => {
  await withServer({}, async (baseUrl) => {
    const response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: ownerHeaders(),
      body: JSON.stringify({
        name: "Fractions",
        port: 27888,
        currentPlayers: 1.5,
        maxPlayers: 2,
      }),
    });
    assert.equal(response.status, 400);
    assert.match((await response.json()).error, /integer/i);
  });
});
