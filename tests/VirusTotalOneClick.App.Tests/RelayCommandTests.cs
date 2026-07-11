using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class RelayCommandTests
{
    [Fact]
    public void ExecuteRunsTheDelegateAndCanExecuteHonorsThePredicate()
    {
        var ran = false;
        var allowed = false;
        var command = new RelayCommand(() => ran = true, () => allowed);

        Assert.False(command.CanExecute(null));
        allowed = true;
        Assert.True(command.CanExecute(null));

        command.Execute(null);

        Assert.True(ran);
    }

    [Fact]
    public void RaiseCanExecuteChangedNotifiesSubscribers()
    {
        var command = new RelayCommand(() => { });
        var raised = 0;
        command.CanExecuteChanged += (_, _) => raised++;

        command.RaiseCanExecuteChanged();

        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task AsyncCommandIsNotExecutableWhileItIsRunning()
    {
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var observedWhileRunning = true;
        var command = new AsyncRelayCommand(() => gate.Task);

        Assert.True(command.CanExecute(null));
        command.Execute(null);
        observedWhileRunning = command.CanExecute(null);
        gate.SetResult();
        await Task.Yield();

        Assert.False(observedWhileRunning);
    }

    [Fact]
    public async Task AsyncCommandBecomesExecutableAgainAfterCompletion()
    {
        var command = new AsyncRelayCommand(() => Task.CompletedTask);

        command.Execute(null);
        await Task.Yield();

        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public async Task AnUnhandledExceptionIsRoutedToTheErrorCallbackNotThrown()
    {
        Exception? seen = null;
        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new AsyncRelayCommand(
            () => Task.FromException(new InvalidOperationException("boom")),
            onError: ex =>
            {
                seen = ex;
                completed.SetResult();
            });

        command.Execute(null);
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.IsType<InvalidOperationException>(seen);
        Assert.Equal("boom", seen.Message);
        Assert.True(command.CanExecute(null));
    }

    [Fact]
    public void ExecuteIsANoOpWhenCanExecuteIsFalse()
    {
        var ran = false;
        var command = new AsyncRelayCommand(() =>
        {
            ran = true;
            return Task.CompletedTask;
        },
        () => false);

        command.Execute(null);

        Assert.False(ran);
    }
}
