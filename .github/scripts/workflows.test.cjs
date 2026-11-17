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
const mediaResolver = workflow('resolve-media.yml');
const dispatcher = workflow('pr-test-request.yml');
const needs = job => typeof job.needs === 'string' ? [job.needs] : job.needs || [];
const environmentName = job => typeof job.environment === 'string'
  ? job.environment
  : job.environment?.name;
const forkPullRequestCondition =
  'github.event.pull_request.head.repo.full_name != github.repository';
const trustedPullRequestCondition =
  'github.event.pull_request.head.repo.full_name == github.repository';

function dependsOn(jobName, required) {
  return needs(ci.jobs[jobName]).some(parent => parent === required || dependsOn(parent, required));
}

test('private PR media and Unity execution both depend on the required-reviewer gate', () => {
  assert.notEqual(REVIEW_ENVIRONMENT.toLowerCase(), 'production');
  assert.equal(ci.jobs['approve-pr'].environment.name, REVIEW_ENVIRONMENT);
  assert.equal(environmentName(mediaResolver.jobs.resolve), 'production');
  assert.equal(environmentName(runner.jobs.test), 'production');
  assert.equal(dependsOn('resolve-pr-media', 'approve-pr'), true);
  assert.equal(dependsOn('test-pr', 'approve-pr'), true);
  assert.equal(dependsOn('test-pr', 'resolve-pr-media'), true);
  assert.equal(ci.jobs['approve-pr'].outputs.base_sha, '${{ steps.approve.outputs.base_sha }}');
  assert.equal(ci.jobs['approve-pr'].outputs.head_repository, '${{ steps.approve.outputs.head_repository }}');
  assert.equal(ci.jobs['approve-pr'].permissions.checks, 'write');
  assert.equal(ci.jobs['test-pr'].with.base_sha, '${{ needs.approve-pr.outputs.base_sha }}');
  assert.equal(ci.jobs['test-pr'].with.reviewed_head_sha, '${{ inputs.head_sha }}');
  assert.equal(ci.jobs['test-pr'].with.reviewed_head_repository, '${{ needs.approve-pr.outputs.head_repository }}');
});

test('metadata planning and result reporting never check out contributor code', () => {
  for (const jobName of ['pr-plan', 'approve-pr', 'pr-result']) {
    const job = ci.jobs[jobName];
    const checkouts = job.steps.filter(step => step.uses?.startsWith('actions/checkout@'));
    assert.equal(checkouts.length, 1);
    assert.equal(checkouts[0].with.ref, '${{ github.workflow_sha }}');
    assert.equal(checkouts[0].with['persist-credentials'], false);
    assert.notEqual(environmentName(job), 'production');
  }
});

test('the pull_request_target dispatcher requests a protected workflow run only for fork PRs', () => {
  assert.ok(dispatcher.on.pull_request_target);
  assert.equal(dispatcher.jobs.request.if, forkPullRequestCondition);
  const steps = dispatcher.jobs.request.steps;
  assert.equal(steps.length, 1);
  assert.equal(steps[0].uses, 'actions/github-script@v7');
  assert.match(steps[0].with.script, /createWorkflowDispatch/);
  assert.match(steps[0].with.script, /context.payload.repository.default_branch/);
  assert.equal(dispatcher.jobs.request.environment, undefined);
});

test('same-repository PRs run private tests automatically from the protected workflow revision', () => {
  const media = dispatcher.jobs['trusted-media'];
  const unity = dispatcher.jobs['trusted-test'];
  assert.equal(media.if, trustedPullRequestCondition);
  assert.equal(media.uses, './.github/workflows/resolve-media.yml');
  assert.equal(media.with.branch, '${{ github.event.pull_request.head.ref }}');
  assert.equal(media.secrets, 'inherit');
  assert.equal(unity.if, trustedPullRequestCondition);
  assert.ok(needs(unity).includes('trusted-media'));
  assert.equal(unity.uses, './.github/workflows/unity-test-runner.yml');
  assert.equal(unity.with.base_sha, '${{ github.event.pull_request.base.sha }}');
  assert.equal(unity.with.reviewed_head_sha, '${{ github.event.pull_request.head.sha }}');
  assert.equal(unity.with.reviewed_head_repository, '${{ github.event.pull_request.head.repo.full_name }}');
  assert.equal(unity.with.media_sha, '${{ needs.trusted-media.outputs.sha }}');
  assert.equal(unity.secrets, 'inherit');
});

test('the shared runner prepares and verifies PR merges from exact base and head revisions', () => {
  assert.equal(runner.on.workflow_call.inputs.base_sha.default, '');
  assert.equal(runner.on.workflow_call.inputs.reviewed_head_sha.default, '');
  assert.equal(runner.on.workflow_call.inputs.reviewed_head_repository.default, '');
  const baseCheckout = runner.jobs.test.steps.find(step => step.name === 'Checkout PR base');
  const headCheckout = runner.jobs.test.steps.find(step => step.name === 'Checkout reviewed PR head');
  const merge = runner.jobs.test.steps.find(step => step.name === 'Prepare reviewed merge');
  assert.equal(baseCheckout.with.ref, '${{ inputs.base_sha }}');
  assert.equal(baseCheckout.with['fetch-depth'], 0);
  assert.equal(baseCheckout.with['persist-credentials'], false);
  assert.equal(headCheckout.with.repository, '${{ inputs.reviewed_head_repository }}');
  assert.equal(headCheckout.with.ref, '${{ inputs.reviewed_head_sha }}');
  assert.equal(headCheckout.with['fetch-depth'], 0);
  assert.equal(headCheckout.with['persist-credentials'], false);
  assert.match(merge.run, /git fetch --no-tags \.ci\/reviewed-head/);
  assert.match(merge.run, /merge --no-ff --no-edit/);
  assert.match(merge.run, /prepared merge does not contain the approved revisions/);
});

test('reviewed PR tests cannot write GitHub checks or poison shared caches', () => {
  const job = ci.jobs['test-pr'];
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

test('default-branch and reviewed PR tests share the complete Unity and coverage pipeline', () => {
  assert.equal(ci.jobs.test.uses, ci.jobs['test-pr'].uses);
  const steps = runner.jobs.test.steps;
  const gameci = steps.find(step => step.uses === 'game-ci/unity-test-runner@v4');
  assert.equal(gameci.with.testMode, 'all');
  assert.equal(gameci.with.coverageEnabled, true);
  assert.ok(steps.find(step => step.name === 'Check coverage threshold'));
  assert.ok(steps.find(step => step.name === 'Check compiled architecture'));
  assert.equal(runner.env.UNITY_VERSION, ci.env.UNITY_VERSION);
  assert.equal(runner.env.UNITY_IMAGE_VERSION, ci.env.UNITY_IMAGE_VERSION);
});

test('the shared runner stages optional native math after the verified merge and before Unity', () => {
  const steps = runner.jobs.test.steps;
  const merge = steps.findIndex(step => step.name === 'Prepare reviewed merge');
  const native = steps.findIndex(step => step.name === 'Build and verify Linux native vector library');
  const unity = steps.findIndex(step => step.uses === 'game-ci/unity-test-runner@v4');
  assert.ok(merge >= 0 && merge < native && native < unity);
  assert.equal(steps[native].if, "${{ hashFiles('Native/TacticalMath/CMakeLists.txt') != '' }}");
  assert.match(steps[native].run, /python3 Tools\/build_tactical_math\.py/);
  assert.match(steps[native].run, /--target linux-x64/);
  assert.equal(steps[native].env, undefined);
});

test('PR results require actual Unity execution and target the PR check', () => {
  const job = ci.jobs['pr-result'];
  for (const dependency of ['pr-plan', 'approve-pr', 'resolve-pr-media', 'test-pr']) {
    assert.ok(needs(job).includes(dependency));
  }
  const script = job.steps.find(step => step.uses === 'actions/github-script@v7');
  assert.equal(script.env.CHECK_ID, '${{ needs.pr-plan.outputs.check_id }}');
  assert.equal(script.env.TEST_RESULT, '${{ needs.test-pr.result }}');
});

test('fork pull_request jobs never receive repository or environment secrets', () => {
  assert.equal(ci.on.pull_request?.branches, undefined);
  assert.deepEqual(needs(ci.jobs.lint), []);
  assert.equal(ci.jobs['resolve-media'].if, "github.event_name == 'push'");
  assert.equal(ci.jobs.test.if, "github.event_name == 'push'");
  assert.equal(ci.jobs.build.if, "github.event_name == 'push'");
  for (const jobName of ['lint', 'windows-build-tools']) {
    assert.doesNotMatch(JSON.stringify(ci.jobs[jobName]), /secrets\.|"secrets":"inherit"/);
  }
  assert.ok(ci.concurrency.group.includes('github.event_name'));
  assert.ok(ci.concurrency.group.includes('inputs.pull_request_number'));
});

test('every CI secret-bearing job is restricted to a default-branch push or reviewed PR', () => {
  for (const [jobName, job] of Object.entries(ci.jobs)) {
    const definition = JSON.stringify(job);
    if (!/secrets\.|"secrets":"inherit"/.test(definition)) continue;
    const runsOnlyOnPush = job.if === "github.event_name == 'push'";
    const dependsOnReview = dependsOn(jobName, 'approve-pr');
    assert.equal(
      runsOnlyOnPush || dependsOnReview,
      true,
      `${jobName} can receive secrets without a protected execution path`,
    );
  }
});

test('every dispatcher secret-bearing job is restricted to same-repository PRs', () => {
  for (const [jobName, job] of Object.entries(dispatcher.jobs)) {
    const definition = JSON.stringify(job);
    if (!/secrets\.|"secrets":"inherit"/.test(definition)) continue;
    assert.equal(
      job.if,
      trustedPullRequestCondition,
      `${jobName} can receive secrets for a fork PR`,
    );
  }
});
