# 10 · 参考实现研究：既有启动器（PCL / ACL）架构剖析

> 目的：**学习**既有实现的架构与算法思想，为 ACL 自研提供参照；**只借鉴思想，不复制代码、不沿用命名**。
> 研究对象：本机 `D:\project\APCL`（`AliCraftLauncher`，基于开源启动器 **PCL2** 的二次开发）。以下为**架构事实**摘要。

## 1. 研究对象概况

- **性质**：Windows 桌面启动器（VB.NET + WPF 宿主 + C# 内核库），基于 **Plain Craft Launcher 2**。
- **解决方案**：4 个工程。
  - 宿主 `AliCraftLauncher`（VB.NET/WPF，.NET Framework 4.8；`RootNamespace=PCL`）。
  - 内核 `PCLCS`（C#，net48）——版本解析、Java、补丁释放、资源枚举。
  - 底座 `MeloongCore`（C#，netstandard2.0）+ `MeloongCore.Wpf`（net48）——配置/日志/进度/任务/文件/网络。
- **模块分层**：宿主层（`ModMain`/`ModSecret`/`Pages`/`Controls`）→ 业务模块层（`ModMinecraft`/`ModDownload`/`ModLaunch`/`ModJava`/`ModModpack`/`ModWatcher`/`ModCrash`/`ModNet`/…，全为 VB `Module`）→ 核心库层（`PCLCS` + `MeloongCore`）。

## 2. 值得学习的架构与算法

### 2.1 任务引擎（`ModLoader`）
- `LoaderBase` / `LoaderTask(Of TIn,TOut)` / `LoaderCombo(Of T)`：把启动、下载、索引拆成**带权重的任务树**，组合进度、支持取消与重跑判定。
- **借鉴点**：任务图 + 进度树 + 权重汇总 + 取消，是优秀的概念；ACL 用 `ITaskPipeline` + `ProgressNode` 重新实现（`async/Task`，去除 `Thread.Sleep` 轮询）。

### 2.2 下载引擎（`ModNet` / `ModDownload`）
- 多源（`NetSource`）+ 分片线程（`NetThread`）+ 文件任务（`NetFile`）+ 批次（`LoaderDownload`）+ 全局管理（`NetManagerClass`）。
- 断点续传（Range）、失败重试、不支持 Range 回退、校验、限速、端口/Ping 工具。
- **源策略**：官方 + 镜像有序候选，**并发送取先返回者**（`DlSourceLoader`）；URL 重写表（Assets/Libraries/Meta/Mod）。
- **借鉴点**：全部采纳为 `IDownloadService` + `IMirrorPolicy` + `IDownloadInterceptor`。

### 2.3 版本模型（`PCLCS.McVersion` / `MCUtils.CompareVersion`）
- 版本名比较：令牌切分 + 关键字权重（`rc/pre/snapshot/experimental`）。
- `Drop` 序数（`Major*10+Minor`）、加载器序数（`ForgelikeCode`）、OptiFine 序数（`OptiFineCode`）。
- 安装清单（`McInstallManifest`）：组件集合 + 受管文件（路径/哈希/写时间），支持**外部篡改检测**与**组件等价判定**。
- **借鉴点**：直接借鉴其**算法目标**（可比较、可分组、可校验），ACL 用 `McVersionSpec`/`ComponentSet`/`InstallManifest` 重新表达。

### 2.4 Java 探测（`PCLCS.Java`）
- 运行 `-XshowSettings:properties -version` 解析版本/位数/编码；32 位拒绝；重解析点与特殊路径筛除；排序与缓存。
- **借鉴点**：探测策略与筛除规则；ACL 用 `IJavaProbe`/`IJavaLocator` 抽象。

### 2.5 启动流水线（`ModLaunch`）
- 预检测 → 获取 Java → 登录 → 补全文件 →（内存优化）→ 参数 → natives → 预启动 → 自定义命令 → 启动进程 → 等待窗口 → 结束。
- 微软登录 6 步（OAuth→XBL→XSTS→MC→Profile）；第三方 `authlib-injector`；离线确定性 UUID。
- 参数生成 + 去重；natives 按 OS 过滤解压。
- **借鉴点**：流水线阶段划分与登录令牌链；ACL 用 `ILaunchPlanner`/`IProcessLauncher`/`IGameMonitor` 重做。

### 2.6 监控与诊断（`ModWatcher` / `ModCrash`）
- 进程状态机、窗口标题检测、日志解析、崩溃规则归因、报告导出。
- **借鉴点**：状态机 + 规则库思路 → `IGameMonitor` + `ICrashRule`。

### 2.7 通用底座（`MeloongCore`）
- 配置（强类型 + 分实例动态项）、日志（后台线程 + 可插拔行为）、分阶段进度、防抖/重跑 Worker、并发容器、文件/路径工具。
- **借鉴点**：这些能力在 ACL 中同样需要（`IConfigurationStore`/`IAppLogger`/`ProgressNode`/`ISystemClock` 等），但以**独立、无静态状态**的方式重做。

## 3. 应当规避的痛点（反模式）

| 痛点 | 现象 | ACL 的对策 |
| --- | --- | --- |
| 全局可变状态 | `McFolderSelected` / `McInstanceSelected` / `Settings` 到处直接访问 | 显式上下文 + 依赖注入（`IMinecraftContext`） |
| UI 副作用侵入核心 | 启动/下载流程里直接 `Hint`/`MyMsgBox`/`Frm*.*` | 内核只发事件/进度/结果，宿主渲染 |
| 静态配置 | `PCLCS.Configs` 静态字段 | 注入 `IConfigurationStore` |
| 无接口/难测试 | VB `Module` 无法有意义单测 | 接口 + DTO + 纯函数 + 单测 |
| 线程模型不统一 | `LoaderTask` 自带线程 + `Thread.Sleep` 轮询 UI | 统一 `Task`/`CancellationToken` + `Task.Delay` |
| 逻辑与界面同文件 | 125KB 的 `ModLaunch.vb` | 内核/应用/UI 分工程，限长与分文件 |
| 遗留命名 | 内部仍是 `PCL` 命名空间 | 新命名空间 `ACL.*`，全新命名 |
| 兼容包袱 | 旧 `.ini` 与 DES 加密 | 新 schema + 平台密钥链（可选迁移） |

## 4. "借思想不借代码"映射表

| 研究对象概念 | ACL 对应设计 | 说明 |
| --- | --- | --- |
| `LoaderBase/LoaderTask/LoaderCombo` | `ITaskPipeline` / `ITaskStage` / `ProgressNode` | 概念借鉴，接口与实现全新 |
| `ModNet` 下载引擎 | `IDownloadService` + `IMirrorPolicy` + `IDownloadInterceptor` | 重写，去除 VB/全局状态 |
| `DlSource*` 重写表 | `MirrorPolicy` 替换表 | 规则借鉴（公开协议） |
| `McVersion` / `CompareVersion` | `McVersionSpec` / `VersionName` | 算法目标借鉴，独立实现 |
| `McInstallManifest` | `InstallManifest` / `ManagedFile` | 数据模型借鉴（兼容外部格式） |
| `Java.CheckAsync` | `IJavaProbe` | 探测策略借鉴 |
| `McLaunch*` 流水线 | `LaunchOrchestrator` + `ILaunchPlanner` | 阶段划分借鉴 |
| `MsLoginStep1..6` | `AuthService`（设备码流） | 协议链借鉴（公开 OAuth/Xbox 规范） |
| `ModCrash` 规则 | `ICrashAnalyzer` + `ICrashRule` | 思路借鉴 |
| `MeloongCore` 底座 | `Infrastructure`（Config/Logger/Clock…） | 能力对等，实现独立 |

## 5. 净室声明

- 本文档仅为**架构与算法思想的书面研究记录**，不包含、不搬运研究对象源码。
- ACL 的实现需**独立编写**，命名、结构、接口均为自研设计；不得从研究对象粘贴代码。
- 参考范围限定为：**公开协议/格式规范**、**外部可观察行为**、以及本文档所总结的**高层设计思想**。

## 6. 主要外部资料来源（公开）

- Minecraft 官方/启动器元数据与资源分发协议（Mojang/launchermeta/piston）。
- 加载器官方元数据：Forge、NeoForge、Fabric、Quilt、LiteLoader、OptiFine。
- 资源站：CurseForge REST API、Modrinth API。
- 认证：Microsoft identity platform（OAuth 2.0 设备码）、Xbox Live、Minecraft Services；`authlib-injector` / Yggdrasil（第三方）。
- 社区镜像：BMCLAPI、MCIM。