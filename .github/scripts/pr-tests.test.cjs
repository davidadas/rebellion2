const assert = require('node:assert/strict');
const { test } = require('node:test');
const { parseRequest, plan, approve, report } = require('./pr-tests.cjs');

const HEAD = '1'.repeat(40);
const BASE = '2'.repeat(40);
const MERGE = '3'.repeat(40);

function fixture() {
  const calls = [];
  const outputs = {};
  const pull = {
    state: 'open', mergeable: true, merge_commit_sha: MERGE,
    base: { ref: 'master', sha: BASE, repo: { full_name: 'owner/game' } },
    head: { sha: HEAD, ref: 'contributor-feature' },
  };
  const environment = { protection_rules: [{ type: 'required_reviewers', reviewers: [{ type: 'User', reviewer: { id: 1 } }] }] };
  const merge = { parents: [{ sha: BASE }, { sha: HEAD }] };
  const refs = {
    'heads/master': { object: { sha: BASE } },
    'pull/7/merge': { object: { sha: MERGE } },
  };
  const github = { rest: {
    repos: { getEnvironment: async () => ({ data: environment }) },
    pulls: { get: async () => ({ data: pull }) },
    git: {
      getRef: async ({ ref }) => {
        if (!refs[ref]) throw Object.assign(new Error('Not Found'), { status: 404 });
        return { data: structuredClone(refs[ref]) };
      },
      getCommit: async ({ commit_sha }) => {
        assert.equal(commit_sha, refs['pull/7/merge'].object.sha);
        return { data: merge };
      },
    },
    checks: {
      create: async data => { calls.push(data); return { data: { id: 23 } }; },
      update: async data => { calls.push(data); },
    },
  } };
  return { github, context: { repo: { owner: 'owner', repo: 'game' }, serverUrl: 'https://github.com', runId: 45 },
    core: { setOutput: (key, value) => { outputs[key] = value; } },
    number: '7', headSha: HEAD, sleep: async () => {}, calls, outputs, pull, environment, merge, refs };
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
  assert.equal(f.outputs.merge_sha, undefined);
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
  assert.equal(f.outputs.merge_sha, undefined);
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
    assert.equal(f.outputs.merge_sha, undefined);
  });
}

test('approval resolves an immutable merge containing the reviewed head', async () => {
  const f = fixture();
  await approve(f);
  assert.equal(f.outputs.merge_sha, MERGE);
  assert.equal(f.outputs.media_branch, 'contributor-feature');
});

test('approval uses live refs when PR base and merge metadata are stale', async () => {
  const f = fixture();
  f.pull.base.sha = '4'.repeat(40);
  f.pull.merge_commit_sha = '5'.repeat(40);
  await approve(f);
  assert.equal(f.outputs.merge_sha, MERGE);
});

test('a base branch moving during verification cannot approve the older merge', async () => {
  const f = fixture();
  f.github.rest.git.getCommit = async () => {
    f.refs['heads/master'].object.sha = '4'.repeat(40);
    return { data: f.merge };
  };
  await assert.rejects(approve(f), /current merge/);
  assert.equal(f.outputs.merge_sha, undefined);
});

test('approval resumes when a merge ref catches up with a newer base', async () => {
  const f = fixture();
  f.refs['heads/master'].object.sha = '4'.repeat(40);
  f.sleep = async () => { f.merge.parents[0].sha = '4'.repeat(40); };
  await approve(f);
  assert.equal(f.outputs.merge_sha, MERGE);
});

test('an API failure cannot unlock private test access', async () => {
  const f = fixture();
  f.github.rest.git.getRef = async () => { throw Object.assign(new Error('Unavailable'), { status: 500 }); };
  await assert.rejects(approve(f), /Unavailable/);
  assert.equal(f.outputs.merge_sha, undefined);
});

test('approval rejects a stale merge with a different head', async () => {
  const f = fixture();
  f.merge.parents[1].sha = '4'.repeat(40);
  await assert.rejects(approve(f), /current merge/);
  assert.equal(f.outputs.merge_sha, undefined);
});

test('approval rejects a stale merge based on another base revision', async () => {
  const f = fixture();
  f.merge.parents[0].sha = '4'.repeat(40);
  await assert.rejects(approve(f), /current merge/);
});

test('a transient merge calculation retries metadata without changing the approved head', async () => {
  const f = fixture();
  delete f.refs['pull/7/merge'];
  let retries = 0;
  f.sleep = async () => { retries++; f.refs['pull/7/merge'] = { object: { sha: MERGE } }; };
  await approve(f);
  assert.equal(retries, 1);
  assert.equal(f.outputs.merge_sha, MERGE);
});

test('a push while GitHub computes a merge invalidates the approval', async () => {
  const f = fixture();
  delete f.refs['pull/7/merge'];
  f.sleep = async () => { f.pull.head.sha = '4'.repeat(40); };
  await assert.rejects(approve(f), /changed/);
});

test('merge conflicts block approval', async () => {
  const f = fixture();
  f.pull.mergeable = false;
  await assert.rejects(approve(f), /merge conflicts/);
});

test('removing the review environment after planning fails closed', async () => {
  const f = fixture();
  await plan(f);
  f.github.rest.repos.getEnvironment = async () => { throw new Error('Not Found'); };
  await assert.rejects(approve(f), /Not Found/);
  assert.equal(f.outputs.merge_sha, undefined);
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
