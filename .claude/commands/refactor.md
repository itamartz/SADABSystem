---
description: Refactor code following my standards
---

When refactoring code:

1. **Read First**: Always read the entire file/class before making changes
2. **Configuration**: Move any hardcoded strings/values to appsettings.json
3. **ToString()**: Add ToString() override if it's a model/DTO and doesn't have one
4. **Dependency Injection**: Use constructor injection for IConfiguration
5. **Error Handling**: Use try-catch with logging, error messages from config
6. **Consistency**: Follow existing patterns in the codebase
7. **Comments**: Remove unnecessary comments, code should be self-documenting
8. **Commit Often**: Make small, focused commits

Refactoring checklist:
- [ ] All strings from configuration
- [ ] ToString() overrides present
- [ ] IConfiguration injected properly
- [ ] Error messages from config
- [ ] Consistent naming conventions
- [ ] No magic numbers/strings
- [ ] Proper null checking
