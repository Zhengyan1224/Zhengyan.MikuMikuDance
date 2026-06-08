# Zhengyan.MikuMikuDance

Zhengyan.MikuMikuDance 是一个使用 C#、.NET 10 和 Silk.NET OpenGL 构建的跨平台 MMD 编辑器与运行时项目。项目目标是在 Windows、Linux 和 macOS 上提供模型/动作格式处理、动画采样、OpenGL 预览渲染、MME 效果运行时，以及后续完整的可视化编辑器能力。

## 特性

- C# / .NET 10 代码库，所有项目命名空间以 `Zhengyan.MikuMikuDance` 为前缀
- Silk.NET OpenGL/GLFW 跨平台预览窗口
- PMD/PMX 模型读取与写入
- VMD/NMD 动作读取与写入
- `.zmm` / `.nma` 项目文件读取与写入
- PMMv1/PMMv2 项目导入与 PMMv2 导出
- ASCII DirectX `.x` 附件读取与写入
- MME `.fx` 参数、技术、pass、脚本、render target 和 offscreen metadata 解析
- 基础 HLSL 到 GLSL 翻译和 OpenGL effect shader program cache
- CPU skinning、Morph 评估、CCD IK、相机/灯光/可见性采样
- OpenGL 模型/附件预览、纹理、toon、sphere、edge、透明排序和 ground shadow
- 可撤销编辑命令基础：undo/redo、批量命令、motion/model snapshot、关键帧增删改、时间轴插入/删除、复制/粘贴/镜像粘贴

## 技术栈

- .NET 10
- C# preview
- Silk.NET OpenGL
- Silk.NET GLFW Windowing
- xUnit

## 环境要求

- .NET 10 SDK
- 支持 OpenGL 3.3 的显卡和驱动
- Windows、Linux 或 macOS

## 快速开始

克隆仓库后，在项目根目录执行：

```powershell
dotnet restore .\Zhengyan.MikuMikuDance.slnx
dotnet build .\Zhengyan.MikuMikuDance.slnx
dotnet test .\Zhengyan.MikuMikuDance.Tests\Zhengyan.MikuMikuDance.Tests.csproj
```

启动空白预览窗口：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --preview
```

启动 ImGui 编辑器壳：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --editor
```

打开项目进入编辑器壳：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --editor path\to\scene.zmm
```

预览模型：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --preview path\to\model.pmx
```

预览模型和动作：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --preview path\to\model.pmx path\to\motion.vmd
```

## 命令行

查看帮助：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --help
```

查看功能目录：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --features
```

检查文件内容：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\model.pmd
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\model.pmx
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\motion.vmd
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\motion.nmd
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\accessory.x
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\effect.fx
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\scene.zmm
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\scene.nma
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --inspect path\to\scene.pmm
```

导出 PMM：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --export-pmm path\to\scene.zmm path\to\scene.pmm
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --export-pmm path\to\scene.nma path\to\scene.pmm
```

计算指定帧姿态：

```powershell
dotnet run --project .\Zhengyan.MikuMikuDance.App -- --pose path\to\model.pmx path\to\motion.vmd 120
```

## 项目结构

```text
Zhengyan.MikuMikuDance.Core
  领域模型、动画、场景状态、姿态评估、编辑命令

Zhengyan.MikuMikuDance.Formats
  PMD/PMX/VMD/NMD/.x/.fx/.zmm/.nma/.pmm 格式读取与写入

Zhengyan.MikuMikuDance.Rendering
  渲染中立的数据结构、MME runtime metadata、shader 翻译、render mesh 构建

Zhengyan.MikuMikuDance.Rendering.OpenGL
  Silk.NET OpenGL 渲染器、纹理缓存、effect program cache、render target 管理

Zhengyan.MikuMikuDance.UI.ImGui
  ImGui.NET 编辑器壳、Silk.NET OpenGL/GLFW UI host、基础菜单、面板、偏好持久化、视口背景图片/视频占位层、视口网格和 pointed debug overlay、视口拾取选择、模型/附件/骨骼/Morph 活动对象选择、selection/pointed overlay、相机导航、Morph 滑块编辑、附件/相机/模型 outside parent 绑定 UI、draw order/transform order 编辑和基础快捷键命令路由

Zhengyan.MikuMikuDance.App
  命令行入口和预览窗口入口

Zhengyan.MikuMikuDance.Tests
  单元测试和兼容性测试
```

## 支持格式

| 类型 | 扩展名 | 当前能力 |
| --- | --- | --- |
| 模型 | `.pmd`, `.pmx` | 读取、写入、预览、姿态评估 |
| 动作 | `.vmd`, `.nmd` | 读取、写入、采样 |
| 附件 | `.x` | ASCII 读取、写入、预览 |
| 效果 | `.fx` | 结构解析、基础运行时 metadata、部分 OpenGL 执行 |
| 项目 | `.zmm`, `.nma`, `.pmm` | 读取、写入或导出，按格式能力不同逐步补全 |

## 开发状态

当前版本重点在核心格式、动画运行时、OpenGL 预览和编辑命令基础。完整桌面编辑器 UI、物理系统、插件宿主、音视频导出、模型编辑器和更完整的 MME 效果执行仍在开发计划中。

详细任务列表见 [待办内容.md](./待办内容.md)。后续每完成一项功能，会同步更新该文档。

## 开发建议

- 新功能优先放在对应的核心项目中，避免 UI 与格式/运行时逻辑耦合
- 格式解析和编辑命令需要补充单元测试
- OpenGL 相关功能应保留无图形上下文的核心测试入口
- 需要新增跨平台依赖时，优先确认 Windows、Linux 和 macOS 都能构建运行
