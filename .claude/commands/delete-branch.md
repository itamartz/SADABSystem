---
description: Delete current feature branch (local and remote)
---

Delete the current branch after it has been merged:

1. Get the current branch name
2. Verify the branch is not 'main' or 'master'
3. Ask user to confirm deletion
4. Switch to main branch
5. Delete local branch using `git branch -d <branch-name>`
6. Delete remote branch using `git push origin --delete <branch-name>`
7. Fetch and prune remote branches
8. Confirm deletion is complete
9. Show remaining branches

Safety checks:
- Never delete main/master branch
- Warn if branch is not merged
- Confirm before deleting remote branch
