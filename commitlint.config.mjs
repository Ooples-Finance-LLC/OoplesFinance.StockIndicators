// Conventional Commits rules for this repository.
//
// This exists because release-please derives the next version and the CHANGELOG entirely from
// commit messages. A commit that does not parse simply vanishes from the release notes, and a
// `feat:` typed as `chore:` silently skips a minor bump - so the message format is load-bearing
// here, not cosmetic.
//
// The file MUST be named .mjs: wagoid/commitlint-github-action defaults its configFile to
// ./commitlint.config.mjs, and a .js sibling is silently ignored, falling back to
// config-conventional defaults without any warning.
export default {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // Longer body lines than the default 100, for detailed technical explanations.
    'body-max-line-length': [1, 'always', 200],
    // The conventional-commits parser classifies any "word: value" line as a footer, so a squashed
    // PR's "* fix(x): ..." bullets trip the default 100. Kept a warning at 200 to match the body.
    'footer-max-line-length': [1, 'always', 200],
    // 'deps' is added for Dependabot, which is configured in .github/dependabot.yml to prefix its
    // NuGet commits with it. Without this the bot's own PRs would fail their own lint.
    'type-enum': [2, 'always', [
      'feat', 'fix', 'docs', 'refactor', 'perf', 'test', 'chore', 'ci', 'style', 'revert',
      'build', 'deps',
    ]],
  },
  ignores: [
    // Merge commits are the only legitimate ignores - they cannot follow the conventional format.
    (message) => /^Merge pull request #\d+/.test(message),
    (message) => /^Merge branch '.+'/.test(message),
    (message) => /^Merge remote-tracking branch/.test(message),
    (message) => /^Merge (origin|upstream|master|main)/i.test(message),
    // Commits authored by GitHub Actions (auto-fixes, release-please, etc.).
    (message) => /Co-Authored-By: github-actions\[bot\]/.test(message),
  ],
};
