namespace FIFOCalculator.ViewModels;

public class Section
{
    public Section(string name, object viewModel)
    {
        Name = name;
        ViewModel = viewModel;
    }

    public string Name { get; }
    public object ViewModel { get; }
}