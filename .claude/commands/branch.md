---
description: Create a new feature branch with proper naming convention
---

Create a new feature branch following the SADAB project's branching strategy:

1. **Ask for descriptive name**
   - Prompt me for a descriptive name for the feature/task
   - The name should be brief but clear (e.g., "add-user-auth", "fix-signalr-connection")

2. **Create branch**
   - Use today's date in YYYYMMDD format
   - Create branch with pattern: `claude/<descriptive-name>-<YYYYMMDD>`
   - Check out to the new branch

3. **Confirm**
   - Show the created branch name
   - Confirm that I'm now on the new branch

Example: If today is October 30, 2025 and the feature is "add-logging", create branch: `claude/add-logging-20251030`

**Important**: Always create the branch BEFORE starting any code changes, as per CLAUDE.md guidelines.
