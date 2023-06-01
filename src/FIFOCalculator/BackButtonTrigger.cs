using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;

namespace FIFOCalculator;

public class BackButtonTrigger : Trigger<Visual>
{
    private IDisposable subs = Disposable.Empty;

    protected override void OnAttachedToVisualTree()
    {
        base.OnAttachedToVisualTree();

        var topLevel = TopLevel.GetTopLevel(AssociatedObject)!;
        subs = Observable.FromEventPattern<RoutedEventArgs>(topLevel, "BackRequested")
            .Do(eventPattern =>
            {
                eventPattern.EventArgs.Handled = true;
                Interaction.ExecuteActions(this, Actions, null);
            })
            .Subscribe();
    }

    protected override void OnDetachedFromVisualTree()
    {
        base.OnDetachedFromVisualTree();
        subs.Dispose();
    }
}