using CommunityToolkit.Mvvm.ComponentModel;
using OpenCrawler.Core.Models;

namespace OpenCrawler.App.ViewModels;

public partial class CategoryNode : ObservableObject
{
    public Category Model { get; }
    [ObservableProperty] private string _name;

    public CategoryNode(Category model)
    {
        Model = model;
        _name = model.Name;
    }

    public long Id => Model.Id;
}
