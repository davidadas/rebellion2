const REVIEW_ENVIRONMENT = 'Fork PR tests';
const CHECK_NAME = 'Unity tests (reviewed PR)';

/** Validate dispatch inputs before using them in API requests or checkout refs. */
function parseRequest(number, headSha) {
  if (!/^[1-9]\d*$/.test(String(number)) || !Number.isSafeInteger(Number(number))) {
    throw new Error('A positive pull request number is required.');
  }
  if (!/^[a-f0-9]{40}$/.test(headSha || '')) {
    throw new Error('The exact pull request head SHA is required.');
  }
  return { number: Number(number), headSha };
}

/** Fail closed if GitHub would otherwise create an unprotected review environment. */
async function requireReviewProtection(github, repo) {
  const { data } = await github.rest.repos.getEnvironment({
    ...repo, environment_name: REVIEW_ENVIRONMENT,
  });
  const review = data.protection_rules?.find(rule => rule.type === 'required_reviewers');
  if (!review?.reviewers?.length) {
    throw new Error(`Configure required reviewers on the '${REVIEW_ENVIRONMENT}' environment first.`);
  }
}

/** Read only metadata, rejecting closed requests, other repositories, and changed heads. */
async function readRequest(github, repo, request) {
  const { data } = await github.rest.pulls.get({ ...repo, pull_number: request.number });
  const expectedRepository = `${repo.owner}/${repo.repo}`.toLowerCase();
  if (data.base?.repo?.full_name?.toLowerCase() !== expectedRepository) {
    throw new Error('The pull request must target this repository.');
  }
  if (data.state !== 'open') throw new Error('The pull request is no longer open.');
  if (data.head?.sha !== request.headSha) {
    throw new Error('The pull request changed. Start a new run and approve its new commit.');
  }
  return data;
}

/** Publish a pending PR check before any approval or private-content job starts. */
async function plan({ github, context, core, number, headSha }) {
  const request = parseRequest(number, headSha);
  await requireReviewProtection(github, context.repo);
  await readRequest(github, context.repo, request);
  const runUrl = `${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/actions/runs/${context.runId}`;
  const { data: check } = await github.rest.checks.create({
    ...context.repo,
    name: CHECK_NAME,
    head_sha: request.headSha,
    status: 'in_progress',
    details_url: runUrl,
    output: {
      title: 'Awaiting maintainer approval',
      summary: `Review commit ${request.headSha} before granting access to private test content.`,
    },
  });
  core.setOutput('check_id', String(check.id));
  core.setOutput('review_url', `${context.serverUrl}/${context.repo.owner}/${context.repo.repo}/pull/${request.number}/commits/${request.headSha}`);
}

/** Bind the approved test run to the current base and reviewed head revisions. */
async function approve({ github, context, core, number, headSha, checkId }) {
  const request = parseRequest(number, headSha);
  await requireReviewProtection(github, context.repo);
  const pull = await readRequest(github, context.repo, request);
  if (!/^[a-f0-9]{40}$/.test(pull.base?.sha || '')) {
    throw new Error('The exact pull request base SHA is required.');
  }
  if (!pull.head?.repo?.full_name) throw new Error('The pull request head repository is required.');
  if (!/^[1-9]\d*$/.test(String(checkId)) || !Number.isSafeInteger(Number(checkId))) {
    throw new Error('A valid PR check ID is required.');
  }

  core.setOutput('base_sha', pull.base.sha);
  core.setOutput('head_repository', pull.head.repo.full_name);
  core.setOutput('media_branch', pull.head.ref);
  await github.rest.checks.update({
    ...context.repo,
    check_run_id: Number(checkId),
    status: 'in_progress',
    output: {
      title: 'Unity tests running',
      summary: `Testing reviewed commit ${request.headSha} against base ${pull.base.sha}.`,
    },
  });
}

/** Report the actual test outcome on the reviewed PR head, rather than on the default branch. */
async function report({ github, context, checkId, testResult, mediaResult, approvalResult }) {
  if (!/^[1-9]\d*$/.test(String(checkId)) || !Number.isSafeInteger(Number(checkId))) {
    throw new Error('A valid PR check ID is required.');
  }
  const success = approvalResult === 'success' && mediaResult === 'success' && testResult === 'success';
  const cancelled = [testResult, mediaResult, approvalResult].includes('cancelled');
  await github.rest.checks.update({
    ...context.repo,
    check_run_id: Number(checkId),
    status: 'completed',
    conclusion: success ? 'success' : cancelled ? 'cancelled' : 'failure',
    output: {
      title: success ? 'Unity tests passed' : 'Unity tests did not complete successfully',
      summary: `Approval: ${approvalResult}. Media setup: ${mediaResult}. Unity tests: ${testResult}.`,
    },
  });
}

module.exports = { REVIEW_ENVIRONMENT, CHECK_NAME, parseRequest, plan, approve, report };
