namespace StickyNotes.Injection;

using Microsoft.Extensions.DependencyInjection;
using StickyNotes.State;
using StickyNotes.Utils;

public sealed class ServiceContainer
{
    public static readonly ServiceContainer Instance = new();

    private readonly ServiceProvider provider;

    private ServiceContainer()
    {
        ServiceCollection services = new();

        services.AddSingleton<IStickyNotePaths, StickyNotePaths>();
        services.AddSingleton<IBackup, Backup>();
        services.AddSingleton<IStore, Store>();
        services.AddSingleton<IWindowManager, WindowManager>();
        services.AddSingleton<IGlobalWatcher, GlobalWatcher>();

        provider = services.BuildServiceProvider();
    }

    private T DoGetService<T>() => provider.GetService<T>()!;

    public static T GetService<T>() => Instance.DoGetService<T>();
}