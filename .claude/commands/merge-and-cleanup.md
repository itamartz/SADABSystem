---
description: Merge branch to main and delete the feature branch
---

Complete workflow to merge and cleanup a feature branch:

## Step 1: Pre-merge Checks
- Verify current branch is not main
- Check if there are uncommitted changes
- Fetch latest from remote

## Step 2: Merge to Main
- Switch to main branch
- Pull latest changes
- Merge the feature branch
- Resolve conflicts if any
- Push to main (or inform user to create PR)

## Step 3: Cleanup
- Switch back to main (if not already)
- Delete local feature branch
- Delete remote feature branch
- Prune remote references
- Show summary of what was deleted

## Step 4: Verification
- Show current branch (should be main)
- Show recent commits on main
- Confirm cleanup is complete

This combines /merge-branch and /delete-branch into one streamlined operation.
