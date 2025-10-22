using System.Collections.Generic;

namespace Dataportal.ViewModels;

public class AccueilIndexViewModel
{
    public IReadOnlyList<LatestDatasetViewModel> LatestDatasets { get; init; } = new List<LatestDatasetViewModel>();
}

public class LatestDatasetViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? EnergyType { get; init; }
    public string IconName { get; init; } = "energy_savings_leaf";
}