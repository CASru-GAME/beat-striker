---
applyTo: '**'
---

# Project Directory Structure Guide

This guide explains the rules for file placement within the project. Applies to all files (**).

- New files should primarily be created under the `Assets/Project` directory.
- Script placement should be divided by scene, not using a general design like `Assets/Scripts`.
- Scripts or files used only in specific scenes should be placed directly in that scene's directory. This organizes the project structure and makes scene-by-scene management easier. By following this rule, it becomes harder for team members to arbitrarily change files outside their management scope.

## Examples

- Battle UI related scripts: `Assets/Project/BattleUI/`
- Title scene related files: `Assets/Project/TitleScene/`
- Stage select scene related: `Assets/Project/StageselectScene/`
- Striker related: `Assets/Project/Strikers/`

# Button Usage Guide

When implementing buttons in the project, do not use Unity's standard Button component. Instead, use the custom `Assets/App/Player/Botan` script with the following events:
- `onHover`
- `onHoverExit`
- `onClick`

This ensures consistency across the project and utilizes our custom button behavior.


