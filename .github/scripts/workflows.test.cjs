const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const path = require('node:path');
const YAML = require('yaml');
const { REVIEW_ENVIRONMENT } = require('./pr-tests.cjs');

const root = path.resolve(__dirname, '..', '..');
const workflow = name => YAML.parse(readFileSync(path.join(root, '.github/workflows', name), 'utf8'));
const ci = workflow('unity-tests.yml');
const runner = workflow('unity-test-runner.yml');
const dispatcher = workflow('fork-pr-tests.yml');
const needs = job => typeof job.needs === 'string' ? [job.needs] : job.needs || [];

function dependsOn(jobName, required) {
  return needs(ci.jobs[jobName]).some(parent => parent === required || dependsOn(parent, required));
}

test('private fork media and Unity execution both depend on the required-reviewer gate', () => {
  assert.equal(ci.jobs['approve-fork'].environment.name, REVIEW_ENVIRONMENT);
  assert.equal(dependsOn('resolve-fork-media', 'approve-fork'), true);
  assert.equal(dependsOn('test-fork', 'approve-fork'), true);
  assert.equal(dependsOn('test-fork', 'resolve-fork-media'), true);
  assert.equal(ci.jobs['approve-fork'].outputs.merge_sha, '${{ steps.approve.outputs.merge_sha }}');
  assert.equal(ci.jobs['test-fork'].with.checkout_ref, '${{ needs.approve-fork.outputs.merge_sha }}');
  assert.equal(ci.jobs['test-fork'].with.reviewed_head_sha, '${{ inputs.head_sha }}');
});

test('metadata planning and result reporting never check out contributor code', () => {
  for (const jobName of ['fork-plan', 'approve-fork', 'fork-result']) {
    const job = ci.jobs[jobName];
    const checkouts = job.steps.filter(step => step.uses?.startsWith('actions/checkout@'));
    assert.equal(checkouts.length, 1);
    assert.equal(checkouts[0].with.ref, '${{ github.workflow_sha }}');
    assert.equal(checkouts[0].with['persist-credentials'], false);
    assert.equal(job.environment === 'production', false);
  }
});

test('the pull_request_target dispatcher only requests a protected workflow run', () => {
  assert.ok(dispatcher.on.pull_request_target);
  const steps = dispatcher.jobs.request.steps;
  assert.equal(steps.length, 1);
  assert.equal(steps[0].uses, 'actions/github-script@v7');
  assert.match(steps[0].with.script, /createWorkflowDispatch/);
  assert.match(steps[0].with.script, /context.payload.repository.default_branch/);
  assert.equal(dispatcher.jobs.request.environment, undefined);
});

test('reviewed fork tests cannot write GitHub checks or poison shared caches', () => {
  const job = ci.jobs['test-fork'];
  assert.deepEqual(job.permissions, { contents: 'read' });
  assert.equal(job.with.publish_checks, false);
  assert.equal(job.with.save_cache, false);
  const testJob = runner.jobs.test;
  assert.equal(testJob.permissions, undefined);
  for (const step of testJob.steps.filter(step => step.uses === 'actions/cache/save@v4')) {
    assert.match(step.if, /inputs.save_cache/);
  }
  assert.equal(testJob.steps.some(step => step.uses === 'actions/cache@v4'), false);
  const gameci = testJob.steps.find(step => step.uses === 'game-ci/unity-test-runner@v4');
  assert.match(gameci.with.githubToken, /inputs.publish_checks/);
});

test('internal and reviewed PRs share the complete Unity and coverage pipeline', () => {
  assert.equal(ci.jobs.test.uses, ci.jobs['test-fork'].uses);
  const steps = runner.jobs.test.steps;
  const gameci = steps.find(step => step.uses === 'game-ci/unity-test-runner@v4');
  assert.equal(gameci.with.testMode, 'all');
  assert.equal(gameci.with.coverageEnabled, true);
  assert.ok(steps.find(step => step.name === 'Check coverage threshold'));
  assert.ok(steps.find(step => step.name === 'Check compiled architecture'));
  assert.equal(runner.env.UNITY_VERSION, ci.env.UNITY_VERSION);
  assert.equal(runner.env.UNITY_IMAGE_VERSION, ci.env.UNITY_IMAGE_VERSION);
});

test('fork results require actual Unity execution and target the PR check', () => {
  const job = ci.jobs['fork-result'];
  for (const dependency of ['fork-plan', 'approve-fork', 'resolve-fork-media', 'test-fork']) {
    assert.ok(needs(job).includes(dependency));
  }
  const script = job.steps.find(step => step.uses === 'actions/github-script@v7');
  assert.equal(script.env.CHECK_ID, '${{ needs.fork-plan.outputs.check_id }}');
  assert.equal(script.env.TEST_RESULT, '${{ needs.test-fork.result }}');
});

test('public PR checks cover every base branch without needing private media', () => {
  assert.equal(ci.on.pull_request?.branches, undefined);
  assert.deepEqual(needs(ci.jobs.lint), []);
  assert.match(ci.jobs['resolve-media'].if, /head.repo.full_name == github.repository/);
  assert.ok(ci.concurrency.group.includes('github.event_name'));
  assert.ok(ci.concurrency.group.includes('inputs.pull_request_number'));
});
