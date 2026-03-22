const https = require('https');
const fs = require('fs');

const BOT_TOKEN = process.env.DISCORD_BOT_TOKEN;
const PR_CHANNEL = process.env.DISCORD_PR_CHANNEL_ID;
const DEPLOY_CHANNEL = process.env.DISCORD_DEPLOYMENTS_CHANNEL_ID;
const ISSUES_CHANNEL = process.env.DISCORD_ISSUES_CHANNEL_ID;

// Build GitHub username → Discord ID map from env vars
const USER_MAP = {};
for (const [key, val] of Object.entries(process.env)) {
  if (key.startsWith('DISCORD_ID_')) {
    USER_MAP[key.replace('DISCORD_ID_', '').toLowerCase()] = val;
  }
}

const eventName = process.env.GITHUB_EVENT_NAME;
const payload = JSON.parse(fs.readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));

function getMention(githubUsername) {
  const id = USER_MAP[githubUsername?.toLowerCase()];
  if (!id) console.warn(`[mention] No Discord ID for GitHub user "${githubUsername}"`);
  return id ? `<@${id}>` : `\`${githubUsername}\``;
}

function truncate(str, max = 300) {
  if (!str) return '_No description provided._';
  return str.length > max ? str.slice(0, max) + '…' : str;
}

function diffStats(pr) {
  return `\`+${pr.additions}\` \`-${pr.deletions}\` · 📁 ${pr.changed_files} file${pr.changed_files === 1 ? '' : 's'}`;
}

function labelList(item) {
  if (!item.labels?.length) return null;
  return item.labels.map((l) => `\`${l.name}\``).join(' ');
}

function post(channelId, channelLabel, content, embeds = []) {
  return new Promise((resolve, reject) => {
    if (!channelId) {
      console.warn(`[discord] Skipping ${channelLabel} — channel ID not set`);
      return resolve();
    }
    const body = JSON.stringify({ content, embeds });
    console.log(`[discord] Sending to ${channelLabel}: "${content}"`);
    const req = https.request(
      {
        hostname: 'discord.com',
        path: `/api/v10/channels/${channelId}/messages`,
        method: 'POST',
        headers: {
          Authorization: `Bot ${BOT_TOKEN}`,
          'Content-Type': 'application/json',
          'Content-Length': Buffer.byteLength(body),
        },
      },
      (res) => {
        let data = '';
        res.on('data', (chunk) => (data += chunk));
        res.on('end', () => {
          console.log(`[discord] ${channelLabel} responded ${res.statusCode}`);
          if (res.statusCode >= 400) {
            console.error(`[discord] Error body: ${data}`);
            reject(new Error(`Discord API error ${res.statusCode}`));
          } else {
            resolve();
          }
        });
      }
    );
    req.on('error', reject);
    req.write(body);
    req.end();
  });
}

async function main() {
  const action = payload.action;
  console.log(`[notify] event="${eventName}" action="${action}"`);

  if (eventName === 'pull_request') {
    const pr = payload.pull_request;
    const sender = payload.sender;
    const actor = getMention(sender.login);
    const fields = [];

    if (action === 'opened') {
      fields.push({ name: 'Changes', value: diffStats(pr), inline: true });
      const labels = labelList(pr);
      if (labels) fields.push({ name: 'Labels', value: labels, inline: true });
      if (pr.milestone) fields.push({ name: 'Milestone', value: pr.milestone.title, inline: true });
      fields.push({ name: 'Branch', value: `\`${pr.head.ref}\` → \`${pr.base.ref}\``, inline: false });

      await post(PR_CHANNEL, '#pull-requests', `${actor} opened a pull request`, [
        {
          author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
          title: `#${pr.number} ${pr.title}`,
          url: pr.html_url,
          description: truncate(pr.body),
          color: 0x2ecc71,
          fields,
          timestamp: new Date().toISOString(),
        },
      ]);
    } else if (action === 'closed') {
      if (pr.merged) {
        fields.push({ name: 'Changes', value: diffStats(pr), inline: true });
        const labels = labelList(pr);
        if (labels) fields.push({ name: 'Labels', value: labels, inline: true });
        if (pr.milestone) fields.push({ name: 'Milestone', value: pr.milestone.title, inline: true });

        const embed = {
          author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
          title: `#${pr.number} ${pr.title}`,
          url: pr.html_url,
          color: 0x9b59b6,
          fields,
          timestamp: new Date().toISOString(),
        };

        await post(PR_CHANNEL, '#pull-requests', `${actor} merged a pull request`, [embed]);

        if (pr.base.ref === 'main') {
          console.log(`[notify] Merged into main — posting to #deployments`);
          await post(DEPLOY_CHANNEL, '#deployments', `🚀 **Deployed to main**`, [
            {
              author: { name: `Merged by ${sender.login}`, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
              title: `#${pr.number} ${pr.title}`,
              url: pr.html_url,
              description: truncate(pr.body),
              color: 0x9b59b6,
              fields,
              timestamp: new Date().toISOString(),
            },
          ]);
        }
      } else {
        await post(PR_CHANNEL, '#pull-requests', `${actor} closed a pull request without merging`, [
          {
            author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
            title: `#${pr.number} ${pr.title}`,
            url: pr.html_url,
            color: 0xe74c3c,
            timestamp: new Date().toISOString(),
          },
        ]);
      }
    } else if (action === 'review_requested') {
      const reviewer = payload.requested_reviewer?.login;
      if (reviewer) {
        await post(PR_CHANNEL, '#pull-requests', `${actor} requested a review from ${getMention(reviewer)}`, [
          {
            author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
            title: `#${pr.number} ${pr.title}`,
            url: pr.html_url,
            color: 0xf1c40f,
            timestamp: new Date().toISOString(),
          },
        ]);
      }
    }
  } else if (eventName === 'pull_request_review') {
    if (action !== 'submitted') return;
    const { review, pull_request: pr, sender } = payload;
    const reviewer = getMention(sender.login);
    const prAuthor = getMention(pr.user.login);

    if (review.state === 'approved') {
      await post(PR_CHANNEL, '#pull-requests', `${reviewer} approved ${prAuthor}'s pull request`, [
        {
          author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
          title: `#${pr.number} ${pr.title} ✅`,
          url: pr.html_url,
          color: 0x2ecc71,
          timestamp: new Date().toISOString(),
        },
      ]);
    } else if (review.state === 'changes_requested') {
      await post(PR_CHANNEL, '#pull-requests', `${reviewer} requested changes on ${prAuthor}'s pull request`, [
        {
          author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
          title: `#${pr.number} ${pr.title} 🔄`,
          url: pr.html_url,
          description: truncate(review.body),
          color: 0xe67e22,
          timestamp: new Date().toISOString(),
        },
      ]);
    } else if (review.state === 'commented' && review.body) {
      await post(PR_CHANNEL, '#pull-requests', `${reviewer} left a review on ${prAuthor}'s pull request`, [
        {
          author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
          title: `#${pr.number} ${pr.title}`,
          url: pr.html_url,
          description: truncate(review.body),
          color: 0x3498db,
          timestamp: new Date().toISOString(),
        },
      ]);
    }
  } else if (eventName === 'pull_request_review_comment') {
    if (action !== 'created') return;
    const { comment, pull_request: pr, sender } = payload;
    await post(PR_CHANNEL, '#pull-requests', `${getMention(sender.login)} commented on a pull request`, [
      {
        author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
        title: `#${pr.number} ${pr.title}`,
        url: comment.html_url,
        description: truncate(comment.body),
        color: 0x3498db,
        timestamp: new Date().toISOString(),
      },
    ]);
  } else if (eventName === 'issue_comment') {
    if (action !== 'created' || !payload.issue.pull_request) return;
    const { comment, issue, sender } = payload;
    await post(PR_CHANNEL, '#pull-requests', `${getMention(sender.login)} commented on a pull request`, [
      {
        author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
        title: `#${issue.number} ${issue.title}`,
        url: comment.html_url,
        description: truncate(comment.body),
        color: 0x3498db,
        timestamp: new Date().toISOString(),
      },
    ]);
  } else if (eventName === 'issues') {
    if (action !== 'assigned') return;
    const { issue, assignee, sender } = payload;
    await post(ISSUES_CHANNEL, '#issues', `${getMention(sender.login)} assigned ${getMention(assignee.login)} to an issue`, [
      {
        author: { name: sender.login, icon_url: sender.avatar_url, url: `https://github.com/${sender.login}` },
        title: `#${issue.number} ${issue.title}`,
        url: issue.html_url,
        description: truncate(issue.body),
        color: 0xe67e22,
        fields: labelList(issue) ? [{ name: 'Labels', value: labelList(issue), inline: true }] : [],
        timestamp: new Date().toISOString(),
      },
    ]);
  }
}

main().catch((err) => {
  console.error('[notify] Fatal error:', err.message);
  process.exit(1);
});
