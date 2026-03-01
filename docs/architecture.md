# SPH 2D 流体模拟 — 架构与技术分析

## 1. 项目概述

本项目是一个基于 **SPH (Smoothed Particle Hydrodynamics)** 方法的 2D 流体模拟系统，运行在 Unity URP (Universal Render Pipeline) 上。核心物理计算完全在 GPU 上通过 Compute Shader 执行，利用空间哈希网格 + Bitonic 排序实现邻域搜索加速。

---

## 2. 架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          Unity URP 渲染管线                              │
│                                                                         │
│  ┌──────────────────────┐    注入Pass    ┌──────────────────────────┐   │
│  │ ScriptableRenderer   │ ◄──────────── │ FluidRenderFeature       │   │
│  │ (URP内置)             │               │ (ScriptableRendererFeature)│  │
│  └──────────────────────┘               └──────────┬───────────────┘   │
│                                                     │ 创建 & 管理       │
│                                                     ▼                   │
│                                          ┌──────────────────────┐      │
│                                          │  DensityFieldPass    │      │
│                                          │  (ScriptableRenderPass)│     │
│                                          └──────────┬───────────┘      │
└─────────────────────────────────────────────────────┼──────────────────┘
                                                      │
                    ┌─────────────────────────────────┼──────────────────────┐
                    │           每帧 Execute 流程      │                      │
                    │                                  ▼                      │
                    │  ┌─────────────────────────────────────────────────┐   │
                    │  │ 阶段1: 空间哈希排序 (Spatial Hash Sort)          │   │
                    │  │  ├─ FluidBuildGridCS  → 为每个粒子计算网格ID      │   │
                    │  │  ├─ BitonicSort       → GPU并行排序(按网格ID)     │   │
                    │  │  └─ SortGridCS        → 构建网格起始索引表        │   │
                    │  └─────────────────────────┬───────────────────────┘   │
                    │                            ▼                           │
                    │  ┌─────────────────────────────────────────────────┐   │
                    │  │ 阶段2: SPH 物理计算                              │   │
                    │  │  ├─ FluidSimulationDensityCS   → 密度场计算      │   │
                    │  │  ├─ FluidSimulationPressureCS  → 压力场计算      │   │
                    │  │  └─ FluidSimulationCS          → 速度/位置积分   │   │
                    │  └─────────────────────────┬───────────────────────┘   │
                    │                            ▼                           │
                    │  ┌─────────────────────────────────────────────────┐   │
                    │  │ 阶段3: 可视化渲染                                │   │
                    │  │  ├─ DrawGridDensity  (全屏密度场热力图)           │   │
                    │  │  ├─ VizDensity       (密度偏差着色)              │   │
                    │  │  ├─ DrawGridPressure (全屏压力场)                │   │
                    │  │  └─ DrawParticles    (GPU Instancing粒子绘制)    │   │
                    │  └─────────────────────────────────────────────────┘   │
                    └────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                          CPU 端架构                                      │
│                                                                         │
│  ┌────────────────────────┐                                             │
│  │ FluidSimulatorController│ ◄── MonoBehaviour 入口                     │
│  │  ├─ 用户输入处理         │     (鼠标点击生成粒子)                      │
│  │  ├─ Inspector参数同步    │     (OnValidate → RenderFeature)           │
│  │  └─ 生命周期管理         │     (Awake/OnDestroy)                      │
│  └───────────┬────────────┘                                             │
│              │ 调用                                                      │
│              ▼                                                           │
│  ┌────────────────────────┐     ┌──────────────────┐                    │
│  │ ParticleSpawner        │────▶│ FluidState       │                    │
│  │  ├─ Add / AddMultiple  │     │  (全局粒子计数)   │                    │
│  │  ├─ FillScreen*        │     └────────┬─────────┘                    │
│  │  ├─ AddAroundCursor    │              │ 触发                          │
│  │  └─ Clean              │              ▼                               │
│  └────────────────────────┘     GPU缓冲区同步                            │
│                                 (InitCS Dispatch)                        │
└─────────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────────┐
│                       GPU 缓冲区布局                                     │
│                                                                         │
│  _initBuffer (staging)  ──InitCS──▶  _physicsBuffer (主物理数据)         │
│                                       ↕ Compute Shader 读写             │
│  _gridSortedBuffer      ◄──BitonicSort──▶  _gridSortedTempBuffer       │
│  _gridBuffer (网格起始索引)  ◄── SortGridCS                              │
│  _argsBuffer (间接绘制参数)  ◄── SyncParticleCount                       │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 3. 核心模块说明

### 3.1 Core 层 — 基础定义

| 文件 | 职责 |
|------|------|
| `FluidConstants.cs` | 全局常量 (最大粒子数、线程组大小、时间步长等) |
| `FluidParticleData.cs` | GPU/CPU共享的粒子数据结构定义，与HLSL端 `FluidParticle.hlsl` 一一对应 |
| `FluidState.cs` | 全局粒子计数管理，变更时自动触发GPU缓冲区同步 |
| `MeshUtility.cs` | 生成GPU Instancing所需的Quad网格 |

### 3.2 Simulation 层 — 模拟控制

| 文件 | 职责 |
|------|------|
| `FluidSimulatorController.cs` | MonoBehaviour入口，处理输入、同步参数、管理生命周期 |
| `ParticleSpawner.cs` | 粒子生成器，支持单个/批量/随机/区域填充等多种生成模式 |

### 3.3 Rendering 层 — URP渲染集成

| 文件 | 职责 |
|------|------|
| `FluidRenderFeature.cs` | URP ScriptableRendererFeature，包含核心 `DensityFieldPass` |

`DensityFieldPass` 是整个系统的核心，每帧执行:
1. 空间哈希网格构建 + Bitonic排序
2. SPH密度/压力Compute Shader计算
3. 可视化渲染 (密度场/压力场/粒子)

### 3.4 Sorting 层 — GPU排序

| 文件 | 职责 |
|------|------|
| `BitonicSort.cs` | GPU Bitonic Merge Sort，用于空间哈希网格的粒子排序 |

### 3.5 GPU Shader 层

| 文件 | 职责 |
|------|------|
| `FluidParticle.hlsl` | 共享数据结构 + SPH核函数 (SmoothingKernel, 压力转换等) |
| `FluidParticlesCS.compute` | 7个Compute Kernel: Init, BuildGrid, SortGrid, Density, Pressure, Update, GraphUpdate |
| `BitonicSortCS.compute` | Bitonic排序 + 矩阵转置 Compute Shader |
| `DrawParticles.shader` | GPU Instancing粒子渲染 (圆形粒子 + 速度着色) |
| `DrawGridDensity.shader` | 全屏密度场可视化 (基于空间哈希邻域搜索) |
| `DrawGridPressure.shader` | 全屏压力场可视化 |
| `VizDensity.shader` | 密度偏差着色 (高于/低于/接近目标密度的三色映射) |
| `DrawDensity.shader` | 逐粒子密度贡献渲染 (Additive Blend) |
| `DrawPressure.shader` | 逐粒子压力贡献渲染 |
| `DrawGradient.shader` | 密度梯度场可视化 |

---

## 4. SPH 算法实现细节

### 4.1 核函数 (Smoothing Kernel)

采用二次多项式核函数:

```
W(r, h) = (h - r)² / (π * h⁴ / 6)    当 r < h
W(r, h) = 0                            当 r ≥ h
```

导数:
```
W'(r, h) = 12(r - h) / (π * h⁴)
```

其中 `h` 为光滑半径 (smoothing radius)，`r` 为粒子间距离。

### 4.2 密度计算

对每个粒子 i，遍历其所在网格及相邻8个网格中的所有粒子 j:

```
ρᵢ = Σⱼ mⱼ · W(|xᵢ - xⱼ|, h)
```

### 4.3 压力计算

使用状态方程将密度转换为压力:
```
P = (ρ - ρ₀) · k
```
其中 `ρ₀` 为目标密度，`k` 为压力系数。

压力梯度力 (对称化处理):
```
Fᵢ = -Σⱼ mⱼ · (Pᵢ + Pⱼ) / (2ρᵢ) · ∇W(|xᵢ - xⱼ|, h)
```

### 4.4 时间积分

采用显式欧拉积分:
```
vᵢ(t+Δt) = vᵢ(t) + aᵢ · Δt
xᵢ(t+Δt) = xᵢ(t) + vᵢ(t+Δt) · Δt
```

固定时间步长 Δt = 0.005s，配合能量衰减系数 (energy damping) 保持稳定性。

### 4.5 边界处理

简单的反射边界:
- 当预测位置超出 [-0.98, 0.98] 范围时，位置钳制到边界
- 速度分量反转并乘以衰减系数

---

## 5. 空间加速结构

### 5.1 空间哈希网格

将 [-1, 1] 的模拟空间划分为均匀网格，网格大小等于光滑半径 `h`:
- `xCount = floor((texelWidth / texelHeight) / h)`
- `yCount = floor(1 / h)`
- `gridID = gridX + gridY * xCount`

### 5.2 排序 + 索引构建

1. **BuildGridCS**: 为每个粒子计算 `(gridID, particleIndex)` 对
2. **BitonicSort**: 按 `gridID` 对所有粒子进行GPU并行排序
3. **SortGridCS**: 扫描排序后的数组，记录每个网格的起始索引

邻域搜索时只需遍历当前网格及周围8个网格的粒子，复杂度从 O(n²) 降至 O(n·k)，其中 k 为平均邻域粒子数。

### 5.3 Bitonic排序

经典的 Bitonic Merge Sort 算法:
- 块内排序使用 shared memory (BITONIC_BLOCK_SIZE = 512)
- 跨块排序通过矩阵转置技巧实现
- 总排序复杂度 O(n · log²n)，完全在GPU上并行执行

---

## 6. 渲染管线集成

### 6.1 数据流

```
CPU (ParticleSpawner)
  │ BeginWrite/EndWrite
  ▼
_initBuffer (staging, SubUpdates模式)
  │ InitCS Dispatch
  ▼
_physicsBuffer (GPU主缓冲区)
  │ Compute Shader 读写
  ▼
DrawParticles (GPU Instancing, 从_physicsBuffer读取位置)
```

### 6.2 双缓冲策略

- `_initBuffer`: CPU可写的staging buffer (ComputeBufferMode.SubUpdates)
- `_physicsBuffer`: GPU专用的物理数据buffer
- 通过 `InitCS` kernel 将新粒子数据从staging拷贝到physics buffer
- 写入期间 (`_isWriting = true`) 跳过渲染Pass，避免竞争

### 6.3 间接绘制

使用 `DrawMeshInstancedIndirect` 进行粒子渲染:
- `_argsBuffer` 存储间接绘制参数
- 粒子数量变化时通过 `SyncParticleCount` 更新 args[1]
- 顶点着色器从 `_physicsBuffer` 读取每个实例的位置

---

## 7. 重构后的目录结构

```
Assets/Scripts/
├── Core/                           # 基础层
│   ├── FluidConstants.cs           # 全局常量
│   ├── FluidParticleData.cs        # 数据结构定义
│   ├── FluidState.cs               # 全局状态管理
│   └── MeshUtility.cs              # 网格工具
├── Simulation/                     # 模拟层
│   ├── FluidSimulatorController.cs # MonoBehaviour入口
│   └── ParticleSpawner.cs          # 粒子生成器
├── Rendering/                      # 渲染层
│   └── FluidRenderFeature.cs       # URP渲染特性 + DensityFieldPass
└── Sorting/                        # 排序层
    └── BitonicSort.cs              # GPU Bitonic排序

Assets/Shaders/
├── ComputeShader/
│   ├── FluidParticle.hlsl          # 共享结构体 + SPH核函数
│   ├── FluidParticlesCS.compute    # 物理计算Compute Shader
│   └── BitonicSortCS.compute       # 排序Compute Shader
├── DrawParticles.shader            # 粒子渲染
├── DrawGridDensity.shader          # 密度场可视化
├── DrawGridPressure.shader         # 压力场可视化
├── VizDensity.shader               # 密度偏差着色
├── DrawDensity.shader              # 逐粒子密度贡献
├── DrawPressure.shader             # 逐粒子压力贡献
└── DrawGradient.shader             # 梯度场可视化
```

---

## 8. 性能特征

| 指标 | 值 |
|------|-----|
| 最大粒子数 | 131,072 (2¹⁷) |
| Compute线程组大小 | 64 |
| Bitonic排序块大小 | 512 |
| 时间步长 | 固定 0.005s |
| 邻域搜索 | 3×3 网格 (当前格 + 8邻居) |
| 渲染方式 | GPU Instancing (DrawMeshInstancedIndirect) |
| 缓冲区模式 | SubUpdates (零拷贝CPU→GPU传输) |

---

## 9. 已知限制与改进方向

1. **固定时间步长**: 当前使用固定 Δt=0.005s，未与Unity的Time.deltaTime同步，可能导致不同帧率下行为不一致
2. **简单边界**: 仅支持矩形反射边界，不支持任意形状障碍物
3. **单一核函数**: 密度和压力使用相同的核函数，可考虑使用Spiky kernel计算压力梯度以获得更好的不可压缩性
4. **无粘性力**: 当前未实现粘性项，流体表现偏"滑"
5. **CPU端粒子生成**: 粒子创建在CPU端完成后传输到GPU，大量生成时可能成为瓶颈
6. **排序缓冲区固定大小**: Bitonic排序要求缓冲区大小为2的幂次，始终排序131072个元素，即使实际粒子数远小于此
