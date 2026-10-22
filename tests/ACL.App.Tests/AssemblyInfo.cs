using Xunit;

// Avalonia 无头测试会共享全局 Application / 资源加载器状态：
// 同一程序集内的测试必须串行，否则并行创建多个测试会话会互相破坏（IAssetLoader 缺失）。
[assembly: CollectionBehavior(DisableTestParallelization = true)]