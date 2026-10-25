"use strict";

const assert = require("node:assert/strict");
const test = require("node:test");
const { createMatchmakerServer } = require("./server");

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
      hostFaction: "rebellion",
      contentFingerprint: "base|1|default|",
    };
    let response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(registration),
    });
    assert.equal(response.status, 200);
    const created = await response.json();
    assert.ok(created.id);
    assert.equal(created.address, registration.address);

    response = await fetch(`${baseUrl}/games`);
    assert.deepEqual((await response.json()).games, [created]);

    registration.id = created.id;
    registration.name = "Updated Game";
    response = await fetch(`${baseUrl}/games`, {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify(registration),
    });
    assert.equal((await response.json()).name, "Updated Game");

    response = await fetch(`${baseUrl}/games/${created.id}`, { method: "DELETE" });
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
      headers: { "content-type": "application/json" },
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
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ name: "No Port" }),
    });
    assert.equal(response.status, 400);
    assert.match((await response.json()).error, /port/i);
  });
});
