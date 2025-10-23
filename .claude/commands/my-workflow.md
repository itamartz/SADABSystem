---
description: Follow my standard development workflow for new features
---

When I ask you to implement a new feature, follow this workflow:

1. **Plan Phase**
   - Create a TODO list with all steps
   - Break down into small, manageable tasks
   - Ask clarifying questions if anything is ambiguous

2. **Implementation Phase**
   - Create a feature branch: `claude/<feature-name>-<session-id>`
   - Read all relevant files first
   - Make changes incrementally
   - Update TODO list as you progress

3. **Configuration Phase**
   - Ensure all strings are in appsettings.json
   - Add ToString() overrides to any new models/DTOs
   - Update dependency injection if needed

4. **Completion Phase**
   - Commit with descriptive message including "🤖 Generated with Claude Code"
   - Push to feature branch
   - Inform me that PR is ready for creation
   - Wait for my approval before merging

5. **Cleanup Phase**
   - After I merge, delete the feature branch
   - Update local main branch

Always follow this workflow unless I explicitly ask you to do something different.
