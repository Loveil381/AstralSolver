---
name: 构建测试
description: 构建项目并运行测试
---

# 工作流：构建测试

## 步骤描述
此工作流描述本项目的标准化持续验证流程：

1. **`dotnet build`**
   对 `AstralSolver` 以及 `AstralSolver.Tests` 执行编译构建，验证所有 C# 12 语法规范与引用依赖无误。
2. **`dotnet test`**
   跳转并执行 xUnit 单元测试套件，检查 Core 及 Jobs 层状态流转的有效性逻辑。
3. **报告结果**
   收集编译与测试的打印信息，对成功或失败进行总结分析并输出到讨论流。
