# Issue tracker: GitHub

Issues and specs live in GitHub Issues for `tylerforgione/hockeysim`.
Prefer the `gh` CLI from this clone; it infers the repository from the remote.
If `gh` is unavailable, use authenticated GitHub connector tools, explicitly
targeting `tylerforgione/hockeysim`. The same issue, labeling, PR, and review
policies apply to either tool. Read [Git workflow](../git-workflow.md) for when
issues are required and what agents may publish.

## Authentication and project access

Check the active GitHub CLI account and token scopes with:

```sh
gh auth status --json hosts
```

In a restricted agent environment, `gh repo view` may initially report
`error connecting to api.github.com` because outbound network access is blocked.
That error alone does not mean the token is invalid. Retry the same read-only
command with network access or the environment's escalation mechanism:

```sh
gh repo view tylerforgione/hockeysim \
  --json nameWithOwner,url,viewerPermission,isPrivate
```

List the owner's projects with:

```sh
gh project list --owner tylerforgione --format json --limit 100
```

To verify which Projects v2 boards are linked to this repository, query the
repository connection directly:

```sh
gh api graphql -f query='query {
  repository(owner: "tylerforgione", name: "hockeysim") {
    projectsV2(first: 100) {
      totalCount
      nodes { number title url closed }
    }
  }
}'
```

## Conventions

- Create: `gh issue create --title "..." --body-file <path>`.
- Read: `gh issue view <number> --comments`. Include labels when evaluating
  the issue.
- List: `gh issue list --state open --json number,title,body,labels,comments`.
  Apply label and state filters as needed.
- Comment: `gh issue comment <number> --body-file <path>`.
- Add labels: `gh issue edit <number> --add-label "..."`.
- Remove labels: `gh issue edit <number> --remove-label "..."`.
- Close: `gh issue close <number> --comment "..."`.

For multiline bodies, write the exact Markdown to a temporary file and
pass it with `--body-file`.

Read `docs/agents/triage-labels.md` for the triage label vocabulary.

## Skill terminology

“Publish to the issue tracker” means create a GitHub issue.
“Fetch the relevant ticket” means read the issue and its comments.

## Pull requests as a triage surface

**PRs as a request surface: no.**
