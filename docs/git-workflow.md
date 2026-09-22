# Git Workflow

## Commits

Commit messages use imperative language and describe the modification made.

Good:
    - Add player contract validation
    - Update tests to take into account X

Bad:
    - Fixed
    - Changes

Commit messages will use descriptors to describe the nature of the modification:
    - feat: a new feature has been added
    - fix: relating to bug fixes or large improvements (e.g: fixing a crash when X happens or speeding up simulation time by Y seconds)
    - chore: small improvements or changes (e.g: translation change, name changes)
    - tests: relating to the addition/updating of tests

## Branches

Branches have the issue (GitHub Issue, Jira Ticket) name. If no issue has been provided, branches have descriptive names (e.g: feature/player-development). 

## Pull Requests

Pull requests follow the [PR template](../.github/pull_request_template.md). 

### Link to issue

Just paste a link to the issue or ticket being used as the basis for the PR

### Changes

Write an informal list of changes made in the PR.

### Testing

Write steps that another user or reviewer may use in order to validate the changes in this PR.
This may include:
    - Steps within the application
    - Terminal commands (in the case of test file changes)