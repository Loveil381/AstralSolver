# 项目身份与基本规则 (Project Identity)

## 1. 项目概述
- **项目名称**: AstralSolver（星灵求解器）
- **项目性质**: FFXIV（最终幻想14）Dalamud 智能战斗辅助插件

## 2. 语言与命名规范
- **自然语言**: 
  - 所有代码注释、日志消息 (Log messages)、Git Commit Message 必须使用**中文**。
- **代码命名**: 
  - 变量名、方法名、类名必须使用**英文**。
  - 公开成员 (Public/Protected)：使用 `PascalCase`（大驼峰式）。
  - 私有成员 (Private/Internal) 和局部变量：使用 `camelCase`（小驼峰式）。
  - 命名空间 (Namespace)：所有代码的命名空间必须以 `AstralSolver` 开头。

## 3. 技术栈
- **编程语言**: C# 12
- **目标框架**: .NET 8
- **核心框架**: Dalamud Plugin API
- **UI框架**: ImGui

## 4. 代码质量与约束
- **文档注释**: 每个公开 (Public) 类和方法必须有**中文 XML 文档注释**。
- **魔法数字**: 绝对禁止使用魔法数字。所有常量必须统一定义在 `Constants` 类中。
- **方法长度**: 单个方法的代码行数不得超过 **50 行**，超过则必须进行重构拆分。
