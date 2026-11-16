const assert = require('node:assert/strict');
const { test } = require('node:test');
const { parseRequest, plan, approve, report } = require('./pr-tests.cjs');

const HEAD = '1'.repeat(40);
const BASE = '2'.repeat(40);

function fixture() {
  const calls = [];
  const outputs = {};
  const pull = {
    state: 'open',
    base: { sha: BASE, repo: { full_name: 'owner/game' } },
    head: { sha: HEAD, ref: 'contributor-feature', repo: { full_name: 'contributor/game' } },
  };
  const environment = { protection_rules: [{ type: 'required_reviewers', reviewers: [{ type: 'User', reviewer: { id: 1 } }] }] };
  const github = { rest: {
    repos: { getEnvironment: async () => ({ data: environment }) },
    pulls: { get: async () => ({ data: pull }) },
    checks: {
      create: async data => { calls.push(data); return { data: { id: 23 } }; },
      update: async data => { calls.push(data); },
    },
  } };
  return { github, context: { repo: { owner: 'owner', repo: 'game' }, serverUrl: 'https://github.com', runId: 45 },
    core: { setOutput: (key, value) => { outputs[key] = value; } },
    number: '7', headSha: HEAD, checkId: '23', calls, outputs, pull, environment };
}

test('dispatch inputs reject malformed numbers and mutable refs', () => {
  for (const number of ['0', '-1', '1;echo hello', '1.1', '9007199254740992', '']) {
    assert.throws(() => parseRequest(number, HEAD));
  }
  for (const sha of ['main', 'refs/pull/7/head', `${HEAD}\n`, '1234567', undefined]) {
    assert.throws(() => parseRequest('7', sha));
  }
  assert.deepEqual(parseRequest('7', HEAD), { number: 7, headSha: HEAD });
});

test('planning binds a pending check and review link to the exact PR head', async () => {
  const f = fixture();
  await plan(f);
  assert.equal(f.calls[0].head_sha, HEAD);
  assert.equal(f.calls[0].status, 'in_progress');
  assert.equal(f.calls[0].details_url, 'https://github.com/owner/game/actions/runs/45');
  assert.equal(f.outputs.check_id, '23');
  assert.equal(f.outputs.review_url, `https://github.com/owner/game/pull/7/commits/${HEAD}`);
  assert.equal(f.outputs.base_sha, undefined);
});

test('an absent required-reviewer rule blocks planning without creating a check', async () => {
  const f = fixture();
  f.environment.protection_rules = [];
  await assert.rejects(plan(f), /required reviewers/);
  assert.deepEqual(f.calls, []);
});

test('an empty reviewer list cannot unlock testing', async () => {
  const f = fixture();
  f.environment.protection_rules[0].reviewers = [];
  await assert.rejects(approve(f), /required reviewers/);
  assert.equal(f.outputs.base_sha, undefined);
});

for (const [name, change, message] of [
  ['a changed head', f => { f.pull.head.sha = '4'.repeat(40); }, /changed/],
  ['a closed PR', f => { f.pull.state = 'closed'; }, /no longer open/],
  ['a different base repository', f => { f.pull.base.repo.full_name = 'attacker/game'; }, /this repository/],
]) {
  test(`${name} blocks planning and approval`, async () => {
    const f = fixture();
    change(f);
    await assert.rejects(plan(f), message);
    await assert.rejects(approve(f), message);
    assert.equal(f.outputs.base_sha, undefined);
  });
}

test('approval binds testing to the exact base and reviewed head repository', async () => {
  const f = fixture();
  await approve(f);
  assert.equal(f.outputs.base_sha, BASE);
  assert.equal(f.outputs.head_repository, 'contributor/game');
  assert.equal(f.outputs.media_branch, 'contributor-feature');
  assert.equal(f.calls[0].check_run_id, 23);
  assert.equal(f.calls[0].status, 'in_progress');
  assert.equal(f.calls[0].output.title, 'Unity tests running');
});

test('removing the review environment after planning fails closed', async () => {
  const f = fixture();
  await plan(f);
  f.github.rest.repos.getEnvironment = async () => { throw new Error('Not Found'); };
  await assert.rejects(approve(f), /Not Found/);
  assert.equal(f.outputs.base_sha, undefined);
});

test('approval rejects an invalid check before exposing revisions to downstream jobs', async () => {
  const f = fixture();
  f.checkId = 'invalid';
  await assert.rejects(approve(f), /check ID/);
  assert.deepEqual(f.outputs, {});
});

for (const [testResult, mediaResult, approvalResult, conclusion] of [
  ['success', 'success', 'success', 'success'],
  ['skipped', 'failure', 'success', 'failure'],
  ['skipped', 'skipped', 'failure', 'failure'],
  ['failure', 'success', 'success', 'failure'],
  ['cancelled', 'success', 'success', 'cancelled'],
]) {
  test(`reporting ${testResult}/${mediaResult}/${approvalResult} yields ${conclusion}`, async () => {
    const f = fixture();
    await report({ ...f, checkId: '23', testResult, mediaResult, approvalResult });
    assert.equal(f.calls[0].check_run_id, 23);
    assert.equal(f.calls[0].conclusion, conclusion);
    assert.equal(f.calls[0].status, 'completed');
  });
}
