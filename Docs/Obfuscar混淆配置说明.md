# Obfuscar 混淆配置说明

## 官方网站

- 官网首页：<https://docs.obfuscar.com/>
- 配置文档：<https://docs.obfuscar.com/getting-started/configuration.html>

## 当前项目建议

当前项目为 .NET 6 WPF，且使用了：

- Autofac
- Castle DynamicProxy
- CommunityToolkit.Mvvm
- XAML 绑定
- 反射
- 异步状态机

因此建议使用偏保守的混淆策略：

- 保留 public API
- 混淆非 public API（private / protected / internal）
- 不混淆 ViewModels、UserControls、Converters、Interceptors 等高风险区域
- 不使用 Unicode 混淆名
- 不启用字符串隐藏

这样可以在尽量保证程序可运行的前提下，对大部分内部实现进行混淆。

## 推荐配置

```xml
<?xml version="1.0" encoding="utf-8" ?>
<Obfuscator>
    <Var name="ProjectDir" value="D:\项目\MeasurementSoftware" />
    <Var name="InPath" value="$(ProjectDir)\bin\Release\net6.0-windows7.0" />
    <Var name="OutPath" value="$(ProjectDir)\obfuscar_out\net6.0-windows7.0" />
    <Var name="KeepPublicApi" value="true" />
    <Var name="HidePrivateApi" value="true" />
    <Var name="ReuseNames" value="false" />
    <Var name="UseUnicodeNames" value="false" />
    <Var name="HideStrings" value="false" />
    <Var name="RenameProperties" value="false" />
    <Var name="RenameEvents" value="false" />
    <Var name="SkipGenerated" value="true" />
    <Var name="SkipSpecialName" value="true" />
    <AssemblySearchPath path="$(InPath)" />
    <AssemblySearchPath path="C:\Program Files\dotnet\shared\Microsoft.NETCore.App\6.0.36" />
    <AssemblySearchPath path="C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\6.0.36" />
    <AssemblySearchPath path="C:\Program Files\dotnet\packs\Microsoft.WindowsDesktop.App.Ref\6.0.36\ref\net6.0" />
    <AssemblySearchPath path="C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\6.0.36\ref\net6.0" />
    <AssemblySearchPath path="C:\Program Files\dotnet\packs\Microsoft.AspNetCore.App.Ref\6.0.36\ref\net6.0" />
    <Module file="$(InPath)\MultiProtocol.dll" />
    <Module file="$(InPath)\SF-GAMS通用自动测量系统.dll">
        <SkipNamespace name="MeasurementSoftware.UserControls*" />
        <SkipNamespace name="MeasurementSoftware.Converters*" />
        <SkipNamespace name="MeasurementSoftware.Interceptors*" />
        <SkipNamespace name="MeasurementSoftware.ViewModels*" />
        <SkipType name="MeasurementSoftware.App" skipFields="true" skipMethods="true" skipProperties="true" skipEvents="true" />
        <SkipType name="MeasurementSoftware.MainWindow" skipFields="true" skipMethods="true" skipProperties="true" skipEvents="true" />
        <SkipType name="MeasurementSoftware.RegistrationWindow" skipFields="true" skipMethods="true" skipProperties="true" skipEvents="true" />
    </Module>
</Obfuscator>
```

## 配置含义

### 1. KeepPublicApi=true

保留 public API 名称，避免：

- WPF/XAML 加载失败
- 依赖注入失效
- 反射找不到成员
- 动态代理异常

### 2. HidePrivateApi=true

混淆非 public API，通常包括：

- private
- protected
- internal
- protected internal

### 3. ReuseNames=false

不复用混淆名，降低可读性。

### 4. UseUnicodeNames=false

禁用 Unicode 混淆名，提升兼容性，避免 CLR / 反射 / 代理相关异常。

### 5. HideStrings=false

关闭字符串隐藏，优先保证运行稳定。

### 6. RenameProperties=false

关闭属性重命名，避免 WPF 绑定、序列化和 MVVM 相关问题。

### 7. RenameEvents=false

关闭事件重命名，避免事件绑定与反射问题。

### 8. SkipGenerated=true

跳过编译器生成成员，例如：

- async 状态机
- lambda 闭包
- 匿名类型

### 9. SkipSpecialName=true

跳过特殊名称成员，例如：

- get_XXX
- set_XXX
- add_XXX
- remove_XXX

## 使用说明

不要直接运行 `obfuscar_out` 目录。

正确做法：

1. 保留原始运行目录中的：
   - `.exe`
   - `.deps.json`
   - `.runtimeconfig.json`
   - 其他依赖 dll
   - `runtimes` 文件夹
2. 只替换混淆后的：
   - `SF-GAMS通用自动测量系统.dll`
   - `MultiProtocol.dll`

## 结论

这份配置属于“保守可运行”的混淆方案：

- 主程序稳定优先
- 大部分内部实现仍然会混淆
- private / protected / internal 会尽量混淆
- WPF / MVVM / DynamicProxy 高风险部分跳过
