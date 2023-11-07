# Watchdog timer

#### _What is it?_

A timer that completes _one_ time, after the TimeSpan in the `Interval` property has elapsed since the _most recent call_ to the `StartOrRestart` method, regardless of the number of restarts. Invoking the `Cancel` method negates any enqueued action or event in the queue.

#### _Examples_

**Display an alert after user moves the mouse**

We know that we're interested in mouse move events, but obviously don't want to make a message eash time one occurs.


**Debouncing**

An impatient user might tap multiple times. A watchdog timer can ensure that an action takes place the first time, and requires a cooling off interval before the same action can happen again.

***
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
    /// Restart the watchdog timer.
    /// </summary>
    /// <remarks>
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, fire an event using a custom parameterized EventArgs object.
    /// </remarks>
    public void StartOrRestart(EventArgs e) => StartOrRestart(null, e);

    /// <summary>
    /// Restart the watchdog timer.
    /// </summary>
    /// <remarks>
    /// Subscribe to the RanToCompletion event to receive notification of completion.  
    /// On completion, invoke a parameterized action.
    /// </remarks>
    public void StartOrRestart(Action action) => StartOrRestart(action, EventArgs.Empty);
        

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

[I want to call a method after some delay when an event is raised, but any subsequent events should "restart" this delay.](https://stackoverflow.com/q/75284980/5438626)


