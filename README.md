# Idle Game Optimizer

A Blazor WebAssembly application that calculates the **Highest % Production Increase per Cost** for idle game upgrades.

## Features

- **Generator Management**: Add, edit, and manage flat-gain generators
- **Research Management**: Add, edit, and manage multiplier-based research upgrades
- **Smart Calculations**: Automatically calculates and ranks upgrades by value score (Gain% / Cost)
- **LocalStorage Persistence**: All data is saved automatically to browser local storage
- **Modern UI**: Built with MudBlazor with dark theme support

## Project Structure

```
IdleOptimizer/
├── Models/          # Data models (Generator, Research, UpgradeResult)
├── Services/        # Business logic (CalculationService, LocalStorageService)
├── Pages/           # Main dashboard page
├── Components/      # Reusable components (Add dialogs)
└── Shared/          # Shared layout components
```

## Getting Started

### Prerequisites

- .NET 8 SDK
- A modern web browser

### Running the Application

1. Restore NuGet packages:
   ```bash
   dotnet restore
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser and navigate to the URL shown in the console (typically `https://localhost:5001` or `http://localhost:5000`)

## How It Works

### Generators
Generators produce a flat amount of resources. Each generator has:
- **Base Production**: The base production per unit
- **Count**: Number of generators owned
- **Cost**: Base cost to purchase
- **Cost Increase Rate**: Multiplier for cost increase per purchase

### Research
Research upgrades apply multipliers to target generators. Each research has:
- **Multiplier Value**: The percentage increase (e.g., 0.25 = +25%)
- **Cost**: Cost to purchase
- **Target Generators**: List of generator names this research affects

### Value Score Calculation
The app calculates:
- **Gain%**: Percentage increase in total production after purchase
- **Value Score**: Gain% / Cost
- Upgrades are ranked by Value Score (highest first)

## Usage

1. **Add Generators**: Click "Add Generator" to create new generators
2. **Add Research**: Click "Add Research" to create new research upgrades
3. **View Rankings**: The table automatically shows all upgrades ranked by value score
4. **Apply Purchase**: Click "Apply" on any upgrade to simulate purchasing it
5. **Edit Items**: Click "Edit" to modify existing generators or research
6. **Delete Items**: Click "Delete" to remove items

## Technologies

- **.NET 8**: Blazor WebAssembly
- **C# 12**: Latest C# language features
- **MudBlazor**: Material Design component library
- **Blazored.LocalStorage**: Browser local storage integration

## License

This project is provided as-is for educational and personal use.

