# IRON NEST MOD 原理学习笔记

> 基于对游戏安装目录（E:\steam\Iron Nest Heavy Turret Simulator）与开源参考 Mod [svr2kos2/IronNestFCS](https://github.com/svr2kos2/IronNestFCS)（114★，v1.0.6）源码的实地分析。

## 一、MelonLoader 如何加载 MOD

```
游戏启动 → Windows 加载 version.dll（代理 DLL）→ 拉起 .NET 运行时（net6）
        → 首次启动用 Cpp2IL 从 GameAssembly.dll 生成 Il2CppAssemblies/（互操作托管程序集）
        → 扫描 Mods/*.dll → 按 [assembly: MelonInfo] 找到入口类 → 实例化并调用生命周期回调
```

| 目录/文件 | 作用 |
|---|---|
| `version.dll` | 代理 DLL，游戏启动入口（MelonLoader 的"钩子"） |
| `MelonLoader/net6/` | .NET 6 运行时：MelonLoader.dll（API）、Il2CppInterop.*（互操作）、0Harmony.dll（补丁）、MonoMod、Tomlet |
| `MelonLoader/Il2CppAssemblies/` | **Cpp2IL 生成的游戏互操作程序集**（含 Assembly-CSharp → `Il2Cpp` 命名空间、UnityEngine.*、Unity.InputSystem）——写 MOD 必须引用它们。**本机尚未生成（游戏未启动过），首次运行游戏自动生成** |
| `Mods/` | MelonLoader 自动加载的 MOD（每个 dll 一个） |
| `Plugins/` | 与游戏同生命周期的插件（同样自动加载） |
| `UserLibs/` | MOD 的依赖库（放这里**不会**被误当 MOD 加载） |
| `UserData/<Mod名>/` | MOD 自己的数据目录（`MelonEnvironment.UserDataDirectory` 获取） |
| `MelonLoader/Logs/` | 日志（`yy-M-d_h-m-s.log` 格式），调试主要靠它 + `MelonLogger` |

## 二、MOD 程序集骨架（MelonMod）

```csharp
[assembly: MelonInfo(typeof(FcsHostMod), "IronNestFCS", "1.0.6", "svr2kos2")]
[assembly: MelonGame("Iron Nest", "Iron Nest: Heavy Turret Simulator")]

public class FcsHostMod : MelonMod
{
    public override void OnInitializeMelon() { }        // 初始化（读配置、加载子程序集）
    public override void OnSceneWasLoaded(int buildIndex, string sceneName) { }  // 场景加载
    public override void OnUpdate() { }                  // 每帧（读 F9 等输入、驱动逻辑）
    public override void OnGUI() { }                     // 每帧 IMGUI 绘制
    public override void OnDeinitializeMelon() { }       // 卸载（撤补丁、清引用）
}
```

- 生命周期回调是 MelonLoader 在正确时机自动调用的，Mod 只覆写需要的方法。
- 输入：游戏用**新 Input System**，`UnityEngine.Input` 会抛异常 → 用 `Keyboard.current.f9Key.wasPressedThisFrame`。
- 协程：`MelonCoroutines.Start(IEnumerator)` / `MelonCoroutines.Stop(handle)`，Unity 主线程分帧驱动。
- 日志：`MelonLogger.Msg/Warning/Error`，写入 `MelonLoader/Logs/`。

## 三、构建与引用（csproj 关键点）

```xml
<TargetFramework>net6.0</TargetFramework>   <!-- 必须 net6，匹配 MelonLoader 运行时 -->
<GameDir>游戏根目录</GameDir>
<Reference Include="MelonLoader"><HintPath>$(GameDir)\MelonLoader\net6\MelonLoader.dll</HintPath><Private>false</Private></Reference>
<Reference Include="Il2CppInterop.Runtime">…\Il2CppInterop.Runtime.dll …</Reference>
<Reference Include="0Harmony">…\0Harmony.dll …</Reference>
<Reference Include="Il2Cppmscorlib">…\MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll …</Reference>
<Reference Include="UnityEngine.CoreModule">…\Il2CppAssemblies\UnityEngine.CoreModule.dll …</Reference>
<Reference Include="Unity.InputSystem">…\Il2CppAssemblies\Unity.InputSystem.dll …</Reference>
```

- **`Private=false` 必须**：运行时程序集由 MelonLoader 进程提供，复制进 `Mods/` 会与进程内已加载版本冲突。
- 引用游戏内部类：`using Il2Cpp;`（= Assembly-CSharp 的互操作版本）+ 各 UnityEngine 模块 + `Il2CppTMPro`。
- 依赖库（如 TagLibSharp/CSCore）输出到 `UserLibs/`，构建后手动拷或 csproj 指定输出。

## 四、与游戏交互的三种方式

### 1. 查找游戏对象（绑定）
```csharp
var turret = GameObject.Find("Player Turret Piece");       // 按名称
var console = gunSystem.transform.Find("--Reloading Console"); // 子对象
var fireMission = root.GetComponent<FireMission>();          // 游戏组件
var tmp = t.GetComponentInChildren<Il2CppTMPro.TextMeshPro>(); // 读文字解析编号
```

### 2. 直接调用游戏内部 API（Il2Cpp 类成员）
```csharp
gunController.CanFire; gunController.CurrentElevation;      // 游戏属性
button.OnClickDown(); button.OnClickUp();                   // 模拟点击游戏按钮（LookAtTarget）
elevationLever.SetSliderValue(elevation);                   // 操作滑杆
recordItem.tracks = new Il2CppReferenceArray<AudioClip>(...); // 写游戏数据
```

### 3. 创建自己的对象（3D 世界内 UI/道具）
```csharp
var button = GameObject.CreatePrimitive(PrimitiveType.Cube); // 可点击立方体按钮
var tmp = go.AddComponent<TextMeshPro>();                    // 3D 文本（World Space）
var disk = Object.Instantiate(src);                          // 克隆游戏对象
AudioClip.Create(..., true, reader, setPos);                 // 流式音轨
```
- 自建 UI 用**自定义射线点击**（Physics.Raycast 检测 collider），不依赖游戏 UI、不注册新 IL2CPP 类型。
- URP 渲染管线：CreatePrimitive 默认 Standard 材质会渲染成紫色 → 用 `Shader.Find("Universal Render Pipeline/Unlit")` 重建材质（`_BaseColor`）。

## 五、IL2CPP 互操作铁律（违反 = 崩溃）

1. **必须主线程**：所有 IL2CPP 对象访问用 `MelonCoroutines` 协程（yield 期间不阻塞、恢复后仍主线程）。**绝不能用 async/Task.Delay**——continuation 在线程池恢复，跨线程访问 IL2CPP → 进程崩溃且无日志。
2. **不能注册新 IL2CPP 类型**：进程内同一类型只能注册一次（热重载时旧类型残留会崩）——用 Il2CppInterop 已有类型或非托管回调。
3. **回调必须保活**：托管回调委托（如 AudioClip.PCMReaderCallback）被 GC 回收后触发 = 野指针 → Mod 实例长期持有。
4. **IL2CPP 裁剪**：部分 Unity API 的 Span 重载被裁掉（`AudioClip.SetData` → MissingMethodException）→ 用无 Span 依赖的 API（`AudioClip.Create` 的 PCMReaderCallback 重载是纯 il2cpp_runtime_invoke）。
5. **材质不要写 MaterialPropertyBlock**：renderer 级会覆盖全部材质槽位（"双黑圈"根因）→ 逐槽位 `new Material(原)` 替换后赋回 `renderer.materials`。

## 六、UI 实现

### IMGUI 面板（HUD 窗口）
- **关键坑**：MelonLoader IL2CPP 下 `OnGUI` 每帧只触发一次，无 Layout/event 多 pass → GUILayout 的 controlID 错位（表现为"只有第一个按钮能点"）。
- **解法**：只用绝对 Rect 的 `GUI.Box/GUI.Label/GUI.Button`，不走布局系统，不套 GUI.Window。

### 3D 世界 UI
- Cube + TextMeshPro + ClickRaycaster（Physics.Raycast），位置/缩放直接设 transform。

## 七、热重载架构（IronNestFCS 的精华，可借鉴）

| 程序集 | 角色 |
|---|---|
| `IronNestFCS`（Mods/） | 宿主 Mod，永不重载：首次加载 Logic、监听 F9、转发生命周期 |
| `IronNestFCS.Abstractions`（UserLibs/） | 仅 `IFcsModule` 接口，唯一可跨 ALC 传递的类型 |
| `IronNestFCS.Logic`（UserData/） | 全部火控逻辑，装进 `isCollectible` 的 ALC，从内存字节加载（不锁盘），F9 重载 |

重载流程：`Shutdown()`（停全部协程、`_harmony.UnpatchSelf()`、清 IL2CPP 引用）→ `alc.Unload()` → GC 回收 → 重新 `LoadFromStream`。逻辑代码更新后重新 build（dll 直接输出到 UserData/），游戏内按 F9 即生效——**写 MOD 时强烈建议采用此架构**（开发效率倍增）。

## 八、游戏数据速查（从源码反推）

- **场景对象名**：`Player Turret Piece`（炮塔）、`Draggable Surface`（地图）、`MapToken_Artillery`（目标标记，TextMeshPro 文本=编号）、`Fire Mission Root`（FireMission 组件）、`Gun System Left/Right` → `--Reloading Console`（`Universal Button *` 系列按钮、`PowderChargeController`、`CylinderShellSelector`、`OdometerDisplay`）、`GunLeft/GunRight`（GunController）、`.Elevation Lever Left/Right`（LinearSliderInteractable）、`Shell ID Left/Right`。
- **弹种枚举**：AP/APHE/ATMC/CLMN/CYAN/DRIL/EQKE/FLCH/HCHE/HE/INCN/LE/PLCM/PHGN/PRPG/SMK/STAR/TEAR/THRM/WP（20 种）。
- **地图坐标**：目标距离 = `localPosition.magnitude × 3.8164`，角度 = `Vector3.SignedAngle(target, Vector3.up, Vector3.forward)`（负值 +360），世界位置 = `localPosition × 3.8164 + (10.016, 5.235, 0)`。
- **唱片机**：`RecordItem`（tracks/loop/displayName）、`RecordDisk`（mesh 材质槽位：mat[0]=VinylRecord 黑胶、mat[1]=CoverArt 封面、mat[2/3]=描边）。
- **游戏名匹配**：`[assembly: MelonGame("Iron Nest", "Iron Nest: Heavy Turret Simulator")]`。

## 九、开发环境现状与第一步

- 本机 MelonLoader v0.7.3 已装好，Mods 已有 IronNestFCS.dll / CustomRecords.dll（成品）。
- **Il2CppAssemblies 尚未生成**（游戏从未以 MelonLoader 启动过，MelonLoader/Logs 为空）→ **开发 MOD 的第一步：启动一次游戏**，等 MelonLoader 生成 `MelonLoader/Il2CppAssemblies/`（Cpp2IL 反编译 GameAssembly.dll），之后 csproj 才能引用游戏内部类。
- 参考 Mod 源码已克隆到本地可随时对照：`svr2kos2/IronNestFCS`（宿主+Logic+Abstractions+CustomRecords 四程序集架构）。
