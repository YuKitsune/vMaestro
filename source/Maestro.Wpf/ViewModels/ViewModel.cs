using System.ComponentModel;
using System.Windows.Input;

namespace Maestro.Wpf.ViewModels;

public abstract class ViewModel : INotifyPropertyChanged, INotifyPropertyChanging
{
    public delegate void PropertyChangingEventHandler<T>(T oldValue, T newValue);
    public delegate void PropertyChangedEventHandler<T>(T oldValue, T newValue);

    readonly IDictionary<string, object> _properties = new Dictionary<string, object>();

    public T Get<T>(string key) => (T) _properties[key];

    public void Set<T>(
        string key,
        T newValue,
        PropertyChangingEventHandler<T>? onPropertyChanging = null,
        PropertyChangedEventHandler<T>? onPropertyChanged = null)
    {
        var oldValue = Get<T>(key);
        onPropertyChanging?.Invoke(oldValue, newValue);
        PropertyChanging?.Invoke(this, new PropertyChangingEventArgs(key));
        _properties[key] = newValue!;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(key));
        onPropertyChanged?.Invoke(oldValue, newValue);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event PropertyChangingEventHandler? PropertyChanging;
}

public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object _) => canExecute == null || canExecute();

    public void Execute(object _) => execute();
}

public class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) : ICommand
{
    public event EventHandler CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object argument) => canExecute == null || canExecute((T) argument);

    public void Execute(object argument) => execute((T)argument);
}

public interface IMessenger
{
    void Publish<T>(T message);
    void Subscribe<T>(IMessageHandler<T> handler);
    void Unsubscribe<T>(IMessageHandler<T> handler);
}

public interface IMessageHandler<T>
{
    void Handle(T message);
}

public class WeakReferenceMessenger : IMessenger
{
    readonly IDictionary<Type, List<WeakReference>> _subscriptions = new Dictionary<Type, List<WeakReference>>();

    public void Publish<T>(T message)
    {
        if (!_subscriptions.TryGetValue(typeof(T), out var subscriptions))
            return;

        subscriptions.RemoveAll(wr => !wr.IsAlive);

        foreach (var subscription in subscriptions)
        {
            if (subscription.Target is IMessageHandler<T> handler)
                handler.Handle(message);
        }
    }

    public void Subscribe<T>(IMessageHandler<T> handler)
    {
        if (_subscriptions.TryGetValue(typeof(T), out var subscriptions))
            subscriptions.Add(new WeakReference(handler));

        _subscriptions[typeof(T)] = [new WeakReference(handler)];
    }

    public void Unsubscribe<T>(IMessageHandler<T> handler)
    {
        if (!_subscriptions.ContainsKey(typeof(T)))
            return;

        _subscriptions[typeof(T)].RemoveAll(wr => wr.Target == handler);
    }
}
