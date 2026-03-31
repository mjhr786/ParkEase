const { execFileSync, spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

loadDotEnv(path.resolve(__dirname, '..', '.env'));

const artifactType = (process.argv[2] || 'APK').toUpperCase();
const allowedArtifactTypes = new Set(['APK', 'AAB']);

if (!allowedArtifactTypes.has(artifactType)) {
  console.error(`[SlackRelease] Unsupported artifact type "${artifactType}". Use APK or AAB.`);
  process.exit(1);
}

const webhookUrl = process.env.SLACK_WEBHOOK_URL;
if (!webhookUrl) {
  console.error('[SlackRelease] Missing SLACK_WEBHOOK_URL.');
  process.exit(1);
}

const projectRoot = path.resolve(__dirname, '..');
const androidDir = path.join(projectRoot, 'android');
const appBuildGradlePath = path.join(androidDir, 'app', 'build.gradle');
const releaseStatePath = path.join(projectRoot, '.release-state.json');
const gradleTasks =
  artifactType === 'AAB'
    ? ['bundleRelease', 'appDistributionUploadRelease', '-PFIREBASE_ARTIFACT_TYPE=AAB']
    : ['assembleRelease', 'appDistributionUploadRelease', '-PFIREBASE_ARTIFACT_TYPE=APK'];

const buildMetadata = bumpAndroidVersionCode(appBuildGradlePath);
const gitContext = getGitContext(projectRoot, releaseStatePath);
const generatedReleaseNotes = buildReleaseNotes({
  buildMetadata,
  gitContext,
  explicitNotes: process.env.SLACK_RELEASE_NOTES || process.env.FIREBASE_RELEASE_NOTES || null,
});
process.env.FIREBASE_RELEASE_NOTES = generatedReleaseNotes;
console.log(
  `[SlackRelease] Bumped Android versionCode to ${buildMetadata.versionCode}` +
    (buildMetadata.versionName ? ` (versionName ${buildMetadata.versionName})` : '.')
);
console.log('[SlackRelease] Release notes summary:');
console.log(generatedReleaseNotes);

const gradleExecutable = process.platform === 'win32' ? 'gradlew.bat' : './gradlew';
const gradle = spawn(gradleExecutable, gradleTasks, {
  cwd: androidDir,
  env: process.env,
  stdio: ['inherit', 'pipe', 'pipe'],
});

let combinedOutput = '';

const pipeOutput = (stream, target) => {
  stream.on('data', chunk => {
    const text = chunk.toString();
    combinedOutput += text;
    target.write(text);
  });
};

pipeOutput(gradle.stdout, process.stdout);
pipeOutput(gradle.stderr, process.stderr);

gradle.on('error', error => {
  console.error('[SlackRelease] Failed to start Gradle.', error);
  process.exit(1);
});

gradle.on('close', async code => {
  if (code !== 0) {
    process.exit(code || 1);
  }

  const releaseInfo = extractReleaseInfo(combinedOutput);

  try {
    await postSlackMessage({
      webhookUrl,
      artifactType,
      releaseInfo,
      appName: process.env.SLACK_APP_NAME || 'ParkEase Android',
      buildMetadata,
      releaseNotes: generatedReleaseNotes,
      configuredChannel: process.env.SLACK_CHANNEL || null,
    });
    writeReleaseState(releaseStatePath, gitContext.currentCommit);
    console.log('[SlackRelease] Slack notification sent.');
  } catch (error) {
    console.error('[SlackRelease] Firebase upload succeeded, but Slack notification failed.');
    console.error(error);
    process.exit(1);
  }
});

function extractReleaseInfo(output) {
  const consoleUrl = matchLine(output, /View this release in the Firebase console:\s*(https:\/\/\S+)/);
  const testerUrl = matchLine(output, /Share this release with testers who have access:\s*(https:\/\/\S+)/);
  const binaryUrl = matchLine(output, /Download the release binary \(link expires in 1 hour\):\s*(https:\/\/\S+)/);

  return { consoleUrl, testerUrl, binaryUrl };
}

function matchLine(text, regex) {
  const match = text.match(regex);
  return match ? match[1] : null;
}

async function postSlackMessage({
  webhookUrl,
  artifactType,
  releaseInfo,
  appName,
  buildMetadata,
  releaseNotes,
  configuredChannel,
}) {
  const lines = [
    `*${appName}* ${artifactType} uploaded to Firebase App Distribution.`,
    `Build number: ${buildMetadata.versionCode}${buildMetadata.versionName ? ` (${buildMetadata.versionName})` : ''}`,
    releaseNotes,
  ];

  if (configuredChannel) {
    lines.push(`Configured Slack target: ${configuredChannel}`);
  }

  if (releaseInfo.testerUrl) {
    lines.push(`<${releaseInfo.testerUrl}|Open tester release>`);
  }

  if (releaseInfo.consoleUrl) {
    lines.push(`<${releaseInfo.consoleUrl}|Open in Firebase console>`);
  }

  if (releaseInfo.binaryUrl) {
    lines.push(`<${releaseInfo.binaryUrl}|Temporary direct download (expires in 1 hour)>`);
  }

  const payload = {
    text: `${appName} ${artifactType} uploaded to Firebase App Distribution.`,
    blocks: [
      {
        type: 'section',
        text: {
          type: 'mrkdwn',
          text: lines.join('\n'),
        },
      },
    ],
  };

  const response = await fetch(webhookUrl, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
  });

  const body = await response.text();
  if (!response.ok || body.trim() !== 'ok') {
    throw new Error(`Slack webhook failed with status ${response.status}: ${body}`);
  }
}

function bumpAndroidVersionCode(filePath) {
  if (!fs.existsSync(filePath)) {
    throw new Error(`[SlackRelease] Android build file not found at ${filePath}`);
  }

  const contents = fs.readFileSync(filePath, 'utf8');
  const versionCodeMatch = contents.match(/versionCode\s+(\d+)/);
  if (!versionCodeMatch) {
    throw new Error('[SlackRelease] Could not find versionCode in android/app/build.gradle');
  }

  const versionNameMatch = contents.match(/versionName\s+"([^"]+)"/);
  const currentVersionCode = Number(versionCodeMatch[1]);
  const nextVersionCode = currentVersionCode + 1;
  const updatedContents = contents.replace(/versionCode\s+\d+/, `versionCode ${nextVersionCode}`);

  fs.writeFileSync(filePath, updatedContents, 'utf8');

  return {
    versionCode: nextVersionCode,
    versionName: versionNameMatch ? versionNameMatch[1] : null,
  };
}

function getGitContext(projectRootPath, statePath) {
  const currentCommit = runGit(projectRootPath, ['rev-parse', 'HEAD']) || null;
  const latestCommitSubject = runGit(projectRootPath, ['log', '-1', '--pretty=%s']) || null;
  const previousReleaseCommit = readReleaseState(statePath)?.lastReleasedCommit || null;
  const commitSummaries = getCommitSummaries(projectRootPath, previousReleaseCommit, currentCommit);
  const dirtyFiles = getDirtyFiles(projectRootPath);

  return {
    currentCommit,
    latestCommitSubject,
    previousReleaseCommit,
    commitSummaries,
    dirtyFiles,
  };
}

function buildReleaseNotes({ buildMetadata, gitContext, explicitNotes }) {
  const lines = [];

  if (explicitNotes) {
    lines.push(explicitNotes);
  } else {
    lines.push(`Build ${buildMetadata.versionCode}${buildMetadata.versionName ? ` (${buildMetadata.versionName})` : ''}`);
  }

  if (gitContext.commitSummaries.length > 0) {
    lines.push('Changes:');
    for (const summary of gitContext.commitSummaries.slice(0, 8)) {
      lines.push(`- ${summary}`);
    }
  } else if (gitContext.latestCommitSubject) {
    lines.push(`Latest commit: ${gitContext.latestCommitSubject}`);
  }

  if (gitContext.dirtyFiles.length > 0) {
    lines.push('Local changes included:');
    for (const file of gitContext.dirtyFiles.slice(0, 8)) {
      lines.push(`- ${file}`);
    }
  }

  return lines.join('\n');
}

function getCommitSummaries(projectRootPath, previousCommit, currentCommit) {
  if (!currentCommit) {
    return [];
  }

  const range =
    previousCommit && previousCommit !== currentCommit
      ? `${previousCommit}..${currentCommit}`
      : currentCommit;
  const output = runGit(projectRootPath, ['log', '--pretty=%s', range]);
  if (!output) {
    return [];
  }

  const summaries = output
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean);

  if (previousCommit && previousCommit === currentCommit) {
    return [];
  }

  return summaries;
}

function getDirtyFiles(projectRootPath) {
  const output = runGit(projectRootPath, ['status', '--short']);
  if (!output) {
    return [];
  }

  return output
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean)
    .map(line => line.replace(/^[A-Z?]+\s+/, ''));
}

function runGit(projectRootPath, args) {
  try {
    return execFileSync('git', args, {
      cwd: projectRootPath,
      encoding: 'utf8',
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch {
    return '';
  }
}

function readReleaseState(filePath) {
  if (!fs.existsSync(filePath)) {
    return null;
  }

  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch {
    return null;
  }
}

function writeReleaseState(filePath, currentCommit) {
  if (!currentCommit) {
    return;
  }

  const state = {
    lastReleasedCommit: currentCommit,
    updatedAt: new Date().toISOString(),
  };

  fs.writeFileSync(filePath, `${JSON.stringify(state, null, 2)}\n`, 'utf8');
}

function loadDotEnv(filePath) {
  if (!fs.existsSync(filePath)) {
    return;
  }

  const contents = fs.readFileSync(filePath, 'utf8');
  for (const rawLine of contents.split(/\r?\n/)) {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) {
      continue;
    }

    const equalsIndex = line.indexOf('=');
    if (equalsIndex === -1) {
      continue;
    }

    const key = line.slice(0, equalsIndex).trim();
    if (!key || process.env[key]) {
      continue;
    }

    let value = line.slice(equalsIndex + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }

    process.env[key] = value;
  }
}
