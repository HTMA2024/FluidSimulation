# SPH 2D Fluid Simulation

基于 **Smoothed Particle Hydrodynamics (SPH)** 方法的实时 2D 流体模拟，运行在 Unity URP 渲染管线上，核心物理计算完全由 GPU Compute Shader 驱动。

![Overview](images/overview.gif)

---

## 特性

- GPU 驱动的 SPH 流体模拟，支持最高 131,072 个粒子
- 空间哈希网格 + Bitonic 排序实现 O(n·k) 邻域搜索
- 实时密度场 / 压力场 / 粒子可视化
- 鼠标交互式粒子生成
- 完全集成 Unity URP ScriptableRendererFeature

## 效果展示

### 密度场可视化

![Density Field](images/density_field.gif)

### 压力场可视化

![Pressure Field](images/pressure_field.png)

## 技术架构

```
Assets/Scripts/
├── Core/                           # 基础层：常量、数据结构、状态管理
│   ├── FluidConstants.cs
│   ├── FluidParticleData.cs
│   ├── FluidState.cs
│   └── MeshUtility.cs
├── Simulation/                     # 模拟层：控制器、粒子生成
│   ├── FluidSimulatorController.cs
│   └── ParticleSpawner.cs
├── Rendering/                      # 渲染层：URP集成、渲染Pass
│   └── FluidRenderFeature.cs
└── Sorting/                        # 排序层：GPU Bitonic排序
    └── BitonicSort.cs

Assets/Shaders/
├── ComputeShader/
│   ├── FluidParticle.hlsl          # SPH核函数 + 共享数据结构
│   ├── FluidParticlesCS.compute    # 物理计算 (密度/压力/积分)
│   └── BitonicSortCS.compute       # Bitonic Merge Sort
├── DrawParticles.shader            # GPU Instancing 粒子渲染
├── DrawGridDensity.shader          # 全屏密度场
├── DrawGridPressure.shader         # 全屏压力场
└── VizDensity.shader               # 密度偏差着色
```

### 每帧渲染流程

```
空间哈希构建 → Bitonic排序 → 密度计算 → 压力计算 → 速度/位置积分 → 粒子绘制
```

## SPH 算法

采用二次多项式核函数，通过空间哈希网格将邻域搜索从 O(n²) 优化至 O(n·k)：

1. 为每个粒子计算所属网格ID
2. GPU Bitonic排序按网格ID排列粒子
3. 构建网格起始索引，实现 O(1) 网格查找
4. 遍历 3×3 邻域网格计算密度和压力
5. 显式欧拉积分更新速度和位置

## 环境要求

- Unity 2022.3+ (URP 14.x)
- 支持 Compute Shader 的 GPU (Shader Model 5.0)

## 使用方式

1. 用 Unity Hub 打开项目
2. 打开 `Assets/Scenes/SampleScene.unity`
3. 在 Inspector 中调整 `FluidSimulatorController` 的参数
4. 点击 Play，鼠标左键点击屏幕生成粒子

### Inspector 参数说明

| 参数 | 说明 |
|------|------|
| Particle Radius | 粒子渲染半径 |
| Smoothing Radius | SPH 光滑核半径，影响邻域搜索范围 |
| Target Density | 目标密度，粒子会趋向此密度分布 |
| Pressure Multiplier | 压力系数，值越大不可压缩性越强 |
| Energy Damping | 能量衰减，控制边界碰撞后的速度损失 |
| Gravity | 重力加速度 |
| Enable Update | 开启/暂停物理模拟 |
| Draw Particles | 显示粒子 |
| Draw Grid Density Field | 显示密度场热力图 |
| Draw Viz Density Map | 显示密度偏差着色 |

## License

[MIT](LICENSE)
