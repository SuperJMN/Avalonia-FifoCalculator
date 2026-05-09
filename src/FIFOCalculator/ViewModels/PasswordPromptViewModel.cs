using System;
using System.Reactive.Linq;
using ReactiveUI;

namespace FIFOCalculator.ViewModels;

public sealed class PasswordPromptViewModel : ViewModelBase
{
    private string password = "";

    public PasswordPromptViewModel(string message)
    {
        Message = message;
        CanSubmit = this.WhenAnyValue(x => x.Password)
            .Select(value => !string.IsNullOrWhiteSpace(value));
    }

    public string Message { get; }

    public string Password
    {
        get => password;
        set => this.RaiseAndSetIfChanged(ref password, value);
    }

    public IObservable<bool> CanSubmit { get; }
}
