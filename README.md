## Sensor Event Manager

### Problem Encountered
The main issue I faced was handling sensor status updates out of order due to asynchronous calls.  
Although sensor statuses were received in order, they were processed in a different order because `GetSensorById()` is asynchronous and slow.

### Solution
I used a `BlockingCollection<SensorStatus>` to queue all incoming sensor statuses, and processed them sequentially in a dedicated background task using `GetConsumingEnumerable()`. This ensured first-in-first-out (FIFO) processing.

---

### Requirements & How I Handled Them

-  **Single status per sensor:** Used `ConcurrentDictionary<Guid, SensorStatus>` to replace or add statuses by `SensorId`.
-  **Auto-remove after 15 seconds:** Scheduled a delayed removal using `Task.Delay(15s)` and verified timestamp before removing.
-  **UI sorted by sensor name/number:** Used `CollectionViewSource.SortDescriptions` in the ViewModel, with sorting based on extracted number.
-  **Multithreaded-safe:** All updates are thread-safe using concurrent collections and a dedicated processing thread.

---

> Built with WPF + MVVM + Rx + C#
