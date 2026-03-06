[中文版本 (Chinese Version)](README_CN.md)

# SPH 2D Fluid Simulation

A real-time 2D fluid simulation based on Smoothed Particle Hydrodynamics (SPH), running on Unity URP with core physics computation fully driven by GPU Compute Shaders.

![overview](Images/overview.gif)

## Features

- GPU-driven SPH fluid simulation, supporting up to 131,072 particles
- Spatial hash grid + Bitonic sort for O(n·k) neighbor search
- Real-time density field / pressure field / particle visualization
- Interactive mouse-based particle spawning
- Fully integrated as a Unity URP `ScriptableRendererFeature`

## Showcase

### Density Field Visualization

![density](Images/density_field.gif)

### Pressure Field Visualization

![pressure](Images/pressure_field.gif)

## Technical Architecture

```
Assets/Scripts/
├── Core/                           # Foundation: constants, data structures, state
│   ├── FluidConstants.cs
│   ├── FluidParticleData.cs
│   ├── FluidState.cs
│   └── MeshUtility.cs
├── Simulation/                     # Simulation: controller, particle spawning
│   ├── FluidSimulatorController.cs
│   └── ParticleSpawner.cs
├── Rendering/                      # Rendering: URP integration, render passes
│   └── FluidRenderFeature.cs
└── Sorting/                        # Sorting: GPU Bitonic sort
    └── BitonicSort.cs

Assets/Shaders/
├── ComputeShader/
│   ├── FluidParticle.hlsl          # SPH kernel functions + shared data structures
│   ├── FluidParticlesCS.compute    # Physics computation (density/pressure/integration)
│   └── BitonicSortCS.compute       # Bitonic Merge Sort
├── DrawParticles.shader            # GPU Instancing particle rendering
├── DrawGridDensity.shader          # Full-screen density field
├── DrawGridPressure.shader         # Full-screen pressure field
└── VizDensity.shader               # Density deviation coloring
```

## Per-Frame Pipeline

```
Spatial Hash Build → Bitonic Sort → Density Calculation → Pressure Calculation
→ Velocity/Position Integration → Particle Drawing
```

## SPH Algorithm

Uses a quadratic polynomial kernel function, with spatial hash grid optimizing neighbor search from O(n²) to O(n·k):

1. Compute grid ID for each particle
2. GPU Bitonic sort particles by grid ID
3. Build grid start indices for O(1) grid lookup
4. Traverse 3×3 neighboring grids to compute density and pressure
5. Explicit Euler integration to update velocity and position

## Requirements

- Unity 2022.3+ (URP 14.x)
- GPU with Compute Shader support (Shader Model 5.0)

## Usage

1. Open the project with Unity Hub
2. Open `Assets/Scenes/SampleScene.unity`
3. Adjust `FluidSimulatorController` parameters in the Inspector
4. Click Play, left-click on screen to spawn particles

## Inspector Parameters

| Parameter | Description |
|---|---|
| Particle Radius | Particle rendering radius |
| Smoothing Radius | SPH smoothing kernel radius, affects neighbor search range |
| Target Density | Target density — particles tend toward this distribution |
| Pressure Multiplier | Pressure coefficient — higher values increase incompressibility |
| Energy Damping | Energy decay, controls velocity loss after boundary collision |
| Gravity | Gravitational acceleration |
| Enable Update | Enable/pause physics simulation |
| Draw Particles | Show particles |
| Draw Grid Density Field | Show density field heatmap |
| Draw Viz Density Map | Show density deviation coloring |

## License

MIT
