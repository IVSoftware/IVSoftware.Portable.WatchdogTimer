# Watchdog timer

#### _What is it?_

A timer that completes _one_ time, after the TimeSpan in the `Interval` property has elapsed since the _most recent call_ to the `StartOrRestart` method, regardless of the number of restarts. Invoking the `Cancel` method negates any enqueued action or event in the queue.

#### _Examples_

**Display an alert after user moves the mouse**

Suppose we're interested in mouse move events, but obviously don't want to make a message each time one occurs.


![Winforms App Image](https://raw.githubusercontent.com/IVSoftware/IVSoftware.Portable.WatchdogTimer/readme-inprog/IVSoftware.Portable.WatchdogTimer/Screenshots/winforms.png)

```
public partial class MainForm : Form
{
    public MainForm() => InitializeComponent();

    WatchdogTimer _wdtMouseMove = new WatchdogTimer
    {
        Interval = TimeSpan.FromSeconds(0.5)
    };

    DateTime _mouseStartTimeStamp = DateTime.MinValue;
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if(!_wdtMouseMove.Running)
        {
            _mouseStartTimeStamp = DateTime.Now;
        }
        _wdtMouseMove.StartOrRestart(() =>
        {
            BeginInvoke(() =>
                MessageBox.Show(
                    $"Mouse down @ {
                        _mouseStartTimeStamp.ToString(@"hh\:mm\:ss tt")
                    }\nTime now is {
                        DateTime.Now.ToString(@"hh\:mm\:ss tt")
                    }."));
        });
        base.OnMouseMove(e);
    }
}
```


**Debouncing**

An impatient user might tap multiple times. A watchdog timer can ensure that an action takes place the first time, and requires a cooling off interval before the same action can happen again.

![Maui .Net Default App Image with Modifications](https://raw.githubusercontent.com/IVSoftware/IVSoftware.Portable.WatchdogTimer/readme-inprog/IVSoftware.Portable.WatchdogTimer/Screenshots/maui.png
)

```
public partial class MainPage : ContentPage
{
    int count = 0;

    public MainPage()
    {
        BindingContext = this;
        InitializeComponent();
    }
    private void OnCounterClicked(object sender, EventArgs e)
    {
        if (checkboxIsLockOutMechanismEnabled.IsChecked)
        {
            ExtendLockout();
        }
        count++;
        if (count == 1)
            CounterBtn.Text = $"Clicked {count} time";
        else
            CounterBtn.Text = $"Clicked {count} times";

        SemanticScreenReader.Announce(CounterBtn.Text);
    }
    WatchdogTimer _wdtOverlay = new WatchdogTimer { Interval = TimeSpan.FromSeconds(2) };

    private void ExtendLockout()
    {
        _wdtOverlay.StartOrRestart(
            initialAction: () => IsLockedOut = true,
            completeAction: () => IsLockedOut = false);
    }

    public bool IsLockedOut
    {
        get => _isLockedOut;
        set
        {
            if (!Equals(_isLockedOut, value))
            {
                _isLockedOut = value;
                OnPropertyChanged();
            }
        }
    }
    bool _isLockedOut = false;

    private void OnOverlayTapped(object sender, TappedEventArgs e)
    {
        ExtendLockout();
    }
}
```
___

**Methods**

    /// <summary>
    /// Restart the watchdog timer.
    /// </summary>
    /// <remarks>
    /// Core method that can take a parameterized action as well as a custom EventArgs object.
    /// </remarks>
    public void StartOrRestart(Action action, EventArgs e)
    {
        Running = true;
        _wdtCount++;
        var capturedCount = _wdtCount;
        _isCancelled= false;
        Task
            .Delay(Interval)
            .GetAwaiter()
            .OnCompleted(() =>
            {
                // If the 'captured' localCount has not changed after awaiting the Interval, 
                // it indicates that no new 'bones' have been thrown during that interval.        
                if (capturedCount.Equals(_wdtCount) && !_isCancelled)
                {
                    action?.Invoke();
                    Running = false;
                    RanToCompletion?.Invoke(this, e ?? EventArgs.Empty);
                }
            });
    }

    /// <summary>
    /// Restart the watchdog timer.
    /// </summary>
    /// <remarks>
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, fire an event with an empty EventArgs object.
    /// </remarks>
    public void StartOrRestart() => StartOrRestart(null, EventArgs.Empty);

    /// <summary>
    /// Restart the watchdog timer injecting a custom event to be fired on completion.
    /// </summary>
    /// <remarks>
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, fire an event using a custom parameterized EventArgs object.
    /// </remarks>
    public void StartOrRestart(EventArgs e) => StartOrRestart(null, e);

    /// <summary>
    /// Restart the watchdog timer with action to invoke on completion.
    /// </summary>
    /// <remarks>
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, invoke a parameterized action.
    /// </remarks>
    public void StartOrRestart(Action action) => StartOrRestart(action, EventArgs.Empty);

    /// <summary>
    /// Restart the watchdog timer with actions to invoke on initialization and completion.
    /// </summary>
    /// <remarks>
    /// Invoke an initial parameterized action if not already running.
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, invoke a parameterized action.
    /// </remarks>
    public void StartOrRestart(Action initialAction, Action completeAction);        

    public void Cancel() => _isCancelled = true;    

***
**Properties**

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public bool Running { get; private set; }

***
**Event**

    public event EventHandler RanToCompletion;

***


**StackOverflow**

[Call a method after some delay when an event is raised, but any subsequent events should "restart" this delay.](https://stackoverflow.com/q/75284980/5438626)


