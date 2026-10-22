# ACL

> **ACL** 是一个**完全自研**的 Minecraft（Java 版）启动器工程。
> 目标：从零构建一个现代、跨平台、内核与界面彻底解耦的启动器，**不基于任何现有启动器派生**。
>
> 状态：**设计 / 文档阶段**（本仓库当前只有文档，尚无代码）。

## 这是什么

- **自研**：内核、服务、界面全部自行实现，不复制、不派生任何第三方启动器代码。
- **净室（Clean-room）**：仅参考 **公开协议与格式规范**（Mojang / 加载器 / 资源站 / 认证服务）以及竞品的**外部行为**，独立实现。
- **参考对象**：同目录下的 `D:\project\APCL`（`AliCraftLauncher`，基于 PCL2 的二次开发）**仅作为架构研究的镜子**——学习其分层得失，但不沿用其代码与命名。研究结论见 [docs/10](docs/10-参考实现研究-PCL与ACL架构剖析.md)。

## 技术栈（拟定）

| 层 | 选型 |
| --- | --- |
| 语言 / 运行时 | **C# + .NET 10 (LTS)** |
| 界面 | **Avalonia 12**（跨平台，XAML + MVVM）；Windows 可选 WPF 后端 |
| 内核 | 纯 .NET 类库，**不引用任何 UI 框架** |
| 序列化 | System.Text.Json（首选）/ Newtonsoft.Json（兼容外部格式时） |
| 测试 | xUnit + 桩服务集成测试 |

## 仓库布局（拟定）

```
ACL/
├─ docs/            设计文档（本目录）
├─ src/             源码（待建）
│  ├─ ACL.Core.*        内核（Abstractions / Domain / 各服务）
│  ├─ ACL.Infrastructure 基础设施实现
│  ├─ ACL.Platform.*     平台适配
│  ├─ ACL.App            Avalonia 界面
│  └─ ACL.Cli            命令行宿主
└─ tests/           测试（待建）
```

## 文档

请从 [docs/README.md](docs/README.md)（文档索引）开始。

## 许可与合规

- 本项目为**净室自研**，不包含第三方启动器源码。
- 对外部规范、数据格式、灵感来源的署名与合规说明见 [docs/09](docs/09-安全隐私与合规.md)。