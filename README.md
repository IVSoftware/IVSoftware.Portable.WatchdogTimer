# Watchdog timer

This timer is designed to complete after the time span set by the `Interval' property has elapsed since the most recent call to the `StartOrRestart` method.

This functionality may be accessed in a variety of ways.

***
**Event**

Like most timer classes, `WatchdogTimer` fires an event when the interval has run to completion.

    WatchdogTimer wdt = new WatchdogTimer(new TImeSpan.FromSeconds(5));