using System.Runtime.CompilerServices;
using ReactiveUI.Builder;

namespace TestProject1;

public static class ReactiveUiTestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        RxAppBuilder.CreateReactiveUIBuilder().BuildApp();
    }
}
