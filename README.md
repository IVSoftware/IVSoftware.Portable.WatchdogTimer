# Watchdog timer

## _What It Is_

A timer that triggers once after the `TimeSpan` set in the `Interval` property has elapsed since the most recent call to the `StartOrRestart` method, regardless of how many restarts occur. Invoking the `Cancel` method prevents any pending actions or events.

### _Examples_

**Display an alert after user moves the mouse**

Suppose we want to handle mouse move events but only trigger a single consolidated response when the mouse has stopped moving for a short period. Using a WatchdogTimer, we can ensure that an alert is displayed only after the user has stopped moving the mouse for a defined interval.


![Winforms App Image](https://raw.githubusercontent.com/IVSoftware/IVSoftware.Portable.WatchdogTimer/master/IVSoftware.Portable.WatchdogTimer/Screenshots/winforms.png)

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
Explanation:

The OnMouseMove method resets the timer each time the mouse moves, and starts the timer on the first move.
If the mouse stops moving for 0.5 seconds (as defined by Interval), the StartOrRestart method executes, displaying a message with the timestamps of the event.

___

**Debouncing**

For impatient users who tap multiple times, a WatchdogTimer can ensure the action occurs only once, requiring a cooldown period before allowing the same action again.

![Maui .Net Default App Image with Modifications](https://raw.githubusercontent.com/IVSoftware/IVSoftware.Portable.WatchdogTimer/master/IVSoftware.Portable.WatchdogTimer/Screenshots/maui.png
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

**Constructor**

```
/// <summary>
/// Initializes a new instance of the <see cref="WatchdogTimer"/> class with optional default actions for the initial and completion phases.
/// </summary>
/// <param name="defaultInitialAction">An optional default action to be executed when the timer starts, if no other initial action is provided in the call to the `StartOrRestart` method.</param>
/// <param name="defaultCompleteAction">An optional default action to be executed upon successful completion of the timer, if no other completion action is provided in the call to the `StartOrRestart` method.</param>
/// <remarks>
/// The preferred usage is to choose one of the following approaches:
/// - Always use default actions, or
/// - Always use actions passed in as arguments to the method.
/// However, in situations where both defaults and method arguments are provided, an orderly scheme is in place for resolving conflicts: actions passed as arguments to the method will always take precedence over default actions, even if defaults are set.
/// This ensures the timer behaves predictably and consistently in scenarios where both default and explicit actions are provided.
/// </remarks>
public WatchdogTimer(Action defaultInitialAction = null, Action defaultCompleteAction = null);
```
___

**Methods**

```
/// <summary>
/// Restarts the watchdog timer using default completion actions.
/// </summary>
/// <remarks>
/// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
/// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
/// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
/// This overload does not specify a completion action, but if <see cref="DefaultCompleteAction"/> is set, it will be executed. 
/// </remarks>
public void StartOrRestart();

/// <summary>
/// Restarts the watchdog timer using default completion actions and specified event arguments.
/// </summary>
/// <remarks>
/// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
/// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
/// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
/// This overload does not specify a completion action, but if <see cref="DefaultCompleteAction"/> is set, it will be executed. 
/// </remarks>
/// <param name="e">An optional <see cref="EventArgs"/> object to pass to the completion event. 
/// If null, an empty <see cref="EventArgs"/> will be used.</param>
public void StartOrRestart(EventArgs e);

/// <summary>
/// Restarts the watchdog timer using a specified completion action.
/// </summary>
/// <remarks>
/// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
/// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
/// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
/// The provided completion action will be executed upon successful completion of the timer, overriding the <see cref="DefaultCompleteAction"/>.
/// </remarks>
/// <param name="action">The action to execute upon successful completion of the timer. 
/// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
/// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
public void StartOrRestart(Action action);

/// <summary>
/// Restarts the watchdog timer using a specified completion action and event arguments.
/// </summary>
/// <remarks>
/// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
/// On completion, an event is fired with the provided <see cref="EventArgs"/> object.
/// This overload does not specify an initial action, but if <see cref="DefaultInitialAction"/> is set, it will be executed. 
/// The provided completion action will be executed upon successful completion of the timer, overriding the <see cref="DefaultCompleteAction"/>.
/// </remarks>
/// <param name="action">The action to execute upon successful completion of the timer. 
/// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
/// <param name="e">An optional <see cref="EventArgs"/> object to pass to the completion event. 
/// If null, an empty <see cref="EventArgs"/> will be used.</param>
/// <exception cref="ArgumentNullException">Thrown when the <paramref name="action"/> parameter is null.</exception>
public void StartOrRestart(Action action, EventArgs e);

/// <summary>
/// Restarts the watchdog timer using specified initial and completion actions.
/// </summary>
/// <remarks>
/// Clients may subscribe to the <see cref="RanToCompletion"/> event to receive notifications upon completion. 
/// On completion, an event is fired with an empty <see cref="EventArgs"/> object.
/// This overload allows clients to specify both an initial action and a completion action, 
/// and both actions will override <see cref="DefaultInitialAction"/> and <see cref="DefaultCompleteAction"/> if they are set.
/// </remarks>
/// <param name="initialAction">The action to execute when starting the timer. 
/// This parameter cannot be null and will override the <see cref="DefaultInitialAction"/> if it is set.</param>
/// <param name="completeAction">The action to execute upon successful completion of the timer. 
/// This parameter cannot be null and will override the <see cref="DefaultCompleteAction"/> if it is set.</param>
/// <exception cref="ArgumentNullException">Thrown when either <paramref name="initialAction"/> or <paramref name="completeAction"/> is null.</exception>
public void StartOrRestart(Action initialAction, Action completeAction);

/// <summary>
/// Cancels the current timer, preventing any pending completion actions and events.
/// </summary>
/// <remarks>
/// Calling this method stops the timer and prevents any pending completion actions from running.
/// You can subscribe to the <see cref="Cancelled"/> event to be notified when the timer is cancelled.
/// </remarks>
public void Cancel();
```
***
**Properties**

```
/// <summary>
/// Gets or sets the time interval for the watchdog timer. This interval defines the delay period before the completion action is triggered.
/// </summary>
/// <value>The interval duration for the timer. Defaults to 1 second if not explicitly set.</value>
public TimeSpan Interval { get; set; }

/// <summary>
/// Gets a value indicating whether the timer is currently running. This property is bindable.
/// </summary>
/// <value><c>true</c> if the timer is running; otherwise, <c>false</c>.</value>
/// <remarks>
/// The running state is managed internally by the <see cref="WatchdogTimer"/> class and cannot be set externally. 
/// This property supports data binding and triggers the <see cref="PropertyChanged"/> event when the running state changes.
/// </remarks>
public bool Running { get; }

/// <summary>
/// Gets the default action to be executed when the timer starts, if no other initial action is provided. 
/// This property is read-only and can only be set through the constructor.
/// </summary>
/// <value>The default initial action.</value>
public Action DefaultInitialAction { get; }

/// <summary>
/// Gets the default action to be executed upon successful completion of the timer, if no other completion action is provided. 
/// This property is read-only and can only be set through the constructor.
/// </summary>
/// <value>The default completion action.</value>
public Action DefaultCompleteAction { get; }    
```
***

**Events**

```
/// <summary>
/// Raised when the timer successfully completes its countdown and the completion action is invoked.
/// </summary>
public event EventHandler RanToCompletion;

/// <summary>
/// Raised when the timer is cancelled before completing its countdown.
/// </summary>
public event EventHandler Cancelled;

/// <summary>
/// Raised when a property value changes, supporting data binding for the <see cref="Running"/> property.
/// </summary>
/// <remarks>
/// This event is triggered whenever the <see cref="Running"/> property changes. 
/// It is part of the <see cref="INotifyPropertyChanged"/> interface to support data binding in UI frameworks.
/// </remarks>
public event PropertyChangedEventHandler PropertyChanged;    
```
***


**StackOverflow**

[Call a method after some delay when an event is raised, but any subsequent events should "restart" this delay.](https://stackoverflow.com/q/75284980/5438626)


