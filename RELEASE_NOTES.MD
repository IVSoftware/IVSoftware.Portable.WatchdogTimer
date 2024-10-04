# IVSoftware.Portable.WatchdogTimer - Version 1.3.0

## Release Notes

### Main Features and Enhancements:
- **Support for Default Actions at Instantiation**:
    - You can now set default actions for `InitialAction` and `CompletedAction` at the time of instantiation. These defaults will be automatically used in the `StartOrRestart` method when no specific actions are provided.
    - This enhances the flexibility of the timer, allowing reusable behavior across multiple instances without requiring actions to be passed in every time.

```csharp
// Example of setting defaults at instantiation
var watchdogTimer = new WatchdogTimer(
    defaultInitialAction: () => Console.WriteLine("Timer Started"),
    defaultCompleteAction: () => Console.WriteLine("Timer Completed")
);
```

### Minor Fixes:
- **Improved Property Change Notifications**:
    - Minor improvements to the `OnPropertyChanged` method to ensure more reliable data binding and property change notifications. This ensures that UI elements and other listeners respond correctly when the `Running` property or other properties change.

### Upgrade Instructions:
- If you previously set actions on each call to `StartOrRestart`, you can now specify default actions at instantiation for greater convenience and cleaner code.

- Review any custom implementations of property change handlers to take advantage of the improved `OnPropertyChanged` method.

This release improves both the usability and reliability of the `WatchdogTimer` class, making it easier to configure timers and work with property changes in data-bound environments.
